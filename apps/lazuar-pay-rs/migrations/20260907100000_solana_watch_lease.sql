-- Watcher lease on reservations (032/08 claimed_at). Not on payments.
ALTER TABLE pay_rs.reservations
    ADD COLUMN claimed_at timestamptz;

CREATE INDEX reservations_unclaimed
    ON pay_rs.reservations (claimed_at)
    WHERE claimed_at IS NULL;
