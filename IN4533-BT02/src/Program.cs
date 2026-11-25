using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Bật hỗ trợ chuẩn lỗi Problem Details (RFC 9457)
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

var validStatuses = new HashSet<string>(StringComparer.Ordinal)
{
    "pending", "confirmed", "cancelled"
};

string GetConnectionString(IConfiguration config) =>
    config.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Chưa cấu hình chuỗi kết nối 'ConnectionStrings:Default'. Hãy dùng user-secrets hoặc biến môi trường.");

// ============================================================================
// 1. GET /api/v1/courses/{courseId:int}/registrations
//    Xem danh sách đăng ký của một khoá học (có lọc tuỳ chọn ?status=...)
// ============================================================================
app.MapGet("/api/v1/courses/{courseId:int}/registrations", async (
    int courseId,
    string? status,
    IConfiguration config,
    HttpContext http) =>
{
    if (courseId <= 0)
    {
        return Results.Problem(
            type: "https://eduhub.example/errors/invalid-course-id",
            title: "Mã khoá học không hợp lệ",
            detail: "Tham số courseId phải là số nguyên dương.",
            statusCode: StatusCodes.Status400BadRequest,
            instance: http.Request.Path);
    }

    if (!string.IsNullOrWhiteSpace(status) && !validStatuses.Contains(status))
    {
        return Results.Problem(
            type: "https://eduhub.example/errors/invalid-status",
            title: "Trạng thái lọc không hợp lệ",
            detail: "Tham số status chỉ nhận một trong ba giá trị: pending, confirmed, cancelled.",
            statusCode: StatusCodes.Status400BadRequest,
            instance: http.Request.Path);
    }

    await using var conn = new NpgsqlConnection(GetConnectionString(config));
    await conn.OpenAsync();

    // Kiểm tra khoá học có tồn tại không (truy vấn tham số hoá chống SQL Injection)
    await using (var checkCmd = new NpgsqlCommand(
        "SELECT 1 FROM courses WHERE id = @courseId;", conn))
    {
        checkCmd.Parameters.AddWithValue("@courseId", courseId);
        var exists = await checkCmd.ExecuteScalarAsync();
        if (exists is null)
        {
            return Results.Problem(
                type: "https://eduhub.example/errors/course-not-found",
                title: "Không tìm thấy khoá học",
                detail: $"Khoá học có mã {courseId} không tồn tại.",
                statusCode: StatusCodes.Status404NotFound,
                instance: http.Request.Path);
        }
    }

    const string sql = """
        SELECT r.id,
               r.member_id,
               m.full_name,
               m.email,
               r.course_id,
               r.status,
               r.registered_at
        FROM registrations r
        JOIN members m ON m.id = r.member_id
        WHERE r.course_id = @courseId
          AND (@status IS NULL OR r.status = @status)
        ORDER BY r.id ASC;
        """;

    var items = new List<RegistrationDetailDto>();
    await using (var cmd = new NpgsqlCommand(sql, conn))
    {
        cmd.Parameters.AddWithValue("@courseId", courseId);
        cmd.Parameters.Add(new NpgsqlParameter("@status", NpgsqlTypes.NpgsqlDbType.Varchar)
        {
            Value = (object?)status ?? DBNull.Value
        });

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new RegistrationDetailDto(
                Id: reader.GetInt32(0),
                MemberId: reader.GetInt32(1),
                MemberName: reader.GetString(2),
                MemberEmail: reader.GetString(3),
                CourseId: reader.GetInt32(4),
                Status: reader.GetString(5),
                RegisteredAt: reader.GetFieldValue<DateTimeOffset>(6)));
        }
    }

    return Results.Ok(items);
});

