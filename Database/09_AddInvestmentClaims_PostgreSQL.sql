-- Add investment claim tracking.
-- Safe to run multiple times (idempotent).

ALTER TABLE investments
  ADD COLUMN IF NOT EXISTS claimed_amount NUMERIC(12,2) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS remaining_amount NUMERIC(12,2) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS claimed_fully BOOLEAN NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS claimed_at TIMESTAMP,
  ADD COLUMN IF NOT EXISTS claimed_by BIGINT,
  ADD COLUMN IF NOT EXISTS claim_notes VARCHAR(500);

-- Backfill remaining_amount when missing/0.
UPDATE investments
SET remaining_amount = GREATEST(0, amount - claimed_amount)
WHERE remaining_amount = 0;

-- Backfill claim metadata for already-claimed rows (legacy data before columns existed).
UPDATE investments
SET
  claimed_amount = amount,
  remaining_amount = 0,
  claimed_at = COALESCE(claimed_at, modified_at, created_at),
  claimed_by = COALESCE(claimed_by, modified_by, created_by)
WHERE claimed_fully = true AND claimed_at IS NULL;

