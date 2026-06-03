-- Amcor AutoFlow - initial schema
-- Relational for stable entities; JSONB for the flexible IR.
-- Enums use text + CHECK (clean EF mapping, no Npgsql enum registration).

CREATE EXTENSION IF NOT EXISTS pgcrypto;  -- gen_random_uuid()

-- ---------------------------------------------------------------------------
CREATE TABLE users (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email        text NOT NULL UNIQUE,
    display_name text,
    created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE automations (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name               text NOT NULL,
    description        text,
    owner_id           uuid NOT NULL REFERENCES users(id),
    current_version_id uuid,
    created_at         timestamptz NOT NULL DEFAULT now(),
    updated_at         timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE automation_versions (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    automation_id  uuid NOT NULL REFERENCES automations(id) ON DELETE CASCADE,
    version_number int  NOT NULL,
    definition     jsonb NOT NULL,
    status         text NOT NULL DEFAULT 'draft'
                   CHECK (status IN ('draft','needs_clarification','active','archived')),
    created_by     uuid NOT NULL REFERENCES users(id),
    created_at     timestamptz NOT NULL DEFAULT now(),
    UNIQUE (automation_id, version_number)
);

ALTER TABLE automations
    ADD CONSTRAINT fk_automations_current_version
    FOREIGN KEY (current_version_id) REFERENCES automation_versions(id);

CREATE TABLE automation_assets (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    automation_version_id uuid NOT NULL REFERENCES automation_versions(id) ON DELETE CASCADE,
    step_id               text,
    object_key            text NOT NULL,
    content_type          text NOT NULL DEFAULT 'image/png',
    created_at            timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE agents (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    machine_name text NOT NULL,
    token_hash   text NOT NULL,
    app_version  text,
    last_seen_at timestamptz,
    created_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (user_id, machine_name)
);

CREATE TABLE automation_runs (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    automation_version_id uuid NOT NULL REFERENCES automation_versions(id),
    agent_id              uuid REFERENCES agents(id),
    trigger_type          text NOT NULL,
    status                text NOT NULL DEFAULT 'pending'
                          CHECK (status IN ('pending','dispatched','running','succeeded','failed','cancelled')),
    error                 text,
    started_at            timestamptz,
    finished_at           timestamptz,
    created_at            timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE run_step_logs (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id                uuid NOT NULL REFERENCES automation_runs(id) ON DELETE CASCADE,
    step_id               text NOT NULL,
    step_order            int  NOT NULL,
    status                text NOT NULL DEFAULT 'pending'
                          CHECK (status IN ('pending','dispatched','running','succeeded','failed','cancelled')),
    message               text,
    screenshot_object_key text,
    started_at            timestamptz,
    finished_at           timestamptz
);

-- Indexes
CREATE INDEX idx_automations_owner       ON automations(owner_id);
CREATE INDEX idx_versions_automation     ON automation_versions(automation_id);
CREATE INDEX idx_versions_status         ON automation_versions(status);
CREATE INDEX idx_versions_definition_gin ON automation_versions USING gin (definition jsonb_path_ops);
CREATE INDEX idx_assets_version          ON automation_assets(automation_version_id);
CREATE INDEX idx_agents_user             ON agents(user_id);
CREATE INDEX idx_runs_version            ON automation_runs(automation_version_id);
CREATE INDEX idx_runs_status             ON automation_runs(status);
CREATE INDEX idx_runs_agent              ON automation_runs(agent_id);
CREATE INDEX idx_step_logs_run           ON run_step_logs(run_id);

-- updated_at maintenance
CREATE OR REPLACE FUNCTION set_updated_at() RETURNS trigger AS $func$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$func$ LANGUAGE plpgsql;

CREATE TRIGGER trg_automations_updated
    BEFORE UPDATE ON automations
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ---------------------------------------------------------------------------
-- Trigger watchers: one row per active non-manual automation trigger.
-- Conditions is a flat JSONB key-value map matching read_email param names.
CREATE TABLE automation_triggers (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    automation_id    uuid NOT NULL REFERENCES automations(id) ON DELETE CASCADE,
    type             text NOT NULL CHECK (type IN ('email_received','file_created','schedule')),
    is_active        bool NOT NULL DEFAULT true,
    conditions       jsonb NOT NULL DEFAULT '{}',
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_triggers_automation ON automation_triggers(automation_id);
CREATE INDEX idx_triggers_active ON automation_triggers(is_active) WHERE is_active = true;

CREATE TRIGGER trg_triggers_updated
    BEFORE UPDATE ON automation_triggers
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Seed demo user (MVP, before SSO).
INSERT INTO users (id, email, display_name)
VALUES ('00000000-0000-0000-0000-000000000001', 'demo@amcor.com', 'Demo User');
