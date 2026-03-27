-- CRM Database Schema for PostgreSQL (full create on empty DB).
-- Then run: 03_ReferenceData_PostgreSQL.sql (reference_entries only).
-- Then run: 02_Minimal_AdminAndReference_PostgreSQL.sql (Admin role + admin user).
-- WARNING: drops listed objects if they exist; use only on disposable/empty databases.

-- ========== DROP EXISTING OBJECTS ==========
DROP TABLE IF EXISTS payment_timelines CASCADE;
DROP TABLE IF EXISTS payments CASCADE;
DROP TABLE IF EXISTS ticket_timelines CASCADE;
DROP TABLE IF EXISTS tickets CASCADE;
DROP TABLE IF EXISTS implementation_timelines CASCADE;
DROP TABLE IF EXISTS implementation_assignments CASCADE;
DROP TABLE IF EXISTS investment_timelines CASCADE;
DROP TABLE IF EXISTS investments CASCADE;
DROP TABLE IF EXISTS invoice_timelines CASCADE;
DROP TABLE IF EXISTS invoices CASCADE;
DROP TABLE IF EXISTS location_timelines CASCADE;
DROP TABLE IF EXISTS locations CASCADE;
DROP TABLE IF EXISTS trademarks CASCADE;
DROP TABLE IF EXISTS services CASCADE;
DROP TABLE IF EXISTS customer_timelines CASCADE;
DROP TABLE IF EXISTS customers CASCADE;
DROP TABLE IF EXISTS reference_entries CASCADE;
DROP TABLE IF EXISTS roles CASCADE;
DROP TABLE IF EXISTS users CASCADE;
DROP TABLE IF EXISTS files CASCADE;
DROP TABLE IF EXISTS reports CASCADE;
DROP TABLE IF EXISTS scheduler_events CASCADE;

-- ========== CREATE ENUM TYPES ==========
CREATE TYPE customer_type AS ENUM ('lead', 'prospect', 'customer');
CREATE TYPE ticket_status AS ENUM ('open', 'in_progress', 'waiting', 'resolved', 'closed');
CREATE TYPE ticket_priority AS ENUM ('critical', 'high', 'medium', 'low');
CREATE TYPE trademark_status AS ENUM ('active', 'expired', 'pending', 'rejected');
CREATE TYPE payment_status_enum AS ENUM ('active', 'inactive');
CREATE TYPE implementation_status_enum AS ENUM ('OPEN', 'IN_PROGRESS', 'COMPLETED');

-- ========== CREATE REFERENCE ENTRIES TABLE ==========
CREATE TABLE reference_entries (
    id SERIAL PRIMARY KEY,
    category VARCHAR(100) NOT NULL,
    label VARCHAR(200) NOT NULL,
    value VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    sort_order INTEGER NOT NULL DEFAULT 0,
    requires_implementation BOOLEAN, is_implementation BOOLEAN,
    UNIQUE(category, value)
);

CREATE INDEX idx_reference_entry_category ON reference_entries(category);
CREATE INDEX idx_reference_entry_is_active ON reference_entries(is_active);

-- ========== CREATE USERS TABLE ==========
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    user_id VARCHAR(100) NOT NULL UNIQUE,
    first_name VARCHAR(255) NOT NULL,
    last_name VARCHAR(255) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255),
    role VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    last_login TIMESTAMP NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT
);

CREATE INDEX idx_user_email ON users(email);
CREATE INDEX idx_user_user_id ON users(user_id);

-- ========== CREATE ROLES TABLE ==========
CREATE TABLE roles (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    description VARCHAR(500) NOT NULL,
    permissions TEXT NOT NULL,
    user_count INTEGER,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT
);

