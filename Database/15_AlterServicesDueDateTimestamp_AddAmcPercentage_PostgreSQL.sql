-- Convert services.due_date from INTEGER (day-of-month) to TIMESTAMP WITHOUT TIME ZONE
-- and add services.amc_percentage.

ALTER TABLE services
  ADD COLUMN IF NOT EXISTS due_date_ts TIMESTAMP;

-- Backfill due_date_ts from existing due_month + due_date + created_at year.
-- Clamp day to 28 to avoid invalid dates (matches existing UI rule).
UPDATE services
SET due_date_ts = make_timestamp(
  EXTRACT(YEAR FROM created_at)::int,
  GREATEST(1, LEAST(12, due_month)),
  GREATEST(1, LEAST(28, due_date)),
  0, 0, 0
)
WHERE due_date_ts IS NULL;

-- Ensure non-null.
UPDATE services
SET due_date_ts = COALESCE(due_date_ts, created_at)
WHERE due_date_ts IS NULL;

ALTER TABLE services
  ALTER COLUMN due_date_ts SET NOT NULL;

-- Swap columns (safe pattern).
ALTER TABLE services
  DROP COLUMN IF EXISTS due_date;

ALTER TABLE services
  RENAME COLUMN due_date_ts TO due_date;

-- New field: AMC percentage (0-100 typical, but not enforced here).
ALTER TABLE services
  ADD COLUMN IF NOT EXISTS amc_percentage NUMERIC(6,2);

-- New field: AMC amount (optional absolute amount).
ALTER TABLE services
  ADD COLUMN IF NOT EXISTS amc_amount NUMERIC(12,2);

CREATE INDEX IF NOT EXISTS idx_services_due_date ON services(due_date);
