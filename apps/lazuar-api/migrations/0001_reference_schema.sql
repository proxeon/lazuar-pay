--
-- PostgreSQL database dump
--


-- Dumped from database version 16.15
-- Dumped by pg_dump version 16.15

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


--
-- Name: audit_events; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.audit_events (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "Action" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: charges; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.charges (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "CheckoutId" text NOT NULL,
    "Provider" text NOT NULL,
    "ProviderRef" text,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" text NOT NULL,
    "Status" text NOT NULL
);


--
-- Name: checkouts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.checkouts (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "PublicToken" text NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" text NOT NULL,
    "Status" text NOT NULL,
    "Interval" text NOT NULL,
    "SuccessUrl" text,
    "CancelUrl" text,
    "PspRedirectUrl" text,
    "PayerName" text,
    "PayerEmail" text,
    "ProductId" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "Provider" text,
    "ProviderSessionId" text,
    "PaymentLinkId" text,
    "SlotKey" text,
    "WatchClaimedAt" timestamp with time zone
);


--
-- Name: document_sequences; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.document_sequences (
    "OrgId" text NOT NULL,
    "Series" text NOT NULL,
    "YearMyt" integer NOT NULL,
    "LastN" integer NOT NULL
);


--
-- Name: documents; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documents (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "CheckoutId" text NOT NULL,
    "Number" text,
    "Title" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: gateway_credentials; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.gateway_credentials (
    "OrgId" text NOT NULL,
    "Provider" text NOT NULL,
    "Ciphertext" text NOT NULL,
    "Last4" text,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "WebhookCiphertext" text,
    "PublicMerchantId" text,
    "Environment" text DEFAULT 'test'::text NOT NULL
);


--
-- Name: idempotency_keys; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.idempotency_keys (
    "OrgId" text NOT NULL,
    "Key" text NOT NULL,
    "CheckoutId" text NOT NULL
);


--
-- Name: journal_entries; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.journal_entries (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "CheckoutId" text NOT NULL,
    "Currency" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: journal_lines; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.journal_lines (
    "Id" text NOT NULL,
    "EntryId" text NOT NULL,
    "Account" text NOT NULL,
    "Dc" text NOT NULL,
    "Amount" numeric(18,2) NOT NULL
);


--
-- Name: mail_outbox; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.mail_outbox (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "ToEmail" text,
    "Kind" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: one_webhook_events; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.one_webhook_events (
    "Id" text NOT NULL,
    "DeliveryId" text NOT NULL,
    "EventType" text NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL
);


--
-- Name: org_settings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.org_settings (
    "OrgId" text NOT NULL,
    "Currency" text NOT NULL,
    "ChargesPaused" boolean NOT NULL,
    "SstRegistered" boolean,
    "ActiveProvider" text,
    "OneWebhookCiphertext" text
);


--
-- Name: org_webhook_deliveries; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.org_webhook_deliveries (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "EventId" text NOT NULL,
    "EventType" text NOT NULL,
    "PayloadJson" text NOT NULL,
    "Status" text NOT NULL,
    "AttemptCount" integer NOT NULL,
    "NextAttemptAt" timestamp with time zone NOT NULL,
    "LastHttpStatus" integer,
    "LastError" text,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: org_webhook_endpoints; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.org_webhook_endpoints (
    "OrgId" text NOT NULL,
    "Url" text NOT NULL,
    "SecretCiphertext" text NOT NULL,
    "SecretPrefix" text,
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: payers; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.payers (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "Email" text,
    "Name" text
);


--
-- Name: payment_links; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.payment_links (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "PublicToken" text NOT NULL,
    "Provider" text NOT NULL,
    "ProductId" text,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" text NOT NULL,
    "MaxPayers" integer,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: prices; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.prices (
    "Id" text NOT NULL,
    "ProductId" text NOT NULL,
    "Currency" text NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Interval" text NOT NULL
);


--
-- Name: products; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.products (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: psp_webhook_events; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.psp_webhook_events (
    "OrgId" text NOT NULL,
    "Provider" text NOT NULL,
    "EventId" text NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL
);


--
-- Name: refunds; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.refunds (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "CheckoutId" text NOT NULL,
    "ChargeId" text,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" text NOT NULL,
    "Status" text NOT NULL,
    "Provider" text NOT NULL,
    "ProviderRef" text,
    "Reason" text NOT NULL,
    "IdempotencyKey" text,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: subscriptions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.subscriptions (
    "Id" text NOT NULL,
    "OrgId" text NOT NULL,
    "CheckoutId" text NOT NULL,
    "PayerId" text,
    "Status" text NOT NULL,
    "Interval" text NOT NULL,
    "AttemptCount" integer DEFAULT 0 NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT '-infinity'::timestamp with time zone NOT NULL,
    "PastDueAt" timestamp with time zone
);


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: audit_events PK_audit_events; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.audit_events
    ADD CONSTRAINT "PK_audit_events" PRIMARY KEY ("Id");


--
-- Name: charges PK_charges; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.charges
    ADD CONSTRAINT "PK_charges" PRIMARY KEY ("Id");


--
-- Name: checkouts PK_checkouts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.checkouts
    ADD CONSTRAINT "PK_checkouts" PRIMARY KEY ("Id");


--
-- Name: document_sequences PK_document_sequences; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.document_sequences
    ADD CONSTRAINT "PK_document_sequences" PRIMARY KEY ("OrgId", "Series", "YearMyt");


--
-- Name: documents PK_documents; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documents
    ADD CONSTRAINT "PK_documents" PRIMARY KEY ("Id");


--
-- Name: gateway_credentials PK_gateway_credentials; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.gateway_credentials
    ADD CONSTRAINT "PK_gateway_credentials" PRIMARY KEY ("OrgId", "Provider");


--
-- Name: idempotency_keys PK_idempotency_keys; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.idempotency_keys
    ADD CONSTRAINT "PK_idempotency_keys" PRIMARY KEY ("OrgId", "Key");


--
-- Name: journal_entries PK_journal_entries; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.journal_entries
    ADD CONSTRAINT "PK_journal_entries" PRIMARY KEY ("Id");


--
-- Name: journal_lines PK_journal_lines; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.journal_lines
    ADD CONSTRAINT "PK_journal_lines" PRIMARY KEY ("Id");


--
-- Name: mail_outbox PK_mail_outbox; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.mail_outbox
    ADD CONSTRAINT "PK_mail_outbox" PRIMARY KEY ("Id");


--
-- Name: one_webhook_events PK_one_webhook_events; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.one_webhook_events
    ADD CONSTRAINT "PK_one_webhook_events" PRIMARY KEY ("Id");


--
-- Name: org_settings PK_org_settings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.org_settings
    ADD CONSTRAINT "PK_org_settings" PRIMARY KEY ("OrgId");


--
-- Name: org_webhook_deliveries PK_org_webhook_deliveries; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.org_webhook_deliveries
    ADD CONSTRAINT "PK_org_webhook_deliveries" PRIMARY KEY ("Id");


--
-- Name: org_webhook_endpoints PK_org_webhook_endpoints; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.org_webhook_endpoints
    ADD CONSTRAINT "PK_org_webhook_endpoints" PRIMARY KEY ("OrgId");


--
-- Name: payers PK_payers; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.payers
    ADD CONSTRAINT "PK_payers" PRIMARY KEY ("Id");


--
-- Name: payment_links PK_payment_links; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.payment_links
    ADD CONSTRAINT "PK_payment_links" PRIMARY KEY ("Id");


--
-- Name: prices PK_prices; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.prices
    ADD CONSTRAINT "PK_prices" PRIMARY KEY ("Id");


--
-- Name: products PK_products; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.products
    ADD CONSTRAINT "PK_products" PRIMARY KEY ("Id");


--
-- Name: psp_webhook_events PK_psp_webhook_events; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.psp_webhook_events
    ADD CONSTRAINT "PK_psp_webhook_events" PRIMARY KEY ("OrgId", "Provider", "EventId");


--
-- Name: refunds PK_refunds; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.refunds
    ADD CONSTRAINT "PK_refunds" PRIMARY KEY ("Id");


--
-- Name: subscriptions PK_subscriptions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.subscriptions
    ADD CONSTRAINT "PK_subscriptions" PRIMARY KEY ("Id");


--
-- Name: IX_charges_CheckoutId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_charges_CheckoutId" ON public.charges USING btree ("CheckoutId");


--
-- Name: IX_checkouts_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_checkouts_OrgId" ON public.checkouts USING btree ("OrgId");


--
-- Name: IX_checkouts_PaymentLinkId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_checkouts_PaymentLinkId" ON public.checkouts USING btree ("PaymentLinkId");


--
-- Name: IX_checkouts_PaymentLinkId_SlotKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_checkouts_PaymentLinkId_SlotKey" ON public.checkouts USING btree ("PaymentLinkId", "SlotKey") WHERE ("SlotKey" IS NOT NULL);


--
-- Name: IX_checkouts_PublicToken; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_checkouts_PublicToken" ON public.checkouts USING btree ("PublicToken");


--
-- Name: IX_documents_CheckoutId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_documents_CheckoutId" ON public.documents USING btree ("CheckoutId");


--
-- Name: IX_documents_OrgId_Number; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_documents_OrgId_Number" ON public.documents USING btree ("OrgId", "Number");


--
-- Name: IX_one_webhook_events_DeliveryId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_one_webhook_events_DeliveryId" ON public.one_webhook_events USING btree ("DeliveryId");


--
-- Name: IX_org_webhook_deliveries_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_org_webhook_deliveries_EventId" ON public.org_webhook_deliveries USING btree ("EventId");


--
-- Name: IX_org_webhook_deliveries_Status_NextAttemptAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_org_webhook_deliveries_Status_NextAttemptAt" ON public.org_webhook_deliveries USING btree ("Status", "NextAttemptAt");


--
-- Name: IX_payment_links_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_payment_links_OrgId" ON public.payment_links USING btree ("OrgId");


--
-- Name: IX_payment_links_PublicToken; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_payment_links_PublicToken" ON public.payment_links USING btree ("PublicToken");


--
-- Name: IX_products_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_products_OrgId" ON public.products USING btree ("OrgId");


--
-- Name: IX_refunds_CheckoutId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_refunds_CheckoutId" ON public.refunds USING btree ("CheckoutId");


--
-- Name: IX_refunds_CheckoutId_late_pay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_refunds_CheckoutId_late_pay" ON public.refunds USING btree ("CheckoutId") WHERE ("Reason" = 'late_pay'::text);


--
-- Name: IX_refunds_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_refunds_OrgId" ON public.refunds USING btree ("OrgId");


--
-- Name: IX_refunds_OrgId_IdempotencyKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_refunds_OrgId_IdempotencyKey" ON public.refunds USING btree ("OrgId", "IdempotencyKey") WHERE ("IdempotencyKey" IS NOT NULL);


--
-- Name: IX_subscriptions_CheckoutId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_subscriptions_CheckoutId" ON public.subscriptions USING btree ("CheckoutId");


--
-- Name: IX_subscriptions_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_subscriptions_OrgId" ON public.subscriptions USING btree ("OrgId");


--
-- PostgreSQL database dump complete
--