-- ========== CREATE CUSTOMERS TABLE ==========
CREATE TABLE customers (
    id SERIAL PRIMARY KEY,
    code VARCHAR(100),
    reg_name VARCHAR(255) NOT NULL,
    mobile VARCHAR(10) NOT NULL,
    email VARCHAR(255) NOT NULL,
    business_type_id INTEGER,
    industry_id INTEGER,
    address_line1 VARCHAR(255) NOT NULL,
    address_line2 VARCHAR(255),
    city_id INTEGER,
    state_id INTEGER,
    country_id INTEGER,
    pincode VARCHAR(6) NOT NULL,
    gst_number VARCHAR(15),
    contact_persons TEXT,
    emails TEXT,
    mobiles TEXT,
    shop_size_id INTEGER NOT NULL,
    tier_id INTEGER NOT NULL,
    type_id INTEGER NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    total_locations INTEGER,
    total_trade_names INTEGER,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT,
    converted_at TIMESTAMP,
    converted_by VARCHAR(255),
    pipeline_status VARCHAR(80),
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (business_type_id) REFERENCES reference_entries(id),
    FOREIGN KEY (industry_id) REFERENCES reference_entries(id),
    FOREIGN KEY (city_id) REFERENCES reference_entries(id),
    FOREIGN KEY (state_id) REFERENCES reference_entries(id),
    FOREIGN KEY (country_id) REFERENCES reference_entries(id),
    FOREIGN KEY (shop_size_id) REFERENCES reference_entries(id),
    FOREIGN KEY (tier_id) REFERENCES reference_entries(id),
    FOREIGN KEY (type_id) REFERENCES reference_entries(id)
);

CREATE INDEX idx_customer_email ON customers(email);
CREATE INDEX idx_customer_type_id ON customers(type_id);
CREATE INDEX idx_customer_is_active ON customers(is_active);
CREATE INDEX idx_customer_created_at ON customers(created_at);

-- ========== CREATE CUSTOMER TIMELINE TABLE ==========
CREATE TABLE customer_timelines (
    id SERIAL PRIMARY KEY,
    customer_id INTEGER NOT NULL,
    type INTEGER NOT NULL,
    notes TEXT NOT NULL,
    file_id INTEGER,
    file_name VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT NOT NULL DEFAULT 1,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE
);

CREATE INDEX idx_customer_timeline_customer_id ON customer_timelines(customer_id);

-- ========== CREATE SERVICES TABLE ==========
CREATE TABLE services (
    id SERIAL PRIMARY KEY,
    customer_id INTEGER NOT NULL,
    location_id INTEGER,
    trade_name_id INTEGER,
    service_type_id INTEGER NOT NULL,
    frequency_id INTEGER,
    due_date INTEGER NOT NULL,
    live_date TIMESTAMP,
    service_value DECIMAL(18,2),
    due_month INTEGER NOT NULL,
    implementation_required BOOLEAN NOT NULL DEFAULT false,
    implementation_status implementation_status_enum NOT NULL DEFAULT 'OPEN',
    implementation_stage_id INTEGER,
    implementation_started_at TIMESTAMP,
    implementation_started_by VARCHAR(255),
    implementation_completed_at TIMESTAMP,
    implementation_completed_by VARCHAR(255),
    project_title VARCHAR(255),
    project_manager_id INTEGER,
    budget_amount DECIMAL(18,2),
    progress_percentage INTEGER,
    tax_id INTEGER,
    notes TEXT,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE,
    FOREIGN KEY (service_type_id) REFERENCES reference_entries(id),
    FOREIGN KEY (frequency_id) REFERENCES reference_entries(id),
    FOREIGN KEY (implementation_stage_id) REFERENCES reference_entries(id),
    FOREIGN KEY (tax_id) REFERENCES reference_entries(id),
    FOREIGN KEY (project_manager_id) REFERENCES users(id)
);

CREATE INDEX idx_service_customer_id ON services(customer_id);
CREATE INDEX idx_service_is_active ON services(is_active);
CREATE INDEX idx_service_created_at ON services(created_at);

