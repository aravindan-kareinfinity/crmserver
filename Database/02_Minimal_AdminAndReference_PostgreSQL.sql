-- Admin role + first admin user (no reference rows — those are in 03_ReferenceData_PostgreSQL.sql).
-- Run after: 01_CreateSchema_PostgreSQL.sql and 03_ReferenceData_PostgreSQL.sql
--
-- Default login (change in production):
--   Email:    admin@local.dev
--   Password: Admin@123
-- BCrypt hash: Admin@123 (BCrypt.Net-Next).

INSERT INTO roles (name, description, permissions, user_count) VALUES
('Admin', 'Full system access', 'all', 1);

INSERT INTO users (
    user_id,
    first_name,
    last_name,
    email,
    password_hash,
    role,
    is_active,
    last_login,
    created_at,
    modified_at
) VALUES (
    'admin',
    'Admin',
    'User',
    'admin@local.dev',
    '$2a$11$oV7so.scIhBs9/6E3K9hfeaObJpNELnsxvjNHAnEJlVk1pSp8//QC',
    'Admin',
    true,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
);

SELECT 'Admin role + user inserted' AS status;
