-- Split conversion tracking: Lead→Prospect vs Prospect→Customer.
-- Idempotent (IF NOT EXISTS).

ALTER TABLE customers
  ADD COLUMN IF NOT EXISTS prospect_converted_at TIMESTAMP,
  ADD COLUMN IF NOT EXISTS prospect_converted_by BIGINT,
  ADD COLUMN IF NOT EXISTS customer_converted_at TIMESTAMP,
  ADD COLUMN IF NOT EXISTS customer_converted_by BIGINT;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'fk_customers_prospect_converted_by'
  ) THEN
    ALTER TABLE customers
      ADD CONSTRAINT fk_customers_prospect_converted_by
      FOREIGN KEY (prospect_converted_by) REFERENCES users(id);
  END IF;
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'fk_customers_customer_converted_by'
  ) THEN
    ALTER TABLE customers
      ADD CONSTRAINT fk_customers_customer_converted_by
      FOREIGN KEY (customer_converted_by) REFERENCES users(id);
  END IF;
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

CREATE INDEX IF NOT EXISTS idx_customers_prospect_converted_at ON customers(prospect_converted_at);
CREATE INDEX IF NOT EXISTS idx_customers_customer_converted_at ON customers(customer_converted_at);

-- Optional: align legacy converted_at with customer_converted_at when missing.
UPDATE customers
SET customer_converted_at = converted_at
WHERE converted_at IS NOT NULL AND customer_converted_at IS NULL;
