-- Move tickets.status from enum/text to reference_entries FK.
-- Uses reference_entries(category='Ticket Status') ids.

ALTER TABLE tickets
ADD COLUMN IF NOT EXISTS status_id INTEGER;

-- Backfill from existing enum/text status
UPDATE tickets t
SET status_id = (
  SELECT r.id
  FROM reference_entries r
  WHERE r.is_active=true
    AND r.category='Ticket Status'
    AND (lower(r.value)=lower(t.status::text) OR lower(r.label)=lower(t.status::text))
  ORDER BY r.sort_order ASC
  LIMIT 1
)
WHERE t.status_id IS NULL;

-- Fallback to Open (by reference_entries.value='open')
UPDATE tickets
SET status_id = (
  SELECT r.id
  FROM reference_entries r
  WHERE r.is_active=true
    AND r.category='Ticket Status'
    AND lower(r.value)='open'
  ORDER BY r.sort_order ASC
  LIMIT 1
)
WHERE status_id IS NULL;

-- Final fallback: first active Ticket Status entry
UPDATE tickets
SET status_id = (
  SELECT r.id
  FROM reference_entries r
  WHERE r.is_active=true
    AND r.category='Ticket Status'
  ORDER BY r.sort_order ASC
  LIMIT 1
)
WHERE status_id IS NULL;

ALTER TABLE tickets
ALTER COLUMN status_id SET NOT NULL;

ALTER TABLE tickets
ADD CONSTRAINT fk_tickets_status_id
FOREIGN KEY (status_id) REFERENCES reference_entries(id);

CREATE INDEX IF NOT EXISTS idx_ticket_status_id ON tickets(status_id);

-- (optional) keep old column for now; once verified, you can drop it:
-- ALTER TABLE tickets DROP COLUMN status;

