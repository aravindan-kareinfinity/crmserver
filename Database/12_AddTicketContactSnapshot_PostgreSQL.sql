-- Store contact snapshot on tickets (person + mobile).
-- Idempotent.

ALTER TABLE tickets
  ADD COLUMN IF NOT EXISTS contact_person VARCHAR(255),
  ADD COLUMN IF NOT EXISTS contact_mobile VARCHAR(20);

CREATE INDEX IF NOT EXISTS idx_tickets_contact_mobile ON tickets(contact_mobile);

