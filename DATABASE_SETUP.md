# CRM Server - Database Setup Guide

## Overview
This guide explains the complete database schema, how to set it up, and how to work with it.

## Database Architecture

### Database Name
`CRM_DB`

### Total Tables: 28
- 5 timeline tables (audit trails)
- 20+ core entity tables
- Support for 60+ reference/lookup values

## Database Schema

### Core Entity Tables

#### 1. **ReferenceEntries** (Master Lookup Table)
Purpose: Stores all dropdown/reference values used across the system

**Columns:**
```sql
Id (PK)              - Primary Key
Category (Index)     - e.g., "Business Type", "Industry", "City"
Label               - Display text (e.g., "Startup", "Mumbai")
Value               - Internal value (e.g., "startup", "mumbai")
IsActive (Index)    - Whether this reference is active
SortOrder           - Display order
RequiresImplementation - For Service Types
```

**Example Data:**
```
Category: "Business Type"
├─ Startup
├─ SME
└─ Enterprise

Category: "Industry"
├─ Technology
├─ Finance
└─ Healthcare

Category: "Payment Mode"
├─ Bank Transfer
├─ UPI
└─ Cash
```

#### 2. **Users**
Purpose: Team member accounts

**Columns:**
- Id, Name, Email (Unique), Role, Status
- LastLogin, CreatedAt, Avatar

#### 3. **Roles**
Purpose: Role definitions with permissions

**Columns:**
- Id, Name (Unique), Description
- Permissions (comma-separated string)
- UserCount

#### 4. **Customers**
Purpose: Core entity - Leads, Prospects, Customers

**Columns:**
- Id, Code (SHA hash), Company, RegName, Name
- Contact: Mobile, Email
- Address: AddressLine1, AddressLine2, CityId, StateId, CountryId, Pincode
- Classification: BusinessTypeId, IndustryId, ShopSizeId, TierId
- Identifiers: GstNumber, ContactPersons, Emails, Mobiles
- Status: Type (lead/prospect/customer), Status
- Metadata: TotalLocations, TotalTradeNames
- Timestamps: CreatedAt, ConvertedAt

**Foreign Keys:**
- BusinessTypeId → ReferenceEntries
- IndustryId → ReferenceEntries
- CityId, StateId, CountryId → ReferenceEntries
- ShopSizeId, TierId → ReferenceEntries

#### 5. **Services** (Transactions)
Purpose: Billable services provided to customers

**Columns:**
- Id, CustomerId (FK)
- LocationId, TradeNameId
- ServiceTypeId (FK), FrequencyId (FK), DueDate, DueMonth
- Implementation: ImplementationRequired, ImplementationStatusId, ImplementationStageId
- Implementation Tracking: ImplementationStartedAt, ImplementationCompletedAt
- Project: ProjectTitle, ProjectManagerId (FK), StartDate, EndDate
- Budget: BudgetAmount, ProgressPercentage
- Tax: TaxId (FK)
- Metadata: Status, Notes, CreatedAt

#### 6. **Invoices**
Purpose: Billing records

**Columns:**
- Id, InvoiceNumber (Unique)
- CustomerId (FK), ServiceId (FK), StaffId (FK)
- PaymentModeId (FK), PaymentStatusId (FK)
- Financial: Receivable, Received
- Subscription: SubscriptionStartAt, SubscriptionEndAt
- Metadata: PaidAt, PaidBy, CreatedAt

#### 7. **Investments**
Purpose: Capital investments in customers

**Columns:**
- Id, CustomerId (FK), LocationId
- Amount, InvestmentTypeId (FK), StaffId (FK)
- Notes, CreatedAt

#### 8. **Tickets**
Purpose: Support ticket management

**Columns:**
- Id, CustomerId (FK), LocationId
- Subject, Description
- Status (open, in_progress, waiting, resolved, closed)
- Priority (critical, high, medium, low)
- AssignedTo (FK)
- SlaDeadline, Category
- ClosedAt, ClosedBy
- CreatedAt

#### 9. **Branches** (Locations)
Purpose: Customer locations/offices

