-- Add per-investment "needs claim" toggle.
-- Idempotent.

ALTER TABLE investments
  ADD COLUMN IF NOT EXISTS needs_claim BOOLEAN NOT NULL DEFAULT true;

-- Backfill existing rows (keep claim behavior unchanged).
UPDATE investments
SET needs_claim = true
WHERE needs_claim IS NULL;

CREATE INDEX IF NOT EXISTS idx_investments_needs_claim ON investments(needs_claim);

