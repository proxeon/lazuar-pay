-- Minimal quoted public schema matching EF PascalCase (PayDbContext). Tests only.

CREATE TABLE IF NOT EXISTS public.org_settings (
    "OrgId" text PRIMARY KEY,
    "Currency" text NOT NULL DEFAULT 'MYR',
    "ChargesPaused" boolean NOT NULL DEFAULT false,
    "SstRegistered" boolean,
    "ActiveProvider" text,
    "OneWebhookCiphertext" text
);

CREATE TABLE IF NOT EXISTS public.gateway_credentials (
    "OrgId" text NOT NULL,
    "Provider" text NOT NULL,
    "Ciphertext" text NOT NULL,
    "Last4" text,
    "WebhookCiphertext" text,
    "PublicMerchantId" text,
    "Environment" text NOT NULL DEFAULT 'test',
    "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY ("OrgId", "Provider")
);

CREATE TABLE IF NOT EXISTS public.products (
    "Id" text PRIMARY KEY,
    "OrgId" text NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "CreatedAt" timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.prices (
    "Id" text PRIMARY KEY,
    "ProductId" text NOT NULL,
    "Currency" text NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Interval" text NOT NULL
);

CREATE TABLE IF NOT EXISTS public.payment_links (
    "Id" text PRIMARY KEY,
    "OrgId" text NOT NULL,
    "PublicToken" text NOT NULL UNIQUE,
    "Provider" text NOT NULL,
    "ProductId" text,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" text NOT NULL,
    "MaxPayers" integer,
    "Label" text,
    "CreatedAt" timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.document_sequences (
    "OrgId" text NOT NULL,
    "Series" text NOT NULL,
    "YearMyt" integer NOT NULL,
    "LastN" integer NOT NULL,
    PRIMARY KEY ("OrgId", "Series", "YearMyt")
);

CREATE TABLE IF NOT EXISTS public.org_webhook_endpoints (
    "OrgId" text PRIMARY KEY,
    "Url" text NOT NULL,
    "SecretCiphertext" text NOT NULL,
    "SecretPrefix" text,
    "UpdatedAt" timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.checkouts (
    "Id" text PRIMARY KEY,
    "OrgId" text NOT NULL,
    "PublicToken" text NOT NULL UNIQUE,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" text NOT NULL,
    "Status" text NOT NULL,
    "Interval" text NOT NULL DEFAULT 'one_off',
    "SuccessUrl" text,
    "CancelUrl" text,
    "PspRedirectUrl" text,
    "PayerName" text,
    "PayerEmail" text,
    "ProductId" text,
    "Provider" text,
    "ProviderSessionId" text,
    "PaymentLinkId" text,
    "SlotKey" text,
    "CreatedAt" timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.charges (
    "Id" text PRIMARY KEY,
    "OrgId" text NOT NULL,
    "CheckoutId" text NOT NULL,
    "Provider" text NOT NULL,
    "ProviderRef" text,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" text NOT NULL,
    "Status" text NOT NULL
);

CREATE TABLE IF NOT EXISTS public.refunds (
    "Id" text PRIMARY KEY,
    "OrgId" text NOT NULL,
    "CheckoutId" text NOT NULL,
    "ChargeId" text,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" text NOT NULL,
    "Status" text NOT NULL,
    "Provider" text NOT NULL,
    "ProviderRef" text,
    "Reason" text NOT NULL DEFAULT 'merchant',
    "IdempotencyKey" text,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "AttemptCount" integer NOT NULL DEFAULT 0,
    "NextAttemptAt" timestamptz,
    "LastError" text
);

CREATE TABLE IF NOT EXISTS public.journal_entries (
    "Id" text PRIMARY KEY,
    "OrgId" text NOT NULL,
    "CheckoutId" text NOT NULL,
    "Currency" text NOT NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.journal_lines (
    "Id" text PRIMARY KEY,
    "EntryId" text NOT NULL,
    "Account" text NOT NULL,
    "Dc" text NOT NULL,
    "Amount" numeric(18,2) NOT NULL
);

CREATE TABLE IF NOT EXISTS public.documents (
    "Id" text PRIMARY KEY,
    "OrgId" text NOT NULL,
    "CheckoutId" text NOT NULL,
    "Number" text,
    "Title" text NOT NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now()
);