// ============================================================================
// 2. POST /api/v1/courses/{courseId:int}/registrations
//    Tạo phiếu đăng ký mới cho một khoá học (gói trong Transaction + FOR UPDATE)
// ============================================================================
app.MapPost("/api/v1/courses/{courseId:int}/registrations", async (
    int courseId,
    CreateRegistrationRequest? req,
    IConfiguration config,
    HttpContext http) =>
{
    if (courseId <= 0 || req is null || req.MemberId <= 0)
    {
        return Results.Problem(
            type: "https://eduhub.example/errors/invalid-request",
            title: "Dữ liệu đầu vào không hợp lệ",
            detail: "Cần cung cấp courseId và memberId là số nguyên dương.",
            statusCode: StatusCodes.Status400BadRequest,
            instance: http.Request.Path);
    }

    await using var conn = new NpgsqlConnection(GetConnectionString(config));
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    try
    {
        // Khoá hàng khoá học bằng FOR UPDATE để tránh tranh chấp chỗ cuối (race condition)
        int capacity;
        await using (var courseCmd = new NpgsqlCommand(
            "SELECT capacity FROM courses WHERE id = @courseId FOR UPDATE;", conn, tx))
        {
            courseCmd.Parameters.AddWithValue("@courseId", courseId);
            var capObj = await courseCmd.ExecuteScalarAsync();
            if (capObj is null)
            {
                return Results.Problem(
                    type: "https://eduhub.example/errors/course-not-found",
                    title: "Không tìm thấy khoá học",
                    detail: $"Khoá học có mã {courseId} không tồn tại.",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: http.Request.Path);
            }
            capacity = (int)capObj;
        }

        // Kiểm tra thành viên tồn tại
        await using (var memberCmd = new NpgsqlCommand(
            "SELECT 1 FROM members WHERE id = @memberId;", conn, tx))
        {
            memberCmd.Parameters.AddWithValue("@memberId", req.MemberId);
            if (await memberCmd.ExecuteScalarAsync() is null)
            {
                return Results.Problem(
                    type: "https://eduhub.example/errors/member-not-found",
                    title: "Không tìm thấy thành viên",
                    detail: $"Thành viên có mã {req.MemberId} không tồn tại.",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: http.Request.Path);
            }
        }

        // Kiểm tra đăng ký trùng (UNIQUE member_id, course_id)
        await using (var dupCmd = new NpgsqlCommand(
            "SELECT 1 FROM registrations WHERE member_id = @memberId AND course_id = @courseId;",
            conn, tx))
        {
            dupCmd.Parameters.AddWithValue("@memberId", req.MemberId);
            dupCmd.Parameters.AddWithValue("@courseId", courseId);
            if (await dupCmd.ExecuteScalarAsync() is not null)
            {
                return Results.Problem(
                    type: "https://eduhub.example/errors/already-registered",
                    title: "Đã đăng ký khoá học này",
                    detail: $"Thành viên {req.MemberId} đã có phiếu đăng ký ở khoá học {courseId}.",
                    statusCode: StatusCodes.Status409Conflict,
                    instance: http.Request.Path);
            }
        }

        // Kiểm tra sức chứa khoá học (không tính các phiếu đã cancelled)
        await using (var countCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM registrations WHERE course_id = @courseId AND status <> 'cancelled';",
            conn, tx))
        {
            countCmd.Parameters.AddWithValue("@courseId", courseId);
            var currentCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
            if (currentCount >= capacity)
            {
                return Results.Problem(
                    type: "https://eduhub.example/errors/course-full",
                    title: "Khoá học đã đủ chỗ",
                    detail: $"Khoá học {courseId} đã đạt tối đa {capacity} chỗ.",
                    statusCode: StatusCodes.Status409Conflict,
                    instance: http.Request.Path);
            }
        }

        const string insertSql = """
            INSERT INTO registrations (member_id, course_id)
            VALUES (@memberId, @courseId)
            RETURNING id, member_id, course_id, status, registered_at;
            """;

        RegistrationDto created;
        await using (var insertCmd = new NpgsqlCommand(insertSql, conn, tx))
        {
            insertCmd.Parameters.AddWithValue("@memberId", req.MemberId);
            insertCmd.Parameters.AddWithValue("@courseId", courseId);

            await using var reader = await insertCmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            created = new RegistrationDto(
                Id: reader.GetInt32(0),
                MemberId: reader.GetInt32(1),
                CourseId: reader.GetInt32(2),
                Status: reader.GetString(3),
                RegisteredAt: reader.GetFieldValue<DateTimeOffset>(4));
        }

        await tx.CommitAsync();
        return Results.Created($"/api/v1/registrations/{created.Id}", created);
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        return Results.Problem(
            type: "https://eduhub.example/errors/already-registered",
            title: "Đã đăng ký khoá học này",
            detail: $"Thành viên {req.MemberId} đã có phiếu đăng ký ở khoá học {courseId}.",
            statusCode: StatusCodes.Status409Conflict,
            instance: http.Request.Path);
    }
});