-- ========== CREATE INVOICES TABLE ==========
CREATE TABLE invoices (
    id SERIAL PRIMARY KEY,
    invoice_number VARCHAR(100) NOT NULL UNIQUE,
    customer_id INTEGER NOT NULL,
    service_id INTEGER NOT NULL,
    staff_id INTEGER,
    payment_mode_id INTEGER NOT NULL,
    payment_status_id INTEGER NOT NULL,
    receivable DECIMAL(18,2) NOT NULL,
    received DECIMAL(18,2) NOT NULL DEFAULT 0,
    subscription_start_at TIMESTAMP NOT NULL,
    subscription_end_at TIMESTAMP NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT,
    paid_at TIMESTAMP,
    paid_by VARCHAR(255),
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE,
    FOREIGN KEY (service_id) REFERENCES services(id) ON DELETE CASCADE,
    FOREIGN KEY (staff_id) REFERENCES users(id),
    FOREIGN KEY (payment_mode_id) REFERENCES reference_entries(id),
    FOREIGN KEY (payment_status_id) REFERENCES reference_entries(id)
);

CREATE INDEX idx_invoice_customer_id ON invoices(customer_id);
CREATE INDEX idx_invoice_service_id ON invoices(service_id);
CREATE INDEX idx_invoice_payment_status ON invoices(payment_status_id);
CREATE INDEX idx_invoice_created_at ON invoices(created_at);

-- ========== CREATE INVOICE TIMELINE TABLE ==========
CREATE TABLE invoice_timelines (
    id SERIAL PRIMARY KEY,
    invoice_id INTEGER NOT NULL,
    type INTEGER NOT NULL,
    notes TEXT NOT NULL,
    file_id INTEGER,
    file_name VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT NOT NULL DEFAULT 1,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (invoice_id) REFERENCES invoices(id) ON DELETE CASCADE
);

CREATE INDEX idx_invoice_timeline_invoice_id ON invoice_timelines(invoice_id);

-- ========== CREATE INVESTMENTS TABLE ==========
CREATE TABLE investments (
    id SERIAL PRIMARY KEY,
    customer_id INTEGER NOT NULL,
    location_id INTEGER NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    investment_type_id INTEGER NOT NULL,
    staff_id INTEGER,
    notes TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE,
    FOREIGN KEY (investment_type_id) REFERENCES reference_entries(id),
    FOREIGN KEY (staff_id) REFERENCES users(id)
);

CREATE INDEX idx_investment_customer_id ON investments(customer_id);

-- ========== CREATE INVESTMENT TIMELINE TABLE ==========
CREATE TABLE investment_timelines (
    id SERIAL PRIMARY KEY,
    investment_id INTEGER NOT NULL,
    type INTEGER NOT NULL,
    notes TEXT NOT NULL,
    file_id INTEGER,
    file_name VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT NOT NULL DEFAULT 1,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (investment_id) REFERENCES investments(id) ON DELETE CASCADE
);

CREATE INDEX idx_investment_timeline_investment_id ON investment_timelines(investment_id);

-- ========== CREATE IMPLEMENTATION ASSIGNMENTS TABLE ==========
CREATE TABLE implementation_assignments (
    id SERIAL PRIMARY KEY,
    service_id INTEGER NOT NULL,
    user_ids TEXT NOT NULL,
    FOREIGN KEY (service_id) REFERENCES services(id) ON DELETE CASCADE
);

CREATE INDEX idx_implementation_assignment_service_id ON implementation_assignments(service_id);

