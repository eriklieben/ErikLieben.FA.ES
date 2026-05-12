-- ErikLieben.FA.ES Postgres provider — schema V1
-- Idempotent: safe to run multiple times. Bootstrapper wraps execution in pg_advisory_lock
-- so concurrent pods cannot race.

-- =====================================================================
-- Events table (declarative LIST partitioned by object_name).
-- Per-object-name partitions are created on-demand by the bootstrapper.
-- =====================================================================
CREATE TABLE IF NOT EXISTS faes_events (
    seq_id              bigserial   NOT NULL,
    object_name         text        NOT NULL,
    object_id           text        NOT NULL,
    stream_id           text        NOT NULL,
    version             integer     NOT NULL,
    event_type          text        NOT NULL,
    schema_version      integer,
    payload             jsonb       NOT NULL DEFAULT '{}'::jsonb,
    action_metadata     jsonb,
    metadata            jsonb,
    external_sequencer  text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (object_name, stream_id, version)
) PARTITION BY LIST (object_name);

-- Default partition for object_names without an explicit partition. New aggregate types
-- land here automatically; the bootstrapper can promote them to dedicated partitions later.
CREATE TABLE IF NOT EXISTS faes_events_default PARTITION OF faes_events DEFAULT;

CREATE INDEX IF NOT EXISTS ix_faes_events_seq_id ON faes_events (seq_id);
CREATE INDEX IF NOT EXISTS ix_faes_events_object ON faes_events (object_name, object_id);

-- =====================================================================
-- Object documents (one row per aggregate).
-- xmin serves as a system ETag for optimistic concurrency.
-- =====================================================================
CREATE TABLE IF NOT EXISTS faes_documents (
    object_name         text        NOT NULL,
    object_id           text        NOT NULL,
    active              jsonb       NOT NULL,
    terminated_streams  jsonb       NOT NULL DEFAULT '[]'::jsonb,
    document_tags       text[]      NOT NULL DEFAULT '{}',
    stream_tags         jsonb       NOT NULL DEFAULT '{}'::jsonb,
    schema_version      text,
    hash                text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (object_name, object_id)
);

-- Tag indexes — GIN for array membership and jsonb path queries.
CREATE INDEX IF NOT EXISTS ix_faes_documents_document_tags
    ON faes_documents USING GIN (document_tags);
CREATE INDEX IF NOT EXISTS ix_faes_documents_stream_tags
    ON faes_documents USING GIN (stream_tags jsonb_path_ops);

-- =====================================================================
-- Snapshots.
-- =====================================================================
CREATE TABLE IF NOT EXISTS faes_snapshots (
    stream_id   text        NOT NULL,
    version     integer     NOT NULL,
    name        text        NOT NULL DEFAULT '',
    data        jsonb       NOT NULL,
    data_type   text,
    size_bytes  bigint,
    created_at  timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (stream_id, version, name)
);

CREATE INDEX IF NOT EXISTS ix_faes_snapshots_stream ON faes_snapshots (stream_id, version DESC);

-- =====================================================================
-- Tags live on faes_documents:
--   document_tags text[]  → aggregate-level, flat. GIN indexed.
--   stream_tags   jsonb   → keyed by stream_identifier so a new stream
--                           does not inherit tags from a terminated one.
-- No separate tags table.
-- =====================================================================

-- =====================================================================
-- Projection progress / checkpoints (for polling subscriptions).
-- =====================================================================
CREATE TABLE IF NOT EXISTS faes_projection_progress (
    projection_name text PRIMARY KEY,
    last_seq_id     bigint NOT NULL DEFAULT 0,
    updated_at      timestamptz NOT NULL DEFAULT now()
);

-- =====================================================================
-- Projection lifecycle status (rebuild coordination, blue-green, recovery).
-- One row per (projection_name, object_id). xmin acts as the OCC token.
-- =====================================================================
CREATE TABLE IF NOT EXISTS faes_projection_status (
    projection_name      text        NOT NULL,
    object_id            text        NOT NULL,
    status               smallint    NOT NULL,    -- ProjectionStatus enum
    status_changed_at    timestamptz,
    schema_version       integer     NOT NULL DEFAULT 0,
    rebuild_info         jsonb,                   -- RebuildInfo record, null when not rebuilding
    active_rebuild_token jsonb,                   -- RebuildToken record, null when no active rebuild
    PRIMARY KEY (projection_name, object_id)
);

CREATE INDEX IF NOT EXISTS ix_faes_projection_status_status
    ON faes_projection_status (status);

-- =====================================================================
-- faes_append: atomic event append + document version bump.
--
-- Contract:
--   - Locks the document row with FOR UPDATE.
--   - Validates the caller's expected_version against the persisted CurrentStreamVersion.
--   - Inserts events from the supplied jsonb array.
--   - Bumps active.currentStreamVersion atomically.
--   - Returns (seq_id, version) per inserted event.
--
-- Errors:
--   SQLSTATE 40001 (serialization_failure) — concurrency conflict.
--   SQLSTATE P0002 (no_data_found)         — document does not exist (must be created first).
-- =====================================================================
CREATE OR REPLACE FUNCTION faes_append(
    p_object_name   text,
    p_object_id     text,
    p_stream_id     text,
    p_expected_ver  integer,
    p_events        jsonb
) RETURNS TABLE(out_seq_id bigint, out_version integer)
LANGUAGE plpgsql AS $$
DECLARE
    v_current     integer;
    v_event_count integer;
    v_new_version integer;
BEGIN
    SELECT (active->>'currentStreamVersion')::integer
      INTO v_current
      FROM faes_documents
     WHERE object_name = p_object_name
       AND object_id   = p_object_id
       FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'document not found: %/%', p_object_name, p_object_id
            USING ERRCODE = 'P0002';
    END IF;

    IF v_current IS DISTINCT FROM p_expected_ver THEN
        RAISE EXCEPTION 'concurrency conflict on stream %: expected version %, got %',
                        p_stream_id, p_expected_ver, v_current
            USING ERRCODE = '40001';
    END IF;

    v_event_count := jsonb_array_length(p_events);
    IF v_event_count = 0 THEN
        RETURN;
    END IF;

    RETURN QUERY
    INSERT INTO faes_events (
        object_name, object_id, stream_id, version,
        event_type, schema_version, payload, action_metadata, metadata,
        external_sequencer, created_at
    )
    SELECT
        p_object_name,
        p_object_id,
        p_stream_id,
        (e->>'eventVersion')::integer,
        e->>'eventType',
        NULLIF(e->>'schemaVersion', '')::integer,
        COALESCE(e->'payload', '{}'::jsonb),
        e->'actionMetadata',
        e->'metadata',
        e->>'externalSequencer',
        COALESCE((e->>'createdAt')::timestamptz, now())
    FROM jsonb_array_elements(p_events) AS e
    RETURNING seq_id, version;

    v_new_version := p_expected_ver + v_event_count;

    UPDATE faes_documents
       SET active     = jsonb_set(active, '{currentStreamVersion}', to_jsonb(v_new_version)),
           updated_at = now()
     WHERE object_name = p_object_name
       AND object_id   = p_object_id;
END;
$$;
