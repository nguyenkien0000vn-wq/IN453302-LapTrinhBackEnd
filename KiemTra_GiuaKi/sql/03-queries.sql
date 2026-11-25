-- ============================================================================
-- Tệp: sql/03-queries.sql
-- Học phần: IN4533 - Lập trình Back End (Bài tập 02: EduHub mini)
-- Mô tả: Ba truy vấn theo yêu cầu mục 1.4 (JOIN, LEFT JOIN + GROUP BY, IS NULL)
-- ============================================================================

-- (a) Họ tên thành viên và tên khoá học của các phiếu confirmed, sắp xếp theo họ tên
SELECT m.full_name,
       c.title AS course_title
FROM registrations r
JOIN members m ON m.id = r.member_id
JOIN courses c ON c.id = r.course_id
WHERE r.status = 'confirmed'
ORDER BY m.full_name ASC, c.title ASC;

-- (b) Số phiếu đăng ký của từng khoá học, kể cả khoá chưa có phiếu nào
-- Lý do dùng LEFT JOIN thay vì JOIN (INNER JOIN): INNER JOIN sẽ loại bỏ các khoá học chưa có ai đăng ký, còn LEFT JOIN giữ lại toàn bộ khoá học bên bảng courses và COUNT(r.id) trả về 0 cho khoá trống.
SELECT c.id,
       c.title,
       COUNT(r.id) AS so_dang_ky
FROM courses c
LEFT JOIN registrations r ON r.course_id = c.id
GROUP BY c.id, c.title
ORDER BY c.id ASC;

-- (c) Các thành viên chưa đăng ký khoá nào
SELECT m.id,
       m.full_name,
       m.email
FROM members m
LEFT JOIN registrations r ON r.member_id = m.id
WHERE r.id IS NULL
ORDER BY m.id ASC;
