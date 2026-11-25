-- ============================================================================
-- Tệp: sql/01-schema.sql
-- Học phần: IN4533 - Lập trình Back End (Bài tập 02: EduHub mini)
-- Mô tả: Định nghĩa cấu trúc 3 bảng (members, courses, registrations),
--        các ràng buộc toàn vẹn (PK, FK, UNIQUE, CHECK, DEFAULT) và chỉ mục.
-- ============================================================================

-- Xoá bảng theo thứ tự phụ thuộc khoá ngoại để có thể chạy lại sạch từ đầu
DROP TABLE IF EXISTS registrations;
DROP TABLE IF EXISTS courses;
DROP TABLE IF EXISTS members;

-- 1. Bảng thành viên (members)
CREATE TABLE members (
    id        INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    full_name VARCHAR(100) NOT NULL,
    email     VARCHAR(150) NOT NULL UNIQUE,
    joined_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- 2. Bảng khoá học (courses)
CREATE TABLE courses (
    id       INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title    VARCHAR(200) NOT NULL,
    capacity INT          NOT NULL CHECK (capacity > 0)
);

-- 3. Bảng phiếu đăng ký (registrations) - tách quan hệ n-n thành hai quan hệ 1-n
CREATE TABLE registrations (
    id            INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    member_id     INT         NOT NULL REFERENCES members(id),
    course_id     INT         NOT NULL REFERENCES courses(id),
    status        VARCHAR(20) NOT NULL DEFAULT 'pending'
                  CHECK (status IN ('pending', 'confirmed', 'cancelled')),
    registered_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (member_id, course_id)
);

-- PostgreSQL tự tạo index cho PRIMARY KEY và UNIQUE, nhưng KHÔNG tự tạo cho khoá ngoại (FK)
CREATE INDEX ix_reg_course ON registrations (course_id);
CREATE INDEX ix_reg_member ON registrations (member_id);