**Columns:**
- Id, CustomerId (FK), Code, Name, RegName
- Address: AddressLine1, AddressLine2, CityId, StateId, CountryId, Pincode
- Contact: ContactPersons, Emails, Mobiles
- Classification: ShopSizeId (FK), TierId (FK)
- Metadata: IsPrimary, GstNumber, Status
- References: CityId, StateId, CountryId, ShopSizeId, TierId (all FK to ReferenceEntries)

#### 10. **Trademarks**
Purpose: Brand registration and management

**Columns:**
- Id, CustomerId (FK), LocationId
- RegName, GstNumber, Pincode
- Address: CityId, StateId, CountryId, AddressLine1, AddressLine2
- Contact: ContactPersons, Emails, Mobiles
- Registration: RegistrationNumber, Category, Description
- Dates: RegistrationDate, ExpiryDate
- Status (active, expired, pending, rejected)
- Remarks, CreatedAt

#### 11. **Ledgers** (Billing Entity)
Purpose: GST mapping and billing entity information

**Columns:**
- Id, CustomerId (FK), Status (active, inactive)
- Organization: Company, ContactName, Mobile, Email
- Address: CityId, StateId, CountryId, Pincode, AddressLine1, AddressLine2
- Identifiers: IndustryId, GstNumber
- Timestamps: CreatedAt, CreatedBy

#### 12. **Payments**
Purpose: Payment tracking for services

**Columns:**
- Id, ServiceId (FK)
- AmountRaised, AmountReceived
- DateReceived, PaymentModeId (FK)
- InvoiceId (FK - optional)

### Timeline Tables (Audit Trails)

Each of these tracks changes to the main entity:

1. **CustomerTimelines** - Customer activity history
2. **InvoiceTimelines** - Invoice changes and payments
3. **TicketTimelines** - Support interactions
4. **BranchTimelines** - Location changes
5. **LedgerTimelines** - Billing entity changes
6. **InvestmentTimelines** - Investment tracking
7. **ImplementationTimelines** - Implementation progress

**Common Columns:**
```sql
Id (PK)
ParentId (FK to parent entity)  -- e.g., CustomerId, InvoiceId
Type (int)  -- 1=SYSTEM, 2=UPDATE, 3=TEXT, 6=FILE, 7=FILE
Notes (nvarchar)
FileId, FileName (optional)
CreatedAt (datetime)
CreatedBy (nvarchar)
```

### Supporting Tables

#### **Reports**
Purpose: Saved reports and queries

**Columns:**
- Id, Name, Module, Columns, Filters
- GroupBy, SortBy
- CreatedBy, CreatedAt, LastRun

#### **SchedulerEvents**
Purpose: Meetings and events

**Columns:**
- Id, Title, Description
- StartTime, EndTime
- Attendees (comma-separated user IDs)
- Location, Type, Priority, Status
- RelatedTo_Type, RelatedTo_Id (polymorphic reference)
- CreatedBy, CreatedAt

## Data Flow Diagram

```
Customer (Core Entity)
    ├── Services (Transactions)
    │   ├── Invoices (Billing)
    │   │   └── Payments (Payment Records)
    │   └── ImplementationTimelines (Progress)
    ├── Branches (Locations)
    ├── Tickets (Support)
    ├── Trademarks (Brand Registrations)
    ├── Investments (Capital)
    └── Ledgers (Billing Entities)

All entities have Timeline tables for audit trails.
```

## Key Relationships

### Customer → Service → Invoice → Payment
```
Customer (1) ──→ (Many) Services
Service (1) ──→ (Many) Invoices
Service (1) ──→ (Many) Payments
Invoice (1) ──→ (Many) InvoiceTimelines
```

### Customer → Branches
```
Customer (1) ──→ (Many) Branches
Branch (1) ──→ (Many) BranchTimelines
```

### Customer → Tickets
```
Customer (1) ──→ (Many) Tickets
Ticket (1) ──→ (Many) TicketTimelines
```

### Customer → Everything Else
```
Customer (1) ──→ (Many) Trademarks
Customer (1) ──→ (Many) Ledgers
Customer (1) ──→ (Many) Investments
```

