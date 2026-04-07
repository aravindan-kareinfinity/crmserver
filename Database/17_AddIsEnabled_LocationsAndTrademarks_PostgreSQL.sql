-- Add is_enabled flags (soft-disable) for locations and trademarks
-- Default enabled for existing + new rows.

ALTER TABLE locations
ADD COLUMN IF NOT EXISTS is_enabled BOOLEAN NOT NULL DEFAULT true;

ALTER TABLE trademarks
ADD COLUMN IF NOT EXISTS is_enabled BOOLEAN NOT NULL DEFAULT true;

UPDATE locations SET is_enabled = true WHERE is_enabled IS NULL;
UPDATE trademarks SET is_enabled = true WHERE is_enabled IS NULL;

CREATE INDEX IF NOT EXISTS idx_location_is_enabled ON locations(is_enabled);
CREATE INDEX IF NOT EXISTS idx_trademark_is_enabled ON trademarks(is_enabled);

