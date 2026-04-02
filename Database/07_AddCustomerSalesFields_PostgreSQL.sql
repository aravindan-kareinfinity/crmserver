-- Add additional sales / interaction fields to customers.
-- Safe to run once (PostgreSQL 12+ for IF NOT EXISTS on ADD COLUMN).

ALTER TABLE customers
  ADD COLUMN IF NOT EXISTS product_features_discussed BOOLEAN NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS assigned_representative_id BIGINT,
  ADD COLUMN IF NOT EXISTS interaction_mode_id INTEGER,
  ADD COLUMN IF NOT EXISTS price_plan_selected BOOLEAN NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS quotation_prepared_sent BOOLEAN NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS quotation_accepted BOOLEAN NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS advance_payment_received BOOLEAN NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS invoice_generated BOOLEAN NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS invoice_number VARCHAR(80);

DO $$
BEGIN
  ALTER TABLE customers
    ADD CONSTRAINT fk_customers_interaction_mode
    FOREIGN KEY (interaction_mode_id) REFERENCES reference_entries(id);
EXCEPTION
  WHEN duplicate_object THEN
    NULL;
END $$;

CREATE INDEX IF NOT EXISTS idx_customers_assigned_representative_id ON customers(assigned_representative_id);
CREATE INDEX IF NOT EXISTS idx_customers_interaction_mode_id ON customers(interaction_mode_id);

