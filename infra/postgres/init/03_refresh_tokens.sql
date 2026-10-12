-- =============================================================================
-- MIGRATION: Bảng refresh_tokens (idempotent — an toàn khi chạy lại)
-- Bảng này đã được tích hợp vào 01_schema.sql.
-- File này giữ lại để hỗ trợ database đã dựng trước khi có bảng refresh_tokens.
-- =============================================================================

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                 UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash              VARCHAR(128) NOT NULL UNIQUE,
    expires_at              TIMESTAMPTZ NOT NULL,
    revoked_at              TIMESTAMPTZ,
    replaced_by_token_hash  VARCHAR(128),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user ON refresh_tokens (user_id);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_expires ON refresh_tokens (expires_at) WHERE revoked_at IS NULL;
