-- Move tickets.category/module from text to reference_entries FK ids.

ALTER TABLE tickets
ADD COLUMN IF NOT EXISTS category_id INTEGER,
ADD COLUMN IF NOT EXISTS module_id INTEGER;

-- Backfill category_id from existing category text
UPDATE tickets t
SET category_id = (
  SELECT r.id
  FROM reference_entries r
  WHERE r.is_active=true
    AND r.category='Ticket Category'
    AND (lower(r.value)=lower(t.category::text) OR lower(r.label)=lower(t.category::text))
  ORDER BY r.sort_order ASC
  LIMIT 1
)
WHERE t.category_id IS NULL;

-- Fallback: first active Ticket Category entry
UPDATE tickets
SET category_id = (
  SELECT r.id
  FROM reference_entries r
  WHERE r.is_active=true
    AND r.category='Ticket Category'
  ORDER BY r.sort_order ASC
  LIMIT 1
)
WHERE category_id IS NULL;

ALTER TABLE tickets
ALTER COLUMN category_id SET NOT NULL;

ALTER TABLE tickets
ADD CONSTRAINT fk_tickets_category_id
FOREIGN KEY (category_id) REFERENCES reference_entries(id);

-- Backfill module_id from existing module text (optional)
UPDATE tickets t
SET module_id = (
  SELECT r.id
  FROM reference_entries r
  WHERE r.is_active=true
    AND r.category='Ticket Module'
    AND (lower(r.value)=lower(t.module::text) OR lower(r.label)=lower(t.module::text))
  ORDER BY r.sort_order ASC
  LIMIT 1
)
WHERE t.module_id IS NULL
  AND t.module IS NOT NULL
  AND trim(t.module) <> '';

ALTER TABLE tickets
ADD CONSTRAINT fk_tickets_module_id
FOREIGN KEY (module_id) REFERENCES reference_entries(id);

CREATE INDEX IF NOT EXISTS idx_ticket_category_id ON tickets(category_id);
CREATE INDEX IF NOT EXISTS idx_ticket_module_id ON tickets(module_id);

-- (optional) once verified, drop old columns:
-- ALTER TABLE tickets DROP COLUMN category;
-- ALTER TABLE tickets DROP COLUMN module;

