-- pay_rs init (032/08, 033/01). Schema-qualified so a leftover public search_path
-- cannot create money tables in the wrong place. No public.checkouts. No backfill.
-- tenant_id is text (08 §11 / 07). proofs.bound_attempt_id is NULL on insert.

CREATE SCHEMA IF NOT EXISTS pay_rs;

-- ---------------------------------------------------------------------------
-- 1. Org / catalog / vault
-- ---------------------------------------------------------------------------

CREATE TABLE pay_rs.org_settings (
    tenant_id               text PRIMARY KEY,
    charges_paused          boolean NOT NULL DEFAULT false,
    currency                text NOT NULL DEFAULT 'MYR',
    one_webhook_ciphertext  text,
    wrap_key_id             text,
    updated_at              timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE pay_rs.gateway_credentials (
    tenant_id               text NOT NULL,
    rail                    text NOT NULL,
    ciphertext              bytea NOT NULL,
    last4                   text,
    webhook_ciphertext      bytea,
    public_merchant_id      text,
    environment             text NOT NULL,
    updated_at              timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT credentials_pk PRIMARY KEY (tenant_id, rail),
    CONSTRAINT gateway_credentials_environment_check
        CHECK (environment IN ('test', 'live', 'devnet', 'mainnet'))
);

CREATE TABLE pay_rs.products (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       text NOT NULL,
    name            text NOT NULL,
    description     text,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE pay_rs.prices (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id      uuid NOT NULL REFERENCES pay_rs.products (id),
    tenant_id       text NOT NULL,
    amount_minor    bigint NOT NULL CHECK (amount_minor > 0),
    currency        text NOT NULL,
    exponent        smallint NOT NULL CHECK (exponent BETWEEN 0 AND 18),
    interval        text NOT NULL,
    CONSTRAINT prices_interval_check CHECK (interval = 'one_off')
);

CREATE TABLE pay_rs.payment_links (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       text NOT NULL,
    public_token    text NOT NULL UNIQUE,
    rail            text NOT NULL,
    product_id      uuid REFERENCES pay_rs.products (id),
    amount_minor    bigint NOT NULL CHECK (amount_minor > 0),
    currency        text NOT NULL,
    exponent        smallint NOT NULL CHECK (exponent BETWEEN 0 AND 18),
    max_payers      integer,
    label           text,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- ---------------------------------------------------------------------------
-- 2. Money core
-- ---------------------------------------------------------------------------

CREATE TABLE pay_rs.payments (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           text NOT NULL,
    public_token        text NOT NULL,
    amount_minor        bigint NOT NULL CHECK (amount_minor > 0),
    currency            text NOT NULL,
    exponent            smallint NOT NULL CHECK (exponent BETWEEN 0 AND 18),
    status              text NOT NULL,
    exception           text NOT NULL DEFAULT 'none',
    intake              text NOT NULL DEFAULT 'open',
    intake_charge_id    uuid,
    intake_refund_id    uuid,
    return_kind         text,
    terminal_reason     text NOT NULL DEFAULT 'none',
    expires_at          timestamptz NOT NULL,
    monitoring_until    timestamptz NOT NULL,
    payment_link_id     uuid REFERENCES pay_rs.payment_links (id),
    slot_key            text,
    product_id          uuid REFERENCES pay_rs.products (id),
    success_url         text,
    cancel_url          text,
    payer_name          text,
    payer_email         text,
    version             bigint NOT NULL DEFAULT 1,
    created_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT payments_status_check
        CHECK (status IN ('open', 'processing', 'settled', 'failed', 'expired')),
    CONSTRAINT payments_exception_check
        CHECK (exception IN ('none', 'partial', 'over', 'late', 'over_capacity')),
    CONSTRAINT payments_intake_check
        CHECK (intake IN ('open', 'taken', 'returned')),
    CONSTRAINT payments_return_kind_check
        CHECK (return_kind IS NULL OR return_kind IN ('late', 'over_capacity')),
    CONSTRAINT payments_terminal_reason_check
        CHECK (terminal_reason IN (
            'none', 'psp_failed', 'watch_timeout', 'reservation_ttl', 'over_capacity'
        )),
    CONSTRAINT payments_intake_taken_charge
        CHECK ((intake = 'taken') = (intake_charge_id IS NOT NULL)),
    CONSTRAINT payments_intake_returned_refund
        CHECK ((intake = 'returned') = (intake_refund_id IS NOT NULL)),
    CONSTRAINT payments_intake_returned_kind
        CHECK ((intake = 'returned') = (return_kind IS NOT NULL))
);

CREATE UNIQUE INDEX payments_public_token ON pay_rs.payments (public_token);
CREATE UNIQUE INDEX payments_link_slot ON pay_rs.payments (payment_link_id, slot_key)
    WHERE slot_key IS NOT NULL;

CREATE TABLE pay_rs.attempts (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id      uuid NOT NULL REFERENCES pay_rs.payments (id),
    tenant_id       text NOT NULL,
    rail            text NOT NULL,
    method          text NOT NULL,
    amount_minor    bigint NOT NULL CHECK (amount_minor > 0),
    currency        text NOT NULL,
    exponent        smallint NOT NULL CHECK (exponent BETWEEN 0 AND 18),
    status          text NOT NULL,
    session_url     text,
    session_id      text,
    capture_id      text,
    network_id      text,
    fail_reason     text,
    version         bigint NOT NULL DEFAULT 1,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT attempts_status_check
        CHECK (status IN (
            'created', 'pending', 'session_live', 'failed', 'expired', 'settled'
        )),
    CONSTRAINT attempts_fail_reason_check
        CHECK (
            fail_reason IS NULL
            OR fail_reason IN (
                'psp_failed', 'watch_timeout', 'reservation_ttl', 'over_capacity'
            )
        )
);

CREATE UNIQUE INDEX attempts_one_live ON pay_rs.attempts (payment_id)
    WHERE status IN ('created', 'pending', 'session_live');
CREATE UNIQUE INDEX attempts_session_id ON pay_rs.attempts (tenant_id, rail, session_id)
    WHERE session_id IS NOT NULL;

CREATE TABLE pay_rs.settlements (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    attempt_id      uuid NOT NULL REFERENCES pay_rs.attempts (id),
    payment_id      uuid NOT NULL REFERENCES pay_rs.payments (id),
    tenant_id       text NOT NULL,
    amount_minor    bigint NOT NULL CHECK (amount_minor > 0),
    currency        text NOT NULL,
    exponent        smallint NOT NULL CHECK (exponent BETWEEN 0 AND 18),
    state           text NOT NULL,
    proof_kind      text NOT NULL,
    proof_id        text NOT NULL,
    archive_id      uuid,
    confirmations   integer,
    finalized       boolean,
    created_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT settlements_state_check
        CHECK (state IN ('seen', 'confirmed', 'reorged', 'ignored')),
    CONSTRAINT settlements_proof_kind_check
        CHECK (proof_kind IN ('psp_webhook', 'psp_sync', 'chain_tx'))
);

CREATE UNIQUE INDEX settlements_proof ON pay_rs.settlements (tenant_id, proof_kind, proof_id);

-- ---------------------------------------------------------------------------
-- 3. Watcher (proofs insert has no attempt_id)
-- ---------------------------------------------------------------------------

CREATE TABLE pay_rs.reservations (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       text NOT NULL,
    attempt_id      uuid NOT NULL REFERENCES pay_rs.attempts (id),
    chain           text NOT NULL,
    locator         text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX reservations_locator ON pay_rs.reservations (chain, locator);

CREATE TABLE pay_rs.proofs (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    chain               text NOT NULL,
    txid                text NOT NULL,
    raw                 jsonb,
    seen_at             timestamptz NOT NULL DEFAULT now(),
    claimed_at          timestamptz,
    bound_attempt_id    uuid REFERENCES pay_rs.attempts (id)
);

CREATE UNIQUE INDEX proofs_txid ON pay_rs.proofs (chain, txid);

-- ---------------------------------------------------------------------------
-- 4. Ledger, charges, documents, refunds
-- ---------------------------------------------------------------------------

CREATE TABLE pay_rs.charges (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       text NOT NULL,
    payment_id      uuid NOT NULL REFERENCES pay_rs.payments (id),
    attempt_id      uuid REFERENCES pay_rs.attempts (id),
    rail            text,
    capture_id      text,
    network_id      text,
    amount_minor    bigint NOT NULL CHECK (amount_minor > 0),
    currency        text NOT NULL,
    exponent        smallint NOT NULL CHECK (exponent BETWEEN 0 AND 18),
    status          text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT charges_status_check
        CHECK (status IN ('paid', 'partially_refunded', 'refunded'))
);

CREATE UNIQUE INDEX charges_payment ON pay_rs.charges (payment_id);

CREATE TABLE pay_rs.journal_entries (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       text NOT NULL,
    payment_id      uuid NOT NULL REFERENCES pay_rs.payments (id),
    currency        text NOT NULL,
    exponent        smallint NOT NULL CHECK (exponent BETWEEN 0 AND 18),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE pay_rs.journal_lines (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    entry_id        uuid NOT NULL REFERENCES pay_rs.journal_entries (id),
    account         text NOT NULL,
    dc              text NOT NULL,
    amount_minor    bigint NOT NULL CHECK (amount_minor > 0),
    CONSTRAINT journal_lines_account_check
        CHECK (account IN (
            'cash', 'pending_settlement', 'unearned', 'revenue', 'refunds_payable'
        )),
    CONSTRAINT journal_lines_dc_check CHECK (dc IN ('D', 'C'))
);

CREATE TABLE pay_rs.documents (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       text NOT NULL,
    payment_id      uuid NOT NULL REFERENCES pay_rs.payments (id),
    series          text NOT NULL,
    number          text NOT NULL,
    title           text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT documents_series_check CHECK (series IN ('RCPT', 'REF'))
);

CREATE UNIQUE INDEX documents_number ON pay_rs.documents (tenant_id, number);

CREATE TABLE pay_rs.document_sequences (
    tenant_id       text NOT NULL,
    series          text NOT NULL,
    year_myt        integer NOT NULL,
    last_n          integer NOT NULL,
    PRIMARY KEY (tenant_id, series, year_myt),
    CONSTRAINT document_sequences_series_check CHECK (series IN ('RCPT', 'REF'))
);

CREATE TABLE pay_rs.refunds (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           text NOT NULL,
    payment_id          uuid NOT NULL REFERENCES pay_rs.payments (id),
    charge_id           uuid REFERENCES pay_rs.charges (id),
    amount_minor        bigint NOT NULL CHECK (amount_minor > 0),
    currency            text NOT NULL,
    exponent            smallint NOT NULL CHECK (exponent BETWEEN 0 AND 18),
    status              text NOT NULL,
    rail                text NOT NULL,
    reason              text NOT NULL,
    provider_ref        text,
    idempotency_key     text,
    attempt_count       integer NOT NULL DEFAULT 0,
    next_attempt_at     timestamptz,
    last_error          text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT refunds_status_check
        CHECK (status IN ('pending', 'succeeded', 'failed', 'manual')),
    CONSTRAINT refunds_reason_check
        CHECK (reason IN ('merchant', 'late_pay', 'over_capacity', 'surplus')),
    CONSTRAINT refunds_charge_id_check CHECK (
        (reason = 'merchant' AND charge_id IS NOT NULL)
        OR (reason IN ('late_pay', 'over_capacity', 'surplus') AND charge_id IS NULL)
    )
);

CREATE UNIQUE INDEX refunds_late_one ON pay_rs.refunds (payment_id)
    WHERE reason IN ('late_pay', 'over_capacity');
CREATE UNIQUE INDEX refunds_idem ON pay_rs.refunds (tenant_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;

-- ---------------------------------------------------------------------------
-- 5. Idempotency, inbound, outbound, jobs, audit
-- ---------------------------------------------------------------------------

CREATE TABLE pay_rs.idempotency_keys (
    tenant_id       text NOT NULL,
    key             text NOT NULL,
    resource_kind   text NOT NULL,
    resource_id     uuid NOT NULL,
    request_hash    text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT idempotency_pk PRIMARY KEY (tenant_id, key)
);

CREATE TABLE pay_rs.raw_archives (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       text NOT NULL,
    rail            text NOT NULL,
    body            bytea NOT NULL,
    headers         jsonb,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE pay_rs.inbound_events (
    tenant_id           text NOT NULL,
    rail                text NOT NULL,
    proof_id            text NOT NULL,
    received_at         timestamptz NOT NULL DEFAULT now(),
    ignore_reason       text,
    raw_archive_id      uuid REFERENCES pay_rs.raw_archives (id),
    CONSTRAINT inbound_events_pk PRIMARY KEY (tenant_id, rail, proof_id)
);

CREATE TABLE pay_rs.org_webhook_endpoints (
    tenant_id           text PRIMARY KEY,
    url                 text NOT NULL,
    secret_ciphertext   bytea NOT NULL,
    secret_prefix       text,
    updated_at          timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE pay_rs.org_webhook_deliveries (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           text NOT NULL,
    event_id            text NOT NULL,
    event_type          text NOT NULL,
    payload_json        jsonb NOT NULL,
    status              text NOT NULL,
    attempt_count       integer NOT NULL DEFAULT 0,
    next_attempt_at     timestamptz NOT NULL,
    leased_until        timestamptz,
    last_http_status    integer,
    last_error          text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT org_webhook_deliveries_status_check
        CHECK (status IN ('pending', 'succeeded', 'failed', 'poison'))
);

CREATE UNIQUE INDEX deliveries_event ON pay_rs.org_webhook_deliveries (tenant_id, event_id);

CREATE TABLE pay_rs.jobs (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    kind                text NOT NULL,
    tenant_id           text,
    payload             jsonb NOT NULL,
    status              text NOT NULL,
    attempt_count       integer NOT NULL DEFAULT 0,
    next_attempt_at     timestamptz NOT NULL,
    leased_until        timestamptz,
    poison_at           timestamptz,
    last_error          text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT jobs_status_check
        CHECK (status IN ('pending', 'succeeded', 'failed', 'poison'))
);

CREATE TABLE pay_rs.audit_events (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   text NOT NULL,
    action      text NOT NULL,
    at          timestamptz NOT NULL DEFAULT now(),
    actor       text,
    detail      jsonb
);

CREATE TABLE pay_rs.one_webhook_events (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    delivery_id     text,
    event_type      text,
    received_at     timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX one_delivery ON pay_rs.one_webhook_events (delivery_id);

CREATE TABLE pay_rs.payers (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   text NOT NULL,
    email       text,
    name        text
);

-- ---------------------------------------------------------------------------
-- 6. Secondary indexes (not locks)
-- ---------------------------------------------------------------------------

CREATE INDEX payments_tenant_created ON pay_rs.payments (tenant_id, created_at DESC, id DESC);
CREATE INDEX payments_link ON pay_rs.payments (tenant_id, payment_link_id)
    WHERE payment_link_id IS NOT NULL;
CREATE INDEX payments_ttl ON pay_rs.payments (status, expires_at)
    WHERE status IN ('open', 'processing');
CREATE INDEX attempts_payment ON pay_rs.attempts (payment_id);
CREATE INDEX attempts_psync ON pay_rs.attempts (tenant_id, rail, status)
    WHERE status IN ('pending', 'session_live');
CREATE INDEX refunds_pending ON pay_rs.refunds (status, next_attempt_at)
    WHERE status = 'pending';
CREATE INDEX deliveries_lease ON pay_rs.org_webhook_deliveries (status, next_attempt_at);
CREATE INDEX jobs_lease ON pay_rs.jobs (status, next_attempt_at);
CREATE INDEX proofs_unclaimed ON pay_rs.proofs (claimed_at) WHERE claimed_at IS NULL;
CREATE INDEX inbound_received ON pay_rs.inbound_events (received_at);