-- ========== CREATE IMPLEMENTATION TIMELINE TABLE ==========
CREATE TABLE implementation_timelines (
    id SERIAL PRIMARY KEY,
    service_id INTEGER NOT NULL,
    type INTEGER NOT NULL,
    status implementation_status_enum NOT NULL,
    notes TEXT NOT NULL,
    file_id INTEGER,
    file_name VARCHAR(255),
    user_id INTEGER NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT NOT NULL DEFAULT 1,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (service_id) REFERENCES services(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE INDEX idx_implementation_timeline_service_id ON implementation_timelines(service_id);
CREATE INDEX idx_implementation_timeline_status ON implementation_timelines(status);

-- ========== CREATE TICKETS TABLE ==========
CREATE TABLE tickets (
    id SERIAL PRIMARY KEY,
    customer_id INTEGER NOT NULL,
    location_id INTEGER NOT NULL,
    subject VARCHAR(500) NOT NULL,
    description TEXT NOT NULL,
    status ticket_status NOT NULL DEFAULT 'open',
    priority ticket_priority NOT NULL DEFAULT 'medium',
    assigned_to INTEGER NOT NULL,
    sla_deadline TIMESTAMP NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT,
    closed_at TIMESTAMP,
    closed_by BIGINT,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    category VARCHAR(100) NOT NULL,
    module VARCHAR(100),
    FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE,
    FOREIGN KEY (assigned_to) REFERENCES users(id)
);

CREATE INDEX idx_ticket_customer_id ON tickets(customer_id);
CREATE INDEX idx_ticket_status ON tickets(status);
CREATE INDEX idx_ticket_priority ON tickets(priority);
CREATE INDEX idx_ticket_assigned_to ON tickets(assigned_to);
CREATE INDEX idx_ticket_created_at ON tickets(created_at);

-- ========== CREATE TICKET TIMELINE TABLE ==========
CREATE TABLE ticket_timelines (
    id SERIAL PRIMARY KEY,
    ticket_id INTEGER NOT NULL,
    type INTEGER NOT NULL,
    notes TEXT NOT NULL,
    file_id INTEGER,
    file_name VARCHAR(255),
    user_id INTEGER NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT NOT NULL DEFAULT 1,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (ticket_id) REFERENCES tickets(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE INDEX idx_ticket_timeline_ticket_id ON ticket_timelines(ticket_id);

-- ========== CREATE TRADEMARKS TABLE ==========
CREATE TABLE trademarks (
    id SERIAL PRIMARY KEY,
    customer_id INTEGER NOT NULL,
    location_id INTEGER NOT NULL,
    reg_name VARCHAR(255) NOT NULL,
    gst_number VARCHAR(15) NOT NULL,
    pincode VARCHAR(6) NOT NULL,
    city_id INTEGER NOT NULL,
    state_id INTEGER NOT NULL,
    country_id INTEGER,
    address_line1 VARCHAR(255) NOT NULL,
    address_line2 VARCHAR(255),
    contact_persons TEXT NOT NULL,
    emails TEXT NOT NULL,
    mobiles TEXT NOT NULL,
    tier_id INTEGER NOT NULL,
    shop_size_id INTEGER,
    registration_number VARCHAR(100),
    category VARCHAR(255),
    description TEXT,
    registration_date TIMESTAMP,
    expiry_date TIMESTAMP,
    is_active BOOLEAN NOT NULL DEFAULT true,
    remarks TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE,
    FOREIGN KEY (city_id) REFERENCES reference_entries(id),
    FOREIGN KEY (state_id) REFERENCES reference_entries(id),
    FOREIGN KEY (country_id) REFERENCES reference_entries(id),
    FOREIGN KEY (tier_id) REFERENCES reference_entries(id)
);

CREATE INDEX idx_trademark_customer_id ON trademarks(customer_id);
CREATE INDEX idx_trademark_is_active ON trademarks(is_active);

-- ========== CREATE LOCATIONS TABLE ==========
CREATE TABLE locations (
    id SERIAL PRIMARY KEY,
    customer_id INTEGER NOT NULL,
    code VARCHAR(100) NOT NULL,
    name VARCHAR(255) NOT NULL,
    reg_name VARCHAR(255) NOT NULL,
    pincode VARCHAR(6) NOT NULL,
    city_id INTEGER NOT NULL,
    state_id INTEGER NOT NULL,
    country_id INTEGER NOT NULL,
    address_line1 VARCHAR(255) NOT NULL,
    address_line2 VARCHAR(255) NOT NULL,
    contact_persons TEXT NOT NULL,
    emails TEXT NOT NULL,
    mobiles TEXT NOT NULL,
    shop_size_id INTEGER NOT NULL,
    tier_id INTEGER NOT NULL,
    is_primary BOOLEAN NOT NULL DEFAULT false,
    gst_number VARCHAR(15) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE,
    FOREIGN KEY (city_id) REFERENCES reference_entries(id),
    FOREIGN KEY (state_id) REFERENCES reference_entries(id),
    FOREIGN KEY (country_id) REFERENCES reference_entries(id),
    FOREIGN KEY (shop_size_id) REFERENCES reference_entries(id),
    FOREIGN KEY (tier_id) REFERENCES reference_entries(id)
);

CREATE INDEX idx_location_customer_id ON locations(customer_id);
CREATE INDEX idx_location_is_primary ON locations(is_primary);
CREATE INDEX idx_location_is_active ON locations(is_active);

-- ========== CREATE LOCATION TIMELINE TABLE ==========
CREATE TABLE location_timelines (
    id SERIAL PRIMARY KEY,
    location_id INTEGER NOT NULL,
    type INTEGER NOT NULL,
    notes TEXT NOT NULL,
    file_id INTEGER,
    file_name VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT NOT NULL DEFAULT 1,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    FOREIGN KEY (location_id) REFERENCES locations(id) ON DELETE CASCADE
);

CREATE INDEX idx_location_timeline_location_id ON location_timelines(location_id);

-- ========== CREATE REPORTS TABLE ==========
CREATE TABLE reports (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    module VARCHAR(100) NOT NULL,
    columns TEXT NOT NULL,
    filters TEXT NOT NULL,
    group_by VARCHAR(100),
    sort_by VARCHAR(100),
    query TEXT,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_by BIGINT NOT NULL DEFAULT 1,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_run TIMESTAMP NOT NULL,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT
);

CREATE INDEX idx_report_module ON reports(module);

-- ========== CREATE FILES TABLE (binary blobs, e.g. images) ==========
CREATE TABLE files (
    id BIGSERIAL PRIMARY KEY,
    is_factory BOOLEAN NOT NULL DEFAULT FALSE,
    content BYTEA NOT NULL,
    version INTEGER NOT NULL DEFAULT 1,
    created_by BIGINT,
    created_on TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT,
    modified_on TIMESTAMP WITHOUT TIME ZONE,
    attributes JSONB,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_suspended BOOLEAN NOT NULL DEFAULT FALSE,
    parent_id BIGINT,
    notes VARCHAR(255),
    type VARCHAR(100)
);

CREATE INDEX idx_files_parent_id ON files(parent_id);
CREATE INDEX idx_files_is_active ON files(is_active);

-- ========== CREATE SCHEDULER EVENTS TABLE ==========
CREATE TABLE scheduler_events (
    id SERIAL PRIMARY KEY,
    title VARCHAR(255) NOT NULL,
    description TEXT NOT NULL,
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP NOT NULL,
    attendees TEXT NOT NULL,
    location VARCHAR(255),
    type VARCHAR(50) NOT NULL,
    priority VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'scheduled',
    is_active BOOLEAN NOT NULL DEFAULT true,
    related_to_type VARCHAR(50),
    related_to_id INTEGER,
    created_by BIGINT NOT NULL DEFAULT 1,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modified_by BIGINT
);

CREATE INDEX idx_scheduler_event_start_time ON scheduler_events(start_time);
CREATE INDEX idx_scheduler_event_is_active ON scheduler_events(is_active);

-- ========== CREATE SEQUENCES ==========
-- PostgreSQL handles sequences automatically with SERIAL, but can be accessed if needed

-- ========== SUMMARY ==========
SELECT 'PostgreSQL CRM Database Schema Created Successfully!' as status;

-- Display table count
SELECT COUNT(*) as table_count FROM information_schema.tables 
WHERE table_schema = 'public' AND table_type = 'BASE TABLE';
