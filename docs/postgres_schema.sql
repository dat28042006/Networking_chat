-- ChatApp PostgreSQL schema
-- PART 1: full schema for a fresh database.
-- PART 2: migration for the existing database built from the OLD plan (safe to re-run).
-- Run in pgAdmin (Query Tool) or:  psql -h localhost -U chatapp_user -d chatapp -f postgres_schema.sql
-- Time values are UTC milliseconds since 1970 (BIGINT) to match the wire protocol.

-- =====================================================================
-- PART 1: FULL SCHEMA
-- =====================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS users (
    id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    username    TEXT        NOT NULL,
    pass_hash   BYTEA       NOT NULL,
    salt        BYTEA       NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_users_username_lower ON users (lower(username));

CREATE TABLE IF NOT EXISTS friendships (
    id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,   -- = request_id in friend.respond
    user_a        BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    user_b        BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    requested_by  BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    status        TEXT   NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'accepted')),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (user_a < user_b),                                          -- each pair stored once
    UNIQUE (user_a, user_b)
);
CREATE INDEX IF NOT EXISTS ix_friendships_user_b ON friendships (user_b);

CREATE TABLE IF NOT EXISTS conversations (
    id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    type        TEXT   NOT NULL CHECK (type IN ('dm', 'group')),
    name        TEXT   NULL,
    owner_id    BIGINT NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS conversation_members (
    conv_id    BIGINT NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    user_id    BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role       TEXT   NOT NULL DEFAULT 'member' CHECK (role IN ('owner', 'member')),
    joined_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (conv_id, user_id)
);
CREATE INDEX IF NOT EXISTS ix_conversation_members_user ON conversation_members (user_id);

CREATE TABLE IF NOT EXISTS messages (
    id             BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    conv_id        BIGINT NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    sender_id      BIGINT NOT NULL REFERENCES users(id),
    client_msg_id  TEXT   NULL,
    body           TEXT   NOT NULL,
    ts_ms          BIGINT NOT NULL,
    CONSTRAINT ck_messages_body_len CHECK (char_length(body) <= 1000)
);
CREATE INDEX IF NOT EXISTS ix_messages_conv_id ON messages (conv_id, id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_messages_sender_client_msg ON messages (sender_id, client_msg_id);

CREATE TABLE IF NOT EXISTS posts (
    id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    author_id  BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    caption    TEXT   NOT NULL,
    ts_ms      BIGINT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_posts_ts ON posts (ts_ms DESC);

CREATE TABLE IF NOT EXISTS post_reactions (
    post_id  BIGINT NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    user_id  BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    kind     TEXT   NOT NULL DEFAULT 'like',
    PRIMARY KEY (post_id, user_id)
);

CREATE TABLE IF NOT EXISTS post_comments (
    id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    post_id    BIGINT NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    author_id  BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    body       TEXT   NOT NULL,
    ts_ms      BIGINT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_post_comments_post ON post_comments (post_id);

COMMIT;

-- =====================================================================
-- PART 2: MIGRATION FOR THE EXISTING (OLD PLAN) DATABASE
-- Assumes the old table names (users, friendships, conversations, conversation_members,
-- messages, posts, post_reactions, post_comments). If a statement fails, a name differs:
-- adjust that line only. Safe to run more than once.
-- =====================================================================
BEGIN;

-- 1) Server-side de-duplication of resent messages (protocol: client_msg_id)
ALTER TABLE messages ADD COLUMN IF NOT EXISTS client_msg_id TEXT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_messages_sender_client_msg ON messages (sender_id, client_msg_id);

-- 2) Case-insensitive unique username ('Admin' and 'admin' are the same user).
--    If this fails, two usernames differ only by case: rename one, then re-run.
CREATE UNIQUE INDEX IF NOT EXISTS ux_users_username_lower ON users (lower(username));

-- 3) Message length limit used by the GUI (NOT VALID = existing rows are not checked)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_messages_body_len') THEN
        ALTER TABLE messages ADD CONSTRAINT ck_messages_body_len CHECK (char_length(body) <= 1000) NOT VALID;
    END IF;
END $$;

-- 4) Speed-only indexes
CREATE INDEX IF NOT EXISTS ix_posts_ts ON posts (ts_ms DESC);
CREATE INDEX IF NOT EXISTS ix_conversation_members_user ON conversation_members (user_id);
CREATE INDEX IF NOT EXISTS ix_friendships_user_b ON friendships (user_b);
CREATE INDEX IF NOT EXISTS ix_post_comments_post ON post_comments (post_id);

COMMIT;
