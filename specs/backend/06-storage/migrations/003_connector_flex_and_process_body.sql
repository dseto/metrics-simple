-- 003_connector_flex_and_process_body.sql
-- Data: 2026-01-05
-- Objetivo:
-- - Remover authRef do connector
-- - Adicionar authType + config/defaults
-- - Introduzir tabela connector_secrets (multi-secret)
-- - Migrar legacy connector_tokens -> connector_secrets (BEARER_TOKEN)
-- - Adicionar bodyText/contentType em process_versions
-- Observação: SQLite não suporta DROP COLUMN: rebuild da tabela connectors.

BEGIN TRANSACTION;

-- 1) Rebuild connectors
CREATE TABLE IF NOT EXISTS connectors_new (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  baseUrl TEXT NOT NULL,
  authType TEXT NOT NULL DEFAULT 'NONE',
  authConfigJson TEXT NULL,
  requestDefaultsJson TEXT NULL,
  timeoutSeconds INTEGER NOT NULL,
  enabled INTEGER NOT NULL DEFAULT 1,
  createdAt TEXT NULL,
  updatedAt TEXT NULL
);

INSERT INTO connectors_new (
  id, name, baseUrl, authType, authConfigJson, requestDefaultsJson,
  timeoutSeconds, enabled, createdAt, updatedAt
)
SELECT
  id, name, baseUrl,
  'NONE' AS authType,
  NULL  AS authConfigJson,
  NULL  AS requestDefaultsJson,
  timeoutSeconds, enabled, createdAt, updatedAt
FROM connectors;

DROP TABLE connectors;
ALTER TABLE connectors_new RENAME TO connectors;

-- 2) Secrets table
CREATE TABLE IF NOT EXISTS connector_secrets (
  connectorId TEXT NOT NULL,
  secretKind TEXT NOT NULL, -- BEARER_TOKEN|API_KEY_VALUE|BASIC_PASSWORD
  encVersion INTEGER NOT NULL,
  encAlg TEXT NOT NULL,
  encNonce TEXT NOT NULL,
  encCiphertext TEXT NOT NULL,
  createdAt TEXT NULL,
  updatedAt TEXT NULL,
  PRIMARY KEY (connectorId, secretKind),
  FOREIGN KEY (connectorId) REFERENCES connectors(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_connector_secrets_connectorId ON connector_secrets(connectorId);

-- 3) Migrate legacy bearer token if connector_tokens exists
-- (Se a tabela não existir, a aplicação deve ignorar este passo.)
INSERT OR IGNORE INTO connector_secrets (
  connectorId, secretKind, encVersion, encAlg, encNonce, encCiphertext, createdAt, updatedAt
)
SELECT
  connectorId,
  'BEARER_TOKEN' AS secretKind,
  encVersion, encAlg, encNonce, encCiphertext, createdAt, updatedAt
FROM connector_tokens;

-- 4) Process versions: body + contentType
-- Nota: se já existir, o migrator deve checar PRAGMA table_info antes de executar.
ALTER TABLE process_versions ADD COLUMN bodyText TEXT NULL;
ALTER TABLE process_versions ADD COLUMN contentType TEXT NULL;

COMMIT;