## Reference Data Categories

### 60+ Reference Entries organized by category:

1. **Business Type** (3 values)
   - Startup, SME, Enterprise

2. **Industry** (8 values)
   - Technology, Finance, Healthcare, Manufacturing, etc.

3. **Location**
   - Cities: Mumbai, Bangalore, Delhi
   - States: Maharashtra, Karnataka, Delhi NCR
   - Countries: India, USA

4. **Classification**
   - Shop Size (6 values): Micro to Hypermart
   - City Tier (3 values): Tier I, II, III

5. **Service Configuration**
   - Service Types (4 values): SaaS, ERP, AMC, Implementation
   - Services (9 values): License, AMC, Customization, Training, etc.
   - Frequency (3 values): Monthly, Yearly, One-Time
   - Implementation Stage (5 values): Discovery, Planning, Execution, Review, Handover
   - Implementation Status (2 values): In Progress, Completed

6. **Financial**
   - Payment Mode (6 values): Bank Transfer, UPI, Cash, Cheque, etc.
   - Payment Status (4 values): Paid, Pending, Overdue, Failed
   - Tax (3 values): GST 18%, GST 12%, No Tax

7. **Investment**
   - Investment Type (3 values): Equity, Debt, Convertible Note

8. **Support**
   - Ticket Category (4 values): Bug, Feature Request, Performance, Billing

9. **Business**
   - Business Nature (3 values): Retail, Manufacturer, Large Format

10. **Other**
    - Lead Source (3 values): Website, Referral, Webinar
    - Inventory Value Unit (2 values): Lakhs, Crores

## Setup Steps

### 1. Create Database
```sql
CREATE DATABASE CRM_DB;
USE CRM_DB;
```

### 2. Run Schema Script
Execute `01_CreateSchema.sql` to create all tables and indexes

### 3. Run Seed Data Script
Execute `02_SeedData.sql` to populate:
- 60+ reference entries
- 5 sample users
- 6 sample customers
- 5 services with invoices and payments
- Sample timelines

### 4. Verify Data
```sql
-- Check tables created
SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo';

-- Check reference data
SELECT COUNT(*), Category FROM ReferenceEntries GROUP BY Category;

-- Check sample data
SELECT COUNT(*) FROM Customers;  -- Should be 6
SELECT COUNT(*) FROM Services;   -- Should be 5
SELECT COUNT(*) FROM Tickets;    -- Should be 5
```

## Indexes

### Clustered Indexes
- Every table has PK clustered index on Id

### Non-Clustered Indexes (50+)
```sql
-- Search indexes
IX_Customer_Email
IX_Customer_Type
IX_Customer_Company
IX_Customer_Status
IX_User_Email

-- Foreign key indexes
IX_Service_CustomerId
IX_Invoice_CustomerId
IX_Invoice_ServiceId
IX_Ticket_CustomerId
IX_Branch_CustomerId

-- Filtering indexes
IX_Ticket_Status
IX_Ticket_Priority
IX_ReferenceEntry_Category
IX_ReferenceEntry_IsActive

-- Date range indexes
IX_Customer_CreatedAt
IX_Invoice_CreatedAt
IX_Ticket_CreatedAt
IX_Payment_DateReceived

-- Other
IX_ImplementationTimeline_Status
IX_Trademark_Status
IX_Branch_IsPrimary
```

## Constraints

### Primary Keys
Every table has an `Id` IDENTITY(1,1) PRIMARY KEY

### Foreign Keys
```sql
-- Customer relationships
FK_Service_Customer → Customers(Id) ON DELETE CASCADE
FK_Branch_Customer → Customers(Id) ON DELETE CASCADE
FK_Ticket_Customer → Customers(Id) ON DELETE CASCADE
FK_Invoice_Customer → Customers(Id) ON DELETE CASCADE
FK_Trademark_Customer → Customers(Id) ON DELETE CASCADE
FK_Ledger_Customer → Customers(Id) ON DELETE CASCADE
FK_Investment_Customer → Customers(Id) ON DELETE CASCADE

-- Service relationships
FK_Invoice_Service → Services(Id) ON DELETE CASCADE
FK_Payment_Service → Services(Id) ON DELETE CASCADE
FK_ImplementationTimeline_Service → Services(Id) ON DELETE CASCADE
FK_ImplementationAssignment_Service → Services(Id) ON DELETE CASCADE

-- Reference relationships
All *Id fields reference ReferenceEntries(Id)

-- Timeline relationships
All Timeline tables reference parent entity ON DELETE CASCADE
```

