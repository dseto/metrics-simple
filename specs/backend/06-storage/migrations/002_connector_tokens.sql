CREATE TABLE IF NOT EXISTS connector_tokens (
  connectorId    TEXT PRIMARY KEY REFERENCES connectors(id) ON DELETE CASCADE,
  encVersion     INTEGER NOT NULL,
  encAlg         TEXT    NOT NULL,
  encNonce       TEXT    NOT NULL,
  encCiphertext  TEXT    NOT NULL,
  createdAt      TEXT    NOT NULL,
  updatedAt      TEXT    NOT NULL
);
