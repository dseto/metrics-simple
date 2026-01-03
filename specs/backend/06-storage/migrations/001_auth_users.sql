-- 001_auth_users.sql (delta: auth local em SQLite)
CREATE TABLE IF NOT EXISTS auth_users (
  id                TEXT PRIMARY KEY,
  username          TEXT NOT NULL UNIQUE,
  display_name      TEXT NULL,
  email             TEXT NULL,
  password_hash     TEXT NOT NULL,
  is_active         INTEGER NOT NULL DEFAULT 1,
  failed_attempts   INTEGER NOT NULL DEFAULT 0,
  lockout_until_utc TEXT NULL,
  created_at_utc    TEXT NOT NULL,
  updated_at_utc    TEXT NOT NULL,
  last_login_utc    TEXT NULL
);

CREATE INDEX IF NOT EXISTS idx_auth_users_username ON auth_users(username);
CREATE INDEX IF NOT EXISTS idx_auth_users_active   ON auth_users(is_active);

CREATE TABLE IF NOT EXISTS auth_user_roles (
  user_id   TEXT NOT NULL,
  role      TEXT NOT NULL,
  PRIMARY KEY (user_id, role),
  FOREIGN KEY (user_id) REFERENCES auth_users(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_auth_user_roles_role ON auth_user_roles(role);
