-- Add lead_source_id to customers (FK to reference_entries).
-- Run on existing DBs after 01_CreateSchema_PostgreSQL.sql + reference data.

ALTER TABLE customers
  ADD COLUMN IF NOT EXISTS lead_source_id INTEGER;

DO $$
BEGIN
  IF NOT EXISTS (
      SELECT 1
      FROM information_schema.table_constraints
      WHERE constraint_name = 'fk_customers_lead_source_id'
        AND table_name = 'customers'
  ) THEN
    ALTER TABLE customers
      ADD CONSTRAINT fk_customers_lead_source_id
      FOREIGN KEY (lead_source_id) REFERENCES reference_entries(id);
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_customer_lead_source_id ON customers(lead_source_id);
