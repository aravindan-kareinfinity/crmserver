-- Change tickets.status from enum to varchar, so API can use reference_entries (Ticket Status) values.
-- This removes the need for a server-side TicketStatus enum.

ALTER TABLE tickets
  ALTER COLUMN status TYPE VARCHAR(50)
  USING status::text;

ALTER TABLE tickets
  ALTER COLUMN status SET DEFAULT 'open';

-- Keep same index name; recreate to match new type (safe even if planner would keep it).
DROP INDEX IF EXISTS idx_ticket_status;
CREATE INDEX idx_ticket_status ON tickets(status);

-- NOTE:
-- You can optionally drop the enum type if nothing else uses it:
-- DROP TYPE IF EXISTS ticket_status;

