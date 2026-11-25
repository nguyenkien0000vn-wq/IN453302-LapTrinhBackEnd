-- ============================================================================
-- Tệp: sql/02-seed.sql
-- Học phần: IN4533 - Lập trình Back End (Bài tập 02: EduHub mini)
-- Mô tả: Dữ liệu mẫu gồm 5 thành viên, 4 khoá học và 6 phiếu đăng ký
--        (đủ 3 trạng thái pending/confirmed/cancelled; có thành viên chưa
--         đăng ký khoá nào và có khoá học chưa có phiếu đăng ký nào).
-- ============================================================================

-- 1. Thêm 5 thành viên (id tự tăng từ 1 đến 5; thành viên số 5 chưa đăng ký khoá nào)
INSERT INTO members (full_name, email, joined_at) VALUES
    ('Nguyễn Văn An',  'an.nv@dthu.edu.vn',   '2026-09-01 08:00:00+07'),
    ('Trần Thị Bình',  'binh.tt@dthu.edu.vn', '2026-09-03 09:15:00+07'),
    ('Lê Hoàng Châu',  'chau.lh@dthu.edu.vn', '2026-09-05 14:30:00+07'),
    ('Phạm Minh Dũng', 'dung.pm@dthu.edu.vn', '2026-09-10 10:00:00+07'),
    ('Võ Ngọc Hân',    'han.vn@dthu.edu.vn',  '2026-09-12 16:45:00+07');

-- 2. Thêm 4 khoá học (id tự tăng từ 1 đến 4; khoá học số 4 chưa có phiếu đăng ký nào)
INSERT INTO courses (title, capacity) VALUES
    ('Lập trình Web cơ bản',               30),
    ('Lập trình Back End với .NET',        35),
    ('Hệ quản trị Cơ sở dữ liệu PostgreSQL', 25),
    ('Triển khai ứng dụng với Docker',     20);

-- 3. Thêm 6 phiếu đăng ký (id tự tăng từ 1 đến 6, đủ 3 trạng thái)
-- Nhóm có chỉ định rõ trạng thái ('confirmed' và 'cancelled'):
INSERT INTO registrations (member_id, course_id, status) VALUES
    (1, 1, 'confirmed'),  -- id = 1: Nguyễn Văn An -> Lập trình Web cơ bản
    (1, 2, 'confirmed'),  -- id = 2: Nguyễn Văn An -> Lập trình Back End với .NET
    (2, 3, 'confirmed'),  -- id = 3: Trần Thị Bình -> Hệ quản trị CSDL PostgreSQL
    (3, 1, 'cancelled');  -- id = 4: Lê Hoàng Châu -> Lập trình Web cơ bản

-- Nhóm sử dụng giá trị mặc định DEFAULT 'pending' của cột status:
INSERT INTO registrations (member_id, course_id) VALUES
    (2, 2),               -- id = 5: Trần Thị Bình -> Lập trình Back End với .NET (pending)
    (4, 3);               -- id = 6: Phạm Minh Dũng -> Hệ quản trị CSDL PostgreSQL (pending)
