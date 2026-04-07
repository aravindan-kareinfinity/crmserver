-- Add locations.customer_id (FK to customers.id), backfilled from customer_code.
-- Safe to re-run: skips if column already exists.

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'locations' AND column_name = 'customer_id'
  ) THEN
    ALTER TABLE locations ADD COLUMN customer_id INTEGER;

    UPDATE locations l
    SET customer_id = c.id
    FROM customers c
    WHERE c.code = l.customer_code;

    ALTER TABLE locations ALTER COLUMN customer_id SET NOT NULL;

    ALTER TABLE locations
      ADD CONSTRAINT fk_locations_customer_id
      FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE;

    CREATE INDEX IF NOT EXISTS idx_location_customer_id ON locations(customer_id);
  END IF;
END $$;

SELECT 'locations.customer_id migration finished' AS status;
