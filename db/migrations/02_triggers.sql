-- Migration 02: email / file / schedule trigger watcher table.
-- Run this against an existing database that was created from 01_schema.sql.

CREATE TABLE IF NOT EXISTS automation_triggers (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    automation_id    uuid NOT NULL REFERENCES automations(id) ON DELETE CASCADE,
    type             text NOT NULL CHECK (type IN ('email_received','file_created','schedule')),
    is_active        bool NOT NULL DEFAULT true,
    conditions       jsonb NOT NULL DEFAULT '{}',
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_triggers_automation ON automation_triggers(automation_id);
CREATE INDEX IF NOT EXISTS idx_triggers_active ON automation_triggers(is_active) WHERE is_active = true;

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger WHERE tgname = 'trg_triggers_updated'
    ) THEN
        CREATE TRIGGER trg_triggers_updated
            BEFORE UPDATE ON automation_triggers
            FOR EACH ROW EXECUTE FUNCTION set_updated_at();
    END IF;
END $$;
