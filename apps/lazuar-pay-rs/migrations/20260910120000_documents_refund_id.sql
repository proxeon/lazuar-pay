-- REF documents must join refunds after sequential numbering (RCPT-2026-00001).
-- The old REF-TEST-{uuid} number encoded the refund id; year-n numbers cannot.
-- RCPT rows leave refund_id NULL. Backfilled REF rows may also be NULL.

ALTER TABLE pay_rs.documents
    ADD COLUMN refund_id uuid REFERENCES pay_rs.refunds (id);

CREATE UNIQUE INDEX documents_refund
    ON pay_rs.documents (refund_id)
    WHERE refund_id IS NOT NULL;