### Unique Constraints
```sql
UK_ReferenceEntry_Category_Value
UK_InvoiceNumber
UK_User_Email
UK_Role_Name
```

## Backup and Maintenance

### Regular Backups
```sql
BACKUP DATABASE CRM_DB
TO DISK = 'C:\Backups\CRM_DB.bak'
WITH INIT, COMPRESSION;
```

### Index Maintenance
```sql
-- Rebuild fragmented indexes
ALTER INDEX ALL ON Customers REBUILD;

-- Update statistics
UPDATE STATISTICS Customers;
```

### Clean Old Timeline Data
```sql
DELETE FROM CustomerTimelines
WHERE CreatedAt < DATEADD(YEAR, -1, GETDATE());
```

## Performance Considerations

1. **Pagination**: Always use with list queries
2. **Indexes**: Use WHERE clauses that match indexed columns
3. **Joins**: Keep joins to FK relationships
4. **Timeline Tables**: Archive old data periodically
5. **Reference Data**: Cache in application layer

## Connection String

### Development
```
Server=(local);Database=CRM_DB;Integrated Security=true;TrustServerCertificate=True
```

### Production
```
Server=prod-server;Database=CRM_DB;Integrated Security=false;User Id=sa;Password=strong;Encrypt=true;
```

## Sample Queries

### Get Customer with Services
```sql
SELECT c.*, s.ProjectTitle, s.Status
FROM Customers c
LEFT JOIN Services s ON c.Id = s.CustomerId
WHERE c.Type = 'customer'
ORDER BY c.Company;
```

### Get Invoices by Month
```sql
SELECT MONTH(CreatedAt) AS Month, COUNT(*) AS InvoiceCount, SUM(Receivable) AS Total
FROM Invoices
WHERE YEAR(CreatedAt) = 2024
GROUP BY MONTH(CreatedAt);
```

### Get Pending Payments
```sql
SELECT p.*, c.Company, s.ProjectTitle
FROM Payments p
JOIN Services s ON p.ServiceId = s.Id
JOIN Customers c ON s.CustomerId = c.Id
WHERE p.AmountReceived < p.AmountRaised;
```

### Get Overdue Tickets
```sql
SELECT t.*
FROM Tickets t
WHERE t.Status != 'closed'
AND t.SlaDeadline < GETUTCDATE();
```

## Database Views (Optional)

You can create views for common queries:

```sql
-- Customer summary
CREATE VIEW vw_CustomerSummary AS
SELECT 
    c.Id, c.Company, c.Name, c.Type, c.Status,
    COUNT(DISTINCT s.Id) AS ServiceCount,
    COUNT(DISTINCT b.Id) AS BranchCount,
    COUNT(DISTINCT t.Id) AS TicketCount
FROM Customers c
LEFT JOIN Services s ON c.Id = s.CustomerId
LEFT JOIN Branches b ON c.Id = b.CustomerId
LEFT JOIN Tickets t ON c.Id = t.CustomerId
GROUP BY c.Id, c.Company, c.Name, c.Type, c.Status;

-- Invoice summary
CREATE VIEW vw_InvoiceSummary AS
SELECT 
    c.Id, c.Company,
    COUNT(i.Id) AS InvoiceCount,
    SUM(i.Receivable) AS TotalReceivable,
    SUM(i.Received) AS TotalReceived
FROM Customers c
LEFT JOIN Invoices i ON c.Id = i.CustomerId
GROUP BY c.Id, c.Company;
```

---

**Database Size Estimate**: ~10MB with sample data (will grow with production usage)  
**Backup Schedule**: Daily  
**Maintenance**: Weekly index maintenance recommended  
**Monitoring**: Track table sizes monthly
