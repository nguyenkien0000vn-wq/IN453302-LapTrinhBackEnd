# Thiết kế REST API — Phân hệ Đăng ký khoá học (`registrations`)

Tài liệu thiết kế RESTful API cho hệ thống **EduHub mini** dựa trên các nguyên tắc ở **Chương 2 (RESTful API & JSON API)**:
- **URI dùng danh từ số nhiều, chữ thường**, phân cấp tài nguyên rõ ràng, **không chứa động từ**.
- **Đánh phiên bản trong URI**: tiền tố `/api/v1`.
- **Ánh xạ đúng HTTP Method sang thao tác CRUD**: `GET` (đọc, an toàn), `POST` (tạo mới), `PATCH` (cập nhật một phần — đổi trạng thái), `DELETE` (huỷ/xoá, lặp lại được).
- **Mã trạng thái HTTP đúng ngữ nghĩa** (`200`, `201`, `204`, `400`, `404`, `409`, `422`, `500`) và phản hồi lỗi theo chuẩn **Problem Details — RFC 9457** (`application/problem+json`).

---

## 2.1. Bảng thiết kế bốn chức năng của `registrations`

| STT | Chức năng | Method | URI | Mã thành công | Các mã lỗi có thể gặp |
| :-: | :--- | :---: | :--- | :---: | :--- |
| 1 | **Xem danh sách đăng ký của một khoá học** *(hỗ trợ lọc tuỳ chọn `?status=pending\|confirmed\|cancelled`)* | `GET` | `/api/v1/courses/{courseId}/registrations` | `200 OK` | `400 Bad Request` (tham số `courseId` hoặc `status` sai định dạng)<br>`404 Not Found` (khoá học `courseId` không tồn tại)<br>`500 Internal Server Error` (lỗi kết nối/truy vấn CSDL) |
| 2 | **Tạo đăng ký mới vào khoá học** | `POST` | `/api/v1/courses/{courseId}/registrations` | `201 Created`<br>*(kèm header `Location`)* | `400 Bad Request` / `422 Unprocessable Entity` (JSON sai cú pháp hoặc `memberId` không hợp lệ)<br>`404 Not Found` (khoá học hoặc thành viên không tồn tại)<br>`409 Conflict` (thành viên đã đăng ký khoá học này hoặc khoá học đã đủ chỗ)<br>`500 Internal Server Error` (lỗi phía server) |
| 3 | **Xác nhận đăng ký** *(cập nhật trường `status` sang `"confirmed"`)* | `PATCH` | `/api/v1/registrations/{id}` | `200 OK` | `400 Bad Request` / `422 Unprocessable Entity` (JSON sai cú pháp hoặc giá trị `status` ngoài 3 trạng thái cho phép)<br>`404 Not Found` (phiếu đăng ký `id` không tồn tại)<br>`409 Conflict` (phiếu đã ở trạng thái `cancelled`, không thể xác nhận)<br>`500 Internal Server Error` (lỗi phía server) |
| 4 | **Huỷ đăng ký** *(chuyển trạng thái phiếu sang `"cancelled"`)* | `DELETE` | `/api/v1/registrations/{id}` | `204 No Content` | `400 Bad Request` (`id` không hợp lệ)<br>`404 Not Found` (phiếu đăng ký `id` không tồn tại)<br>`500 Internal Server Error` (lỗi phía server) |

> **Giải thích lựa chọn URI & Method:**
> - Hai chức năng **Xem danh sách** và **Tạo đăng ký** gắn trực tiếp với một khoá học cụ thể nên đặt dưới tài nguyên cha `/api/v1/courses/{courseId}/registrations`. Khi tạo mới, client chỉ cần gửi `{ "memberId": 3 }` trong body.
> - Hai chức năng **Xác nhận đăng ký** và **Huỷ đăng ký** thao tác trên một phiếu đăng ký đã có định danh duy nhất `id`, nên định danh trực tiếp qua `/api/v1/registrations/{id}`:
>   - Không dùng URI chứa động từ như `/confirm` hay `/cancel`.
>   - Dùng `PATCH /api/v1/registrations/{id}` với body `{ "status": "confirmed" }` vì chỉ sửa một phần thuộc tính `status` của phiếu đăng ký.
>   - Dùng `DELETE /api/v1/registrations/{id}` trả về `204 No Content` (cài đặt cập nhật mềm `status = 'cancelled'` để giữ lịch sử đăng ký đúng theo bối cảnh bài toán).

---

## 2.2. JSON Request và Response mẫu cho chức năng Tạo đăng ký

### a) HTTP Request (Tạo đăng ký cho thành viên `3` vào khoá học `2`)

```http
POST /api/v1/courses/2/registrations HTTP/1.1
Host: localhost:5000
Content-Type: application/json
Accept: application/json

{
  "memberId": 3
}
```

### b) HTTP Response khi thành công (`201 Created`)

```http
HTTP/1.1 201 Created
Location: /api/v1/registrations/7
Content-Type: application/json; charset=utf-8

{
  "id": 7,
  "memberId": 3,
  "courseId": 2,
  "status": "pending",
  "registeredAt": "2026-11-25T11:30:00Z"
}
```

### c) HTTP Response khi lỗi trùng đăng ký (`409 Conflict` — Chuẩn Problem Details RFC 9457)

Khi thành viên `3` gửi lại yêu cầu đăng ký khoá học `2` (vi phạm ràng buộc `UNIQUE (member_id, course_id)`), API trả về mã `409 Conflict` cùng `Content-Type: application/problem+json`:

```http
HTTP/1.1 409 Conflict
Content-Type: application/problem+json; charset=utf-8

{
  "type": "https://eduhub.example/errors/already-registered",
  "title": "Đã đăng ký khoá học này",
  "status": 409,
  "detail": "Thành viên 3 đã có phiếu đăng ký ở khoá học 2.",
  "instance": "/api/v1/courses/2/registrations"
}
```

---

## Phụ lục: Mẫu JSON cho các chức năng còn lại

### 1. Xem danh sách đăng ký của khoá học (`GET /api/v1/courses/2/registrations`)
```json
[
  {
    "id": 2,
    "memberId": 1,
    "memberName": "Nguyễn Văn An",
    "memberEmail": "an.nv@dthu.edu.vn",
    "courseId": 2,
    "status": "confirmed",
    "registeredAt": "2026-11-25T11:00:00Z"
  },
  {
    "id": 5,
    "memberId": 2,
    "memberName": "Trần Thị Bình",
    "memberEmail": "binh.tt@dthu.edu.vn",
    "courseId": 2,
    "status": "pending",
    "registeredAt": "2026-11-25T11:00:00Z"
  }
]
```

### 2. Xác nhận đăng ký (`PATCH /api/v1/registrations/7`)
- **Request body**:
```json
{
  "status": "confirmed"
}
```
- **Response (`200 OK`)**:
```json
{
  "id": 7,
  "memberId": 3,
  "courseId": 2,
  "status": "confirmed",
  "registeredAt": "2026-11-25T11:30:00Z"
}
```

### 3. Huỷ đăng ký (`DELETE /api/v1/registrations/7`)
- **Response (`204 No Content`)**: Không có phần thân (empty body).
