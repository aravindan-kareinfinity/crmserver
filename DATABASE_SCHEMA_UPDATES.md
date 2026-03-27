# PostgreSQL Schema Updates

## Summary of Changes

All tables in the PostgreSQL schema have been updated with the following standardized changes:

### 1. **Added Audit Columns to All Tables**
- `modified_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
- `modified_by` VARCHAR(255)

These columns track when records are last modified and by whom, providing complete audit trail capabilities.

---

## Table-Specific Changes

### **Users Table**
- **Changed columns:**
  - `name` → Split into `first_name` (VARCHAR(255)) and `last_name` (VARCHAR(255))
  - `status` (VARCHAR) → `is_active` (BOOLEAN)
  - `avatar` column → Removed
- **Added columns:**
  - `user_id` VARCHAR(100) NOT NULL UNIQUE - Login identifier for users
  - `created_by`, `modified_at`, `modified_by` - Audit columns
- **Indexes added:**
  - `idx_user_user_id` on user_id for quick login lookups

**Rationale:** Separating first and last names provides better data flexibility. The `user_id` field represents a unique login identifier distinct from the database `id`. Boolean `is_active` is more consistent and efficient than string status.

---

### **Roles Table**
- **Changed columns:**
  - `user_count` → Now nullable (INTEGER) - No longer requires a NOT NULL constraint
- **Added columns:**
  - `created_at`, `created_by`, `modified_at`, `modified_by` - Audit columns

**Rationale:** When roles are created, user count is unknown. It should be nullable and updated only when users are assigned to roles.

---

### **Customers Table**
- **Changed columns:**
  - `mobile` VARCHAR(20) → VARCHAR(10) - Standard mobile number length in India (10 digits)
  - `pincode` VARCHAR(10) → VARCHAR(6) - Standard pincode length in India (6 digits)
  - `gst_number` VARCHAR(20) → VARCHAR(15) - Standard GST number length (15 characters)
  - `type` (customer_type ENUM) → `type_id` INTEGER NOT NULL with FK to reference_entries
  - `status` (VARCHAR) → `is_active` BOOLEAN
- **Removed columns:**
  - `company` - Not in the structure
  - `name` - Not in the structure
- **Indexes updated:**
  - `idx_customer_type` → `idx_customer_type_id`
  - `idx_customer_status` → `idx_customer_is_active`
  - Removed `idx_customer_company` (column removed)

**Rationale:** Using reference IDs for customer type provides better flexibility and consistency with the reference data system. Boolean flags are more efficient than string statuses.

---

### **Services Table**
- **Changed columns:**
  - `status` (VARCHAR) → `is_active` BOOLEAN
- **Indexes updated:**
  - `idx_service_status` → `idx_service_is_active`

**Notes:** The `due_date2` column already exists - no changes needed.

---

### **Locations Table** (formerly Branches)
- **Table renamed** from `branches` to `locations`
- **Timeline table renamed** from `branch_timelines` to `location_timelines`
- **Changed columns:**
  - `pincode` VARCHAR(10) → VARCHAR(6)
  - `gst_number` VARCHAR(20) → VARCHAR(15)
  - `mobiles` TEXT type - remains, but should contain 10-digit numbers
  - `status` (VARCHAR) → `is_active` BOOLEAN
- **Indexes updated:**
  - `idx_branch_*` → `idx_location_*`
  - `idx_branch_status` → `idx_location_is_active`

**Rationale:** "Locations" is a more accurate semantic term for branch locations of a customer. Standardized field lengths for Indian business requirements.

---

### **Timeline Tables** (All)
All timeline tables have been updated consistently:
- `customer_timelines`
- `invoice_timelines`
- `investment_timelines`
- `implementation_timelines`
- `ticket_timelines`
- `location_timelines`

**Changes to all timeline tables:**
- Added `is_active` BOOLEAN NOT NULL DEFAULT true
- Added `modified_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
- Added `modified_by` VARCHAR(255)

---

### **Invoices Table**
- **Changed columns:**
  - `status` (implicit) → `is_active` BOOLEAN
- **Added columns:**
  - Audit columns: `modified_at`, `modified_by`

---

### **Investments Table**
- **Changed columns:**
  - Implicit status → `is_active` BOOLEAN
- **Added columns:**
  - Audit columns: `modified_at`, `modified_by`

---

### **Trademarks Table**
- **Changed columns:**
  - `pincode` VARCHAR(10) → VARCHAR(6)
  - `gst_number` VARCHAR(20) → VARCHAR(15)
  - `status` (trademark_status ENUM) → `is_active` BOOLEAN
- **Indexes updated:**
  - `idx_trademark_status` → `idx_trademark_is_active`

---

### **Tickets Table**
- **Changed columns:**
  - Implied status → `is_active` BOOLEAN
- **Added columns:**
  - Audit columns: `modified_at`, `modified_by`

---

### **Payments Table**
- **Changed columns:**
  - Implicit status → `is_active` BOOLEAN
- **Added columns:**
  - Audit columns: `created_at`, `created_by`, `modified_at`, `modified_by`

---

### **Reports Table**
- **Changed columns:**
  - Implicit status → `is_active` BOOLEAN
- **Added columns:**
  - Audit columns: `modified_at`, `modified_by`

---

### **Scheduler Events Table**
- **Changed columns:**
  - `status` (VARCHAR) → `is_active` BOOLEAN
  - Removed `status` VARCHAR column
- **Added columns:**
  - Audit columns: `modified_at`, `modified_by`
- **Indexes updated:**
  - `idx_scheduler_event_status` → `idx_scheduler_event_is_active`

---

## DROP TABLE Statements Updated

The schema now drops:
- `location_timelines` (instead of `branch_timelines`)
- `locations` (instead of `branches`)

---

## Standard Patterns Applied

### Boolean Status Pattern
All tables now use `is_active BOOLEAN NOT NULL DEFAULT true` instead of string-based status columns. This:
- Reduces storage size
- Improves query performance
- Provides type safety
- Enables efficient indexing

### Audit Trail Pattern
All tables include:
- `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
- `created_by` VARCHAR(255)
- `modified_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
- `modified_by` VARCHAR(255)

This enables complete audit trails and change tracking.

### Field Length Standards (India-specific)
- **Mobile**: VARCHAR(10) - Standard 10-digit Indian mobile numbers
- **Pincode**: VARCHAR(6) - Standard 6-digit Indian postal codes
- **GST Number**: VARCHAR(15) - Standard 15-character GST identifier

### Reference-Based Types
- Customer `type` now uses `type_id` (INTEGER) with FK to `reference_entries`
- This provides flexibility to add/modify customer types without schema changes
- Maintains data integrity through referential constraints

---

## Migration Recommendations

1. **For existing deployments:**
   - Backup existing database
   - Create migration scripts to:
     - Rename tables (branches → locations)
     - Add new columns with defaults
     - Migrate data as needed
     - Update foreign key references
     - Migrate enum values to reference entries
     - Update indexes

2. **For new deployments:**
   - Use the updated schema directly
   - Ensure reference_entries are populated with customer types and other lookup values

---

## Files Updated
- `E:\project\crm\crm-server\Database\01_CreateSchema_PostgreSQL.sql`

---

## Next Steps
1. Review seed data script to ensure alignment with new schema
2. Update Entity Framework models to reflect schema changes
3. Update all services and repositories to use new columns
4. Update DTOs to include audit columns
5. Update API endpoints for customer type to use reference IDs
6. Update any stored procedures or custom queries
