-- Add customer_id (FK to customers.id) alongside existing customer_code on child tables.
-- For databases that only have customer_code (e.g. after 06_MigrateCustomerId_ToCustomerCode).
-- locations.customer_id is handled by 15_AddLocationCustomerId_PostgreSQL.sql — skipped here.
-- Safe to re-run: each block only runs if customer_id is missing.

-- payments
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'payments' AND column_name = 'customer_id'
  ) THEN
    ALTER TABLE payments ADD COLUMN customer_id INTEGER;
    UPDATE payments p SET customer_id = c.id FROM customers c WHERE c.code = p.customer_code;
    ALTER TABLE payments ALTER COLUMN customer_id SET NOT NULL;
    ALTER TABLE payments
      ADD CONSTRAINT fk_payments_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE;
    CREATE INDEX IF NOT EXISTS idx_payments_customer_id ON payments(customer_id);
  END IF;
END $$;

-- customer_timelines
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'customer_timelines' AND column_name = 'customer_id'
  ) THEN
    ALTER TABLE customer_timelines ADD COLUMN customer_id INTEGER;
    UPDATE customer_timelines ct SET customer_id = c.id FROM customers c WHERE c.code = ct.customer_code;
    ALTER TABLE customer_timelines ALTER COLUMN customer_id SET NOT NULL;
    ALTER TABLE customer_timelines
      ADD CONSTRAINT fk_customer_timelines_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE;
    CREATE INDEX IF NOT EXISTS idx_customer_timeline_customer_id ON customer_timelines(customer_id);
  END IF;
END $$;

-- services
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'services' AND column_name = 'customer_id'
  ) THEN
    ALTER TABLE services ADD COLUMN customer_id INTEGER;
    UPDATE services s SET customer_id = c.id FROM customers c WHERE c.code = s.customer_code;
    ALTER TABLE services ALTER COLUMN customer_id SET NOT NULL;
    ALTER TABLE services
      ADD CONSTRAINT fk_services_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE;
    CREATE INDEX IF NOT EXISTS idx_service_customer_id ON services(customer_id);
  END IF;
END $$;

-- invoices
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'invoices' AND column_name = 'customer_id'
  ) THEN
    ALTER TABLE invoices ADD COLUMN customer_id INTEGER;
    UPDATE invoices i SET customer_id = c.id FROM customers c WHERE c.code = i.customer_code;
    ALTER TABLE invoices ALTER COLUMN customer_id SET NOT NULL;
    ALTER TABLE invoices
      ADD CONSTRAINT fk_invoices_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE;
    CREATE INDEX IF NOT EXISTS idx_invoice_customer_id ON invoices(customer_id);
  END IF;
END $$;

-- investments
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'investments' AND column_name = 'customer_id'
  ) THEN
    ALTER TABLE investments ADD COLUMN customer_id INTEGER;
    UPDATE investments inv SET customer_id = c.id FROM customers c WHERE c.code = inv.customer_code;
    ALTER TABLE investments ALTER COLUMN customer_id SET NOT NULL;
    ALTER TABLE investments
      ADD CONSTRAINT fk_investments_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE;
    CREATE INDEX IF NOT EXISTS idx_investment_customer_id ON investments(customer_id);
  END IF;
END $$;

-- tickets
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'tickets' AND column_name = 'customer_id'
  ) THEN
    ALTER TABLE tickets ADD COLUMN customer_id INTEGER;
    UPDATE tickets t SET customer_id = c.id FROM customers c WHERE c.code = t.customer_code;
    ALTER TABLE tickets ALTER COLUMN customer_id SET NOT NULL;
    ALTER TABLE tickets
      ADD CONSTRAINT fk_tickets_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE;
    CREATE INDEX IF NOT EXISTS idx_ticket_customer_id ON tickets(customer_id);
  END IF;
END $$;

-- trademarks
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'trademarks' AND column_name = 'customer_id'
  ) THEN
    ALTER TABLE trademarks ADD COLUMN customer_id INTEGER;
    UPDATE trademarks tm SET customer_id = c.id FROM customers c WHERE c.code = tm.customer_code;
    ALTER TABLE trademarks ALTER COLUMN customer_id SET NOT NULL;
    ALTER TABLE trademarks
      ADD CONSTRAINT fk_trademarks_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE;
    CREATE INDEX IF NOT EXISTS idx_trademark_customer_id ON trademarks(customer_id);
  END IF;
END $$;

SELECT '16_AddCustomerIdAlongsideCustomerCode finished' AS status;
