-- Admin role + first admin user (no reference rows — those are in 03_ReferenceData_PostgreSQL.sql).
-- Run after: 01_CreateSchema_PostgreSQL.sql and 03_ReferenceData_PostgreSQL.sql
--
-- Default login (change in production):
--   Email:    admin@local.dev
--   Password: password
-- BCrypt hash: password (BCrypt.Net-Next).

-- Safe to re-run: skips rows that already exist (unique on roles.name, users.user_id / users.email).
INSERT INTO roles (name, description, permissions, user_count) VALUES
    ('Admin', 'Full system access', 'all', 1)
ON CONFLICT (name) DO NOTHING;

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
    'admin@crm',
    '$2y$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi',
    'Admin',
    true,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
)
ON CONFLICT (user_id) DO NOTHING;

SELECT 'Admin seed script finished (skipped any existing Admin role / admin user)' AS status;
