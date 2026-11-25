# Kịch bản kiểm thử API bằng `curl` (`docs/requests.md`)

Giả sử ứng dụng **EduHub Minimal API** đang chạy tại `http://localhost:5000` và CSDL PostgreSQL vừa được nạp dữ liệu mẫu từ `sql/01-schema.sql` và `sql/02-seed.sql` (đang có 6 phiếu đăng ký `id` từ `1` đến `6`).

> **Lưu ý trên Windows PowerShell** (theo Chương 2 - Phần A): Trong PowerShell, `curl` là bí danh của `Invoke-WebRequest`, vì vậy hãy gõ **`curl.exe`** thay cho `curl`! Bên dưới trình bày lệnh dùng được trực tiếp trên cả **PowerShell** (`curl.exe`) và **Bash / Git Bash / Terminal**.

---

## 1. Bốn lệnh `curl` chính tương ứng với mục 2.1

### Lệnh 1 — Xem danh sách đăng ký của khoá học số 2 (`GET`)
- **Mã trạng thái dự kiến**: `200 OK`
- **Bash / Git Bash**:
```bash
curl -i http://localhost:5000/api/v1/courses/2/registrations
```
- **Windows PowerShell**:
```powershell
curl.exe -i http://localhost:5000/api/v1/courses/2/registrations
```

---

### Lệnh 2 — Tạo đăng ký mới cho thành viên số 3 vào khoá học số 2 (`POST`)
- **Mã trạng thái dự kiến**: `201 Created` *(kèm header `Location: /api/v1/registrations/7` và JSON phiếu mới có `"status": "pending"`)*
- **Bash / Git Bash**:
```bash
curl -i -X POST http://localhost:5000/api/v1/courses/2/registrations \
  -H "Content-Type: application/json" \
  -d '{"memberId": 3}'
```
- **Windows PowerShell**:
```powershell
curl.exe -i -X POST http://localhost:5000/api/v1/courses/2/registrations -H "Content-Type: application/json" -d '{\"memberId\": 3}'
```

---

### Lệnh 3 — Xác nhận phiếu đăng ký số 7 (`PATCH`)
- **Mã trạng thái dự kiến**: `200 OK` *(JSON trả về có `"status": "confirmed"`)*
- **Bash / Git Bash**:
```bash
curl -i -X PATCH http://localhost:5000/api/v1/registrations/7 \
  -H "Content-Type: application/json" \
  -d '{"status": "confirmed"}'
```
- **Windows PowerShell**:
```powershell
curl.exe -i -X PATCH http://localhost:5000/api/v1/registrations/7 -H "Content-Type: application/json" -d '{\"status\": \"confirmed\"}'
```

---

### Lệnh 4 — Huỷ phiếu đăng ký số 7 (`DELETE`)
- **Mã trạng thái dự kiến**: `204 No Content` *(không có body trả về; trạng thái phiếu trong CSDL chuyển sang `cancelled`)*
- **Bash / Git Bash**:
```bash
curl -i -X DELETE http://localhost:5000/api/v1/registrations/7
```
- **Windows PowerShell**:
```powershell
curl.exe -i -X DELETE http://localhost:5000/api/v1/registrations/7
```

---

## 2. Các lệnh kiểm thử tình huống lỗi (Problem Details — RFC 9457)

### Lệnh 5 — Gửi trùng đăng ký để kiểm tra lỗi `409 Conflict`
- **Mã trạng thái dự kiến**: `409 Conflict` (`Content-Type: application/problem+json`)
```powershell
curl.exe -i -X POST http://localhost:5000/api/v1/courses/2/registrations -H "Content-Type: application/json" -d '{\"memberId\": 3}'
```

### Lệnh 6 — Xem đăng ký của khoá học không tồn tại (`404 Not Found`)
- **Mã trạng thái dự kiến**: `404 Not Found` (`Content-Type: application/problem+json`)
```powershell
curl.exe -i http://localhost:5000/api/v1/courses/999/registrations
```

### Lệnh 7 — Huỷ phiếu đăng ký không tồn tại (`404 Not Found`)
- **Mã trạng thái dự kiến**: `404 Not Found` (`Content-Type: application/problem+json`)
```powershell
curl.exe -i -X DELETE http://localhost:5000/api/v1/registrations/999
```
