# IN4533 — Bài tập 02: EduHub mini (PostgreSQL & REST API)

## Thông tin sinh viên
- **Họ và tên**: Nguyễn Trung Kiên
- **Mã số sinh viên (MSSV)**: `<ĐIỀN_MSSV_CỦA_BẠN>`
- **Học phần**: Lập trình Back End (`IN4533`)
- **Giảng viên hướng dẫn**: ThS. Nguyễn Tuấn Kiệt
- **Học kỳ**: Học kỳ 1, năm học 2026 – 2027

---

## 1. Cấu trúc thư mục nộp bài

```text
IN4533-BT02/
├── sql/
│   ├── 01-schema.sql                  # Tạo 3 bảng members, courses, registrations + PK, FK, UNIQUE, CHECK, INDEX
│   ├── 02-seed.sql                    # Dữ liệu mẫu: 5 thành viên, 4 khoá học, 6 phiếu đăng ký (đủ 3 trạng thái)
│   └── 03-queries.sql                 # 3 câu truy vấn SQL (JOIN, LEFT JOIN + GROUP BY, LEFT JOIN + IS NULL)
├── docs/
│   ├── api.md                         # Bảng thiết kế 4 endpoint REST + JSON mẫu & Problem Details (RFC 9457)
│   ├── requests.md                    # Bộ lệnh kiểm thử curl / curl.exe kèm mã trạng thái dự kiến
│   ├── eduhub.postman_collection.json # Collection Postman kiểm thử tự động
│   └── images/                        # Ảnh chụp màn hình kết nối PostgreSQL & kết quả gọi thử API
├── src/
│   ├── EduHub.Api.csproj              # Dự án ASP.NET Core Minimal API (.NET 10 + Npgsql)
│   ├── Program.cs                     # Cài đặt 4 endpoint kết nối PostgreSQL bằng truy vấn tham số hoá
│   └── appsettings.json               # Cấu hình mẫu (không chứa mật khẩu thật)
├── compose.yaml                       # Tệp Docker Compose dựng nhanh PostgreSQL 17 (tuỳ chọn)
├── .env.example                       # Biến môi trường mẫu cho Docker Compose
├── .gitignore                         # Loại trừ bin/, obj/, .env, appsettings.Development.json
└── README.md                          # Tài liệu hướng dẫn cài đặt và chạy dự án
```

---

## 2. Cách cài đặt PostgreSQL và nạp cơ sở dữ liệu

### Cách đã cài đặt trên máy cá nhân: Cài trực tiếp PostgreSQL 18 trên Windows
1. Tải bộ cài đặt chính thức **PostgreSQL 18 (Windows x86-64 Installer)** từ trang chủ `postgresql.org` (đi kèm `psql`, `pgAdmin 4` và Command Line Tools tại `C:\Program Files\PostgreSQL\18\bin`).
2. Trong quá trình cài đặt:
   - Đặt tài khoản quản trị mặc định `postgres` và mật khẩu cục bộ.
   - Cấu hình cổng lắng nghe: **`1234`** (hoặc `5432` mặc định).
3. Mở **PowerShell** và kiểm tra kết nối bằng `psql`:

```powershell
# Thêm đường dẫn psql vào phiên làm việc hiện tại
$env:Path += ";C:\Program Files\PostgreSQL\18\bin"

# Kết nối vào PostgreSQL và tạo CSDL eduhub
psql -h localhost -p 1234 -U postgres -c "CREATE DATABASE eduhub;"

# Chạy lần lượt 3 tệp SQL từ đầu
psql -h localhost -p 1234 -U postgres -d eduhub -f sql/01-schema.sql
psql -h localhost -p 1234 -U postgres -d eduhub -f sql/02-seed.sql
psql -h localhost -p 1234 -U postgres -d eduhub -f sql/03-queries.sql
```

### Cách thay thế (dành cho máy có Docker Desktop)
Dự án có kèm sẵn tệp `compose.yaml` (PostgreSQL 17, mở cổng `5432` và gắn volume `eduhub-pgdata`):

```powershell
docker compose up -d
docker compose exec -T db psql -U eduhub -d eduhub < sql/01-schema.sql
docker compose exec -T db psql -U eduhub -d eduhub < sql/02-seed.sql
docker compose exec -T db psql -U eduhub -d eduhub < sql/03-queries.sql
```

---

## 3. Cách cấu hình và chạy ASP.NET Core Minimal API (`src/`)

### Yêu cầu môi trường
- **.NET 10 SDK** (`dotnet --version` kiểm tra).
- CSDL `eduhub` đã chạy `sql/01-schema.sql` và `sql/02-seed.sql`.

### Bước 1: Cấu hình chuỗi kết nối bằng `user-secrets` (Bảo mật — không ghi mật khẩu thật vào code)
Theo nguyên tắc quản lý secret ở Chương 2, mật khẩu thật được lưu ngoài thư mục dự án bằng `dotnet user-secrets`:

```powershell
cd src

# Nếu dùng PostgreSQL 18 cài trực tiếp trên máy (ví dụ cổng 1234):
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=1234;Database=eduhub;Username=postgres;Password=<MAT_KHAU_CUA_BAN>"

# Hoặc nếu dùng PostgreSQL qua Docker Compose (cổng 5432):
# dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=eduhub;Username=eduhub;Password=eduhub_dev"
```

### Bước 2: Khởi động API
```powershell
dotnet run
```
Ứng dụng Kestrel sẽ lắng nghe tại địa chỉ mặc định: **`http://localhost:5000`**.

---

## 4. Kiểm thử các Endpoint REST API

Mở một cửa sổ terminal mới và chạy các lệnh trong [`docs/requests.md`](docs/requests.md) (hoặc nhập tệp [`docs/eduhub.postman_collection.json`](docs/eduhub.postman_collection.json) vào **Postman**):

```powershell
# 1. Xem danh sách đăng ký của khoá học số 2 -> 200 OK
curl.exe -i http://localhost:5000/api/v1/courses/2/registrations

# 2. Tạo phiếu đăng ký mới cho thành viên 3 vào khoá học 2 -> 201 Created (id = 7)
curl.exe -i -X POST http://localhost:5000/api/v1/courses/2/registrations -H "Content-Type: application/json" -d '{\"memberId\": 3}'

# 3. Gửi lại lệnh tạo đăng ký trên để kiểm tra lỗi trùng -> 409 Conflict (application/problem+json)
curl.exe -i -X POST http://localhost:5000/api/v1/courses/2/registrations -H "Content-Type: application/json" -d '{\"memberId\": 3}'

# 4. Xác nhận phiếu đăng ký số 7 -> 200 OK (status = "confirmed")
curl.exe -i -X PATCH http://localhost:5000/api/v1/registrations/7 -H "Content-Type: application/json" -d '{\"status\": \"confirmed\"}'

# 5. Huỷ phiếu đăng ký số 7 -> 204 No Content (status chuyển sang "cancelled")
curl.exe -i -X DELETE http://localhost:5000/api/v1/registrations/7
```

---

## 5. Ảnh chụp màn hình kết quả thực hiện (`docs/images/`)

- **Kết nối PostgreSQL thành công (`psql` / `pgAdmin 4`)**: `docs/images/01-postgres-connect.png`
- **Kết quả gọi thử các endpoint Minimal API bằng `curl` / Postman**: `docs/images/02-api-curl-test.png`
