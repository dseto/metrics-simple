-- Check auth_users schema
.tables
.schema auth_users
.schema auth_user_roles

-- Check data
SELECT id, username, display_name, email, substr(password_hash, 1, 20) as password_hash_prefix, is_active FROM auth_users;

SELECT * FROM auth_user_roles;
