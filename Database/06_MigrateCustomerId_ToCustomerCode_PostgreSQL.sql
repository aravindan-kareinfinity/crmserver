-- Migrate existing databases from INTEGER customer_id FKs to VARCHAR customer_code → customers(code).
-- Run ONLY on DBs created before customer_code change. Fresh installs: use 01_CreateSchema_PostgreSQL.sql only.
-- Backup the database first.
--
-- Steps (per table): add customer_code, backfill from customers, drop old FK/column, add new FK, index.
-- Requires every customer row to have a non-null unique code before NOT NULL + FK.

-- 1) Ensure customers.code is populated and unique
UPDATE customers SET code = 'LEGACY/' || id::text WHERE code IS NULL OR trim(code) = '';

ALTER TABLE customers DROP CONSTRAINT IF EXISTS customers_code_key;
CREATE UNIQUE INDEX IF NOT EXISTS customers_code_key ON customers (code);
ALTER TABLE customers ALTER COLUMN code SET NOT NULL;

-- 2) customer_timelines
ALTER TABLE customer_timelines ADD COLUMN IF NOT EXISTS customer_code VARCHAR(100);
UPDATE customer_timelines ct SET customer_code = c.code FROM customers c WHERE c.id = ct.customer_id AND ct.customer_code IS NULL;
ALTER TABLE customer_timelines ALTER COLUMN customer_code SET NOT NULL;
ALTER TABLE customer_timelines DROP CONSTRAINT IF EXISTS customer_timelines_customer_id_fkey;
DROP INDEX IF EXISTS idx_customer_timeline_customer_id;
ALTER TABLE customer_timelines DROP COLUMN IF EXISTS customer_id;
ALTER TABLE customer_timelines ADD CONSTRAINT customer_timelines_customer_code_fkey
    FOREIGN KEY (customer_code) REFERENCES customers(code) ON DELETE CASCADE;
CREATE INDEX IF NOT EXISTS idx_customer_timeline_customer_code ON customer_timelines(customer_code);

-- 3) services
ALTER TABLE services ADD COLUMN IF NOT EXISTS customer_code VARCHAR(100);
UPDATE services s SET customer_code = c.code FROM customers c WHERE c.id = s.customer_id AND s.customer_code IS NULL;
ALTER TABLE services ALTER COLUMN customer_code SET NOT NULL;
ALTER TABLE services DROP CONSTRAINT IF EXISTS services_customer_id_fkey;
DROP INDEX IF EXISTS idx_service_customer_id;
ALTER TABLE services DROP COLUMN IF EXISTS customer_id;
ALTER TABLE services ADD CONSTRAINT services_customer_code_fkey
    FOREIGN KEY (customer_code) REFERENCES customers(code) ON DELETE CASCADE;
CREATE INDEX IF NOT EXISTS idx_service_customer_code ON services(customer_code);

-- 4) invoices
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS customer_code VARCHAR(100);
UPDATE invoices i SET customer_code = c.code FROM customers c WHERE c.id = i.customer_id AND i.customer_code IS NULL;
ALTER TABLE invoices ALTER COLUMN customer_code SET NOT NULL;
ALTER TABLE invoices DROP CONSTRAINT IF EXISTS invoices_customer_id_fkey;
DROP INDEX IF EXISTS idx_invoice_customer_id;
ALTER TABLE invoices DROP COLUMN IF EXISTS customer_id;
ALTER TABLE invoices ADD CONSTRAINT invoices_customer_code_fkey
    FOREIGN KEY (customer_code) REFERENCES customers(code) ON DELETE CASCADE;
CREATE INDEX IF NOT EXISTS idx_invoice_customer_code ON invoices(customer_code);

-- 5) investments
ALTER TABLE investments ADD COLUMN IF NOT EXISTS customer_code VARCHAR(100);
UPDATE investments inv SET customer_code = c.code FROM customers c WHERE c.id = inv.customer_id AND inv.customer_code IS NULL;
ALTER TABLE investments ALTER COLUMN customer_code SET NOT NULL;
ALTER TABLE investments DROP CONSTRAINT IF EXISTS investments_customer_id_fkey;
DROP INDEX IF EXISTS idx_investment_customer_id;
ALTER TABLE investments DROP COLUMN IF EXISTS customer_id;
ALTER TABLE investments ADD CONSTRAINT investments_customer_code_fkey
    FOREIGN KEY (customer_code) REFERENCES customers(code) ON DELETE CASCADE;
CREATE INDEX IF NOT EXISTS idx_investment_customer_code ON investments(customer_code);

-- 6) tickets
ALTER TABLE tickets ADD COLUMN IF NOT EXISTS customer_code VARCHAR(100);
UPDATE tickets t SET customer_code = c.code FROM customers c WHERE c.id = t.customer_id AND t.customer_code IS NULL;
ALTER TABLE tickets ALTER COLUMN customer_code SET NOT NULL;
ALTER TABLE tickets DROP CONSTRAINT IF EXISTS tickets_customer_id_fkey;
DROP INDEX IF EXISTS idx_ticket_customer_id;
ALTER TABLE tickets DROP COLUMN IF EXISTS customer_id;
ALTER TABLE tickets ADD CONSTRAINT tickets_customer_code_fkey
    FOREIGN KEY (customer_code) REFERENCES customers(code) ON DELETE CASCADE;
CREATE INDEX IF NOT EXISTS idx_ticket_customer_code ON tickets(customer_code);

-- 7) trademarks (before locations: no FK to locations in legacy schema)
ALTER TABLE trademarks ADD COLUMN IF NOT EXISTS customer_code VARCHAR(100);
UPDATE trademarks tm SET customer_code = c.code FROM customers c WHERE c.id = tm.customer_id AND tm.customer_code IS NULL;
ALTER TABLE trademarks ALTER COLUMN customer_code SET NOT NULL;
ALTER TABLE trademarks DROP CONSTRAINT IF EXISTS trademarks_customer_id_fkey;
DROP INDEX IF EXISTS idx_trademark_customer_id;
ALTER TABLE trademarks DROP COLUMN IF EXISTS customer_id;
ALTER TABLE trademarks ADD CONSTRAINT trademarks_customer_code_fkey
    FOREIGN KEY (customer_code) REFERENCES customers(code) ON DELETE CASCADE;
CREATE INDEX IF NOT EXISTS idx_trademark_customer_code ON trademarks(customer_code);

-- 8) locations
ALTER TABLE locations ADD COLUMN IF NOT EXISTS customer_code VARCHAR(100);
UPDATE locations l SET customer_code = c.code FROM customers c WHERE c.id = l.customer_id AND l.customer_code IS NULL;
ALTER TABLE locations ALTER COLUMN customer_code SET NOT NULL;
ALTER TABLE locations DROP CONSTRAINT IF EXISTS locations_customer_id_fkey;
DROP INDEX IF EXISTS idx_location_customer_id;
ALTER TABLE locations DROP COLUMN IF EXISTS customer_id;
ALTER TABLE locations ADD CONSTRAINT locations_customer_code_fkey
    FOREIGN KEY (customer_code) REFERENCES customers(code) ON DELETE CASCADE;
CREATE INDEX IF NOT EXISTS idx_location_customer_code ON locations(customer_code);

SELECT '06_MigrateCustomerId_ToCustomerCode finished — verify app against this database.' AS status;
