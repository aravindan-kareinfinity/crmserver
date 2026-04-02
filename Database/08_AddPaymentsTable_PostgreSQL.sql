-- Create payments table for invoice collections.
-- Safe to run multiple times (idempotent).

CREATE TABLE IF NOT EXISTS payments (
    id SERIAL PRIMARY KEY,
    invoice_id INTEGER NOT NULL,
    customer_code VARCHAR(100) NOT NULL,
    amount NUMERIC(12,2) NOT NULL DEFAULT 0,
    remaining NUMERIC(12,2) NOT NULL DEFAULT 0,
    payment_mode_id INTEGER NOT NULL,
    received_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    notes VARCHAR(500),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    CONSTRAINT fk_payments_invoice_id FOREIGN KEY (invoice_id) REFERENCES invoices(id) ON DELETE CASCADE,
    CONSTRAINT fk_payments_customer_code FOREIGN KEY (customer_code) REFERENCES customers(code) ON DELETE CASCADE,
    CONSTRAINT fk_payments_payment_mode_id FOREIGN KEY (payment_mode_id) REFERENCES reference_entries(id)
);

CREATE INDEX IF NOT EXISTS idx_payments_invoice_id ON payments(invoice_id);
CREATE INDEX IF NOT EXISTS idx_payments_customer_code ON payments(customer_code);
CREATE INDEX IF NOT EXISTS idx_payments_received_at ON payments(received_at);