// ============================================================================
// 3. PATCH /api/v1/registrations/{id:int}
//    Xác nhận đăng ký (cập nhật một phần trường status -> "confirmed")
// ============================================================================
app.MapPatch("/api/v1/registrations/{id:int}", async (
    int id,
    UpdateRegistrationStatusRequest? req,
    IConfiguration config,
    HttpContext http) =>
{
    if (id <= 0 || req is null || string.IsNullOrWhiteSpace(req.Status) || !validStatuses.Contains(req.Status))
    {
        return Results.Problem(
            type: "https://eduhub.example/errors/invalid-status",
            title: "Trạng thái cập nhật không hợp lệ",
            detail: "Trường status bắt buộc và chỉ nhận: pending, confirmed, cancelled.",
            statusCode: StatusCodes.Status400BadRequest,
            instance: http.Request.Path);
    }

    await using var conn = new NpgsqlConnection(GetConnectionString(config));
    await conn.OpenAsync();

    await using (var curCmd = new NpgsqlCommand(
        "SELECT status FROM registrations WHERE id = @id;", conn))
    {
        curCmd.Parameters.AddWithValue("@id", id);
        var currentStatus = (string?)await curCmd.ExecuteScalarAsync();
        if (currentStatus is null)
        {
            return Results.Problem(
                type: "https://eduhub.example/errors/registration-not-found",
                title: "Không tìm thấy phiếu đăng ký",
                detail: $"Phiếu đăng ký số {id} không tồn tại.",
                statusCode: StatusCodes.Status404NotFound,
                instance: http.Request.Path);
        }

        if (currentStatus == "cancelled" && req.Status == "confirmed")
        {
            return Results.Problem(
                type: "https://eduhub.example/errors/registration-cancelled",
                title: "Không thể xác nhận phiếu đã huỷ",
                detail: $"Phiếu đăng ký số {id} đã ở trạng thái cancelled.",
                statusCode: StatusCodes.Status409Conflict,
                instance: http.Request.Path);
        }
    }

    const string updateSql = """
        UPDATE registrations
        SET status = @status
        WHERE id = @id
        RETURNING id, member_id, course_id, status, registered_at;
        """;

    await using var cmd = new NpgsqlCommand(updateSql, conn);
    cmd.Parameters.AddWithValue("@status", req.Status);
    cmd.Parameters.AddWithValue("@id", id);

    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();
    var updated = new RegistrationDto(
        Id: reader.GetInt32(0),
        MemberId: reader.GetInt32(1),
        CourseId: reader.GetInt32(2),
        Status: reader.GetString(3),
        RegisteredAt: reader.GetFieldValue<DateTimeOffset>(4));

    return Results.Ok(updated);
});

// ============================================================================
// 4. DELETE /api/v1/registrations/{id:int}
//    Huỷ đăng ký (chuyển trạng thái sang 'cancelled', trả 204 No Content)
// ============================================================================
app.MapDelete("/api/v1/registrations/{id:int}", async (
    int id,
    IConfiguration config,
    HttpContext http) =>
{
    if (id <= 0)
    {
        return Results.Problem(
            type: "https://eduhub.example/errors/invalid-registration-id",
            title: "Mã phiếu đăng ký không hợp lệ",
            detail: "Tham số id phải là số nguyên dương.",
            statusCode: StatusCodes.Status400BadRequest,
            instance: http.Request.Path);
    }

    await using var conn = new NpgsqlConnection(GetConnectionString(config));
    await conn.OpenAsync();

    await using var cmd = new NpgsqlCommand(
        "UPDATE registrations SET status = 'cancelled' WHERE id = @id RETURNING id;", conn);
    cmd.Parameters.AddWithValue("@id", id);

    var affected = await cmd.ExecuteScalarAsync();
    if (affected is null)
    {
        return Results.Problem(
            type: "https://eduhub.example/errors/registration-not-found",
            title: "Không tìm thấy phiếu đăng ký",
            detail: $"Phiếu đăng ký số {id} không tồn tại.",
            statusCode: StatusCodes.Status404NotFound,
            instance: http.Request.Path);
    }

    return Results.NoContent();
});

app.Run();

// Các kiểu dữ liệu DTO truyền nhận JSON (tự động ánh xạ camelCase)
public record CreateRegistrationRequest(int MemberId);
public record UpdateRegistrationStatusRequest(string Status);

public record RegistrationDto(
    int Id,
    int MemberId,
    int CourseId,
    string Status,
    DateTimeOffset RegisteredAt);

public record RegistrationDetailDto(
    int Id,
    int MemberId,
    string MemberName,
    string MemberEmail,
    int CourseId,
    string Status,
    DateTimeOffset RegisteredAt);
