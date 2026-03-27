# PostgreSQL Configuration Guide for CRM Server

## Overview
This guide explains how to set up the CRM Server to work with PostgreSQL database.

## Prerequisites
- PostgreSQL 12+ installed
- .NET 8 SDK
- Npgsql NuGet package (PostgreSQL .NET provider)

## Step 1: Install PostgreSQL NuGet Package

Add to `crm-server.csproj`:
```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
```

Or via Package Manager:
```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.0
```

## Step 2: Update Connection String

### appsettings.json (Development)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=crm_db;Username=postgres;Password=postgres;Port=5432;Pooling=true;"
  }
}
```

### appsettings.Production.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=prod-server;Database=crm_db;Username=postgres;Password=strong_password;Port=5432;Pooling=true;SSL Mode=Require;"
  }
}
```

## Step 3: Update Program.cs

Replace SQL Server configuration with PostgreSQL:

```csharp
// OLD (SQL Server)
// var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
//     ?? "Server=(local);Database=CRM_DB;Integrated Security=true;";
// builder.Services.AddDbContext<CrmDbContext>(options =>
//     options.UseSqlServer(connectionString));

// NEW (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=crm_db;Username=postgres;Password=postgres;Port=5432;";
builder.Services.AddDbContext<CrmDbContext>(options =>
    options.UseNpgsql(connectionString));
```

## Step 4: Create Database

### Option A: Using psql command line
```bash
# Connect to PostgreSQL
psql -U postgres

# Create database
CREATE DATABASE crm_db;

# Exit
\q
```

### Option B: Using pgAdmin GUI
1. Right-click on "Databases"
2. Click "Create" → "Database"
3. Enter name: `crm_db`
4. Click "Save"

## Step 5: Apply Migrations

### Using Entity Framework CLI
```bash
# Add migration
dotnet ef migrations add InitialCreate --context CrmDbContext

# Apply migration
dotnet ef database update --context CrmDbContext
```

### Or run SQL scripts directly
```bash
# Connect to PostgreSQL
psql -U postgres -d crm_db

# Run schema script
\i Database/01_CreateSchema_PostgreSQL.sql

# Run seed script
\i Database/02_SeedData_PostgreSQL.sql

# Exit
\q
```

### Schema patches (existing databases)

If the Scheduler API returns **400** with PostgreSQL error **`column s.status does not exist`**, your `scheduler_events` table predates the `status` column. Apply:

```bash
psql -U postgres -d crm -f Database/07_AlterSchedulerEvents_AddStatus_PostgreSQL.sql
```

(Use your actual database name and credentials; in **pgAdmin**, open **Query Tool** and run the contents of that file.)

## Step 6: Verify Installation

### Using psql
```bash
psql -U postgres -d crm_db

# List tables
\dt

# Count records
SELECT COUNT(*) FROM customers;
SELECT COUNT(*) FROM reference_entries;

# Exit
\q
```

### Or using application
```bash
dotnet run

# Navigate to https://localhost:5001/api/customers
# Should return customer list
```

---

## Key Differences: PostgreSQL vs SQL Server

### 1. Data Types
| SQL Server | PostgreSQL |
|-----------|-----------|
| NVARCHAR | VARCHAR/TEXT |
| INT IDENTITY | SERIAL |
| DATETIME2 | TIMESTAMP |
| BIT | BOOLEAN |
| DECIMAL | DECIMAL/NUMERIC |

### 2. Enums
PostgreSQL supports native ENUM types:
```sql
CREATE TYPE customer_type AS ENUM ('lead', 'prospect', 'customer');
CREATE TYPE ticket_status AS ENUM ('open', 'in_progress', 'waiting', 'resolved', 'closed');
```

### 3. Indexes
PostgreSQL automatically creates indexes for:
- Primary keys (UNIQUE)
- Foreign keys
- Explicitly created indexes

### 4. Null Handling
PostgreSQL treats NULL differently in some functions:
```sql
-- COALESCE works the same
-- ISNULL → COALESCE
-- GETDATE() → CURRENT_TIMESTAMP
-- GETUTCDATE() → CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
```

---

## PostgreSQL Connection String Options

### Basic
```
Host=localhost;Database=crm_db;Username=postgres;Password=postgres;Port=5432;
```

### With Connection Pooling
```
Host=localhost;Database=crm_db;Username=postgres;Password=postgres;Port=5432;Pooling=true;Maximum Pool Size=20;
```

### With SSL (Production)
```
Host=prod-server;Database=crm_db;Username=postgres;Password=password;Port=5432;SSL Mode=Require;
```

### With Timeout
```
Host=localhost;Database=crm_db;Username=postgres;Password=postgres;Port=5432;Command Timeout=30;
```

---

## PostgreSQL Specific Queries in Services

### Pagination Query
```csharp
// PostgreSQL with LIMIT/OFFSET
var customers = await _context.Customers
    .AsNoTracking()
    .OrderByDescending(c => c.CreatedAt)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

### Search Query
```csharp
// PostgreSQL with ILIKE (case-insensitive)
var customers = await _context.Customers
    .Where(c => EF.Functions.ILike(c.Company, $"%{searchTerm}%") ||
                EF.Functions.ILike(c.Email, $"%{searchTerm}%"))
    .ToListAsync();
```

### Full-Text Search (Advanced)
```csharp
// PostgreSQL full-text search
var customers = await _context.Customers
    .FromSqlInterpolated($@"
        SELECT * FROM customers 
        WHERE to_tsvector('english', company || ' ' || name || ' ' || email) @@ 
              plainto_tsquery('english', {searchTerm})
        ORDER BY created_at DESC
    ")
    .ToListAsync();
```

### Date Filtering
```csharp
// PostgreSQL date functions
var recentCustomers = await _context.Customers
    .Where(c => c.CreatedAt > DateTime.UtcNow.AddDays(-30))
    .OrderByDescending(c => c.CreatedAt)
    .ToListAsync();
```

### Aggregation
```csharp
// PostgreSQL GROUP BY
var summary = await _context.Services
    .GroupBy(s => s.CustomerId)
    .Select(g => new
    {
        CustomerId = g.Key,
        TotalServices = g.Count(),
        TotalBudget = g.Sum(s => s.BudgetAmount)
    })
    .ToListAsync();
```

### JSON Operations (PostgreSQL specific)
```csharp
// If using JSON columns in future
// var data = EF.Functions.JsonExtract(entity.Data, "$.key");
```

---

## Common PostgreSQL Commands

### Connect to Database
```bash
psql -U postgres -d crm_db -h localhost -p 5432
```

### List Tables
```sql
\dt
```

### Show Table Structure
```sql
\d table_name
```

### List Indexes
```sql
\di
```

### Check Database Size
```sql
SELECT pg_database.datname, 
       pg_size_pretty(pg_database_size(pg_database.datname)) 
FROM pg_database 
ORDER BY pg_database_size DESC;
```

### Check Table Sizes
```sql
SELECT schemaname, 
       tablename, 
       pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) 
FROM pg_tables 
WHERE schemaname != 'pg_catalog' 
ORDER BY pg_total_relation_size DESC;
```

### View Slow Queries
```sql
SELECT query, calls, mean_time, total_time 
FROM pg_stat_statements 
ORDER BY mean_time DESC 
LIMIT 10;
```

---

## Performance Tuning for PostgreSQL

### 1. Connection Pooling Configuration
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=crm_db;Username=postgres;Password=postgres;Pooling=true;Maximum Pool Size=20;Minimum Pool Size=5;Connection Lifetime=300;"
  }
}
```

### 2. Query Optimization
```sql
-- Add ANALYZE to execution plan
EXPLAIN ANALYZE
SELECT * FROM customers WHERE type = 'customer' ORDER BY created_at DESC;
```

### 3. Index Maintenance
```sql
-- Vacuum and analyze
VACUUM ANALYZE;

-- Rebuild specific index
REINDEX INDEX idx_customer_email;

-- Check index bloat
SELECT schemaname, tablename, indexname, 
       round(100.0 * (pg_relation_size(idx) - pg_relation_size(rel)) / pg_relation_size(rel), 2) AS idx_bloat_ratio
FROM (
  SELECT schemaname, tablename, indexname, idx, rel
  FROM (
    SELECT schemaname, tablename, indexname, 
           idx, rel, 2 + ceiling(rel_len / (bs - page_hdr))::float AS otta
    FROM (
      SELECT schemaname, tablename, indexname, rel, bs, rel_len, page_hdr, idx
      FROM (
        SELECT schemaname, tablename, indexname, idx, rel, bs, rel_len, page_hdr
        FROM (
          SELECT schemaname, tablename, indexname, i_rel, i_size, rel_len, 
                 page_hdr, idx_len - i_size * 8192 AS idx
          FROM (
            SELECT schemaname, tablename, indexname, idx, rel, 
                   pg_relation_size(idx) / 8192 AS i_size, i_rel, 
                   pg_relation_size(rel) AS rel_len, 28 AS page_hdr, 8192 AS bs, 
                   pg_relation_size(idx) - pg_relation_size(i_rel) AS idx_len
            FROM pg_stat_user_indexes
          ) a
        ) b
      ) c
    ) d
  ) e
) f;
```

---

## Backup and Restore

### Backup Database
```bash
# Full backup
pg_dump -U postgres -d crm_db -h localhost > crm_db_backup.sql

# Custom format (compressed)
pg_dump -U postgres -d crm_db -Fc > crm_db_backup.dump
```

### Restore Database
```bash
# From SQL
psql -U postgres -d crm_db -h localhost < crm_db_backup.sql

# From custom format
pg_restore -U postgres -d crm_db -h localhost crm_db_backup.dump
```

### Automated Backup (Linux/Mac)
```bash
#!/bin/bash
BACKUP_DIR="/backups/postgresql"
DB_NAME="crm_db"
BACKUP_FILE="$BACKUP_DIR/crm_db_$(date +%Y%m%d_%H%M%S).dump"

pg_dump -U postgres -d $DB_NAME -Fc > $BACKUP_FILE

# Keep only last 30 days
find $BACKUP_DIR -name "crm_db_*.dump" -mtime +30 -delete
```

---

## Monitoring PostgreSQL

### Check Active Connections
```sql
SELECT datname, count(*) 
FROM pg_stat_activity 
GROUP BY datname;
```

### Kill Long-Running Queries
```sql
SELECT pid, usename, query, query_start
FROM pg_stat_activity
WHERE query_start < NOW() - INTERVAL '30 minutes'
AND state = 'active';

-- Kill specific query
SELECT pg_terminate_backend(pid) FROM pg_stat_activity 
WHERE query_start < NOW() - INTERVAL '30 minutes' AND state = 'active';
```

### Monitor Index Usage
```sql
SELECT schemaname, tablename, indexname, idx_scan, idx_tup_read, idx_tup_fetch
FROM pg_stat_user_indexes
ORDER BY idx_scan DESC;
```

---

## Troubleshooting

### Connection Issues

**Error: "FATAL: Ident authentication failed"**
```bash
# Edit postgresql.conf
# Change: local   all             all                                     ident
# To:     local   all             all                                     md5
```

**Error: "FATAL: role 'postgres' does not exist"**
```bash
# Create role
psql -U postgres -c "CREATE ROLE postgres WITH LOGIN SUPERUSER PASSWORD 'password';"
```

### Query Issues

**Slow Query**
```sql
-- Use EXPLAIN to analyze
EXPLAIN (ANALYZE, BUFFERS) 
SELECT * FROM customers WHERE type = 'customer';
```

**Wrong Result Type**
```csharp
// Ensure correct mapping in DbContext
modelBuilder.Entity<Customer>()
    .Property(c => c.Type)
    .HasConversion(v => v.ToString(), v => Enum.Parse<CustomerType>(v));
```

---

## Environment-Specific Setup

### Development
- Host: localhost
- Port: 5432 (default)
- User: postgres
- Database: crm_db_dev
- Connection Pooling: Min 5, Max 20

### Staging
- Host: staging-db.example.com
- Port: 5432
- User: crm_staging
- Database: crm_db_staging
- Connection Pooling: Min 10, Max 50

### Production
- Host: prod-db.example.com
- Port: 5432
- User: crm_prod (with restricted privileges)
- Database: crm_db_prod
- Connection Pooling: Min 20, Max 100
- SSL Mode: Require
- Read Replicas: 2+ for scaling

---

## Migration Path from SQL Server to PostgreSQL

### Using Entity Framework Migrations
```bash
# 1. Add EF migration for PostgreSQL
dotnet ef migrations add PostgreSQLVersion

# 2. Update database
dotnet ef database update

# 3. Verify all tables created
psql -d crm_db -c "\dt"
```

### Data Migration
```sql
-- Export from SQL Server (MSSQL)
-- Import to PostgreSQL using pg_restore or psql
```

---

**PostgreSQL Version**: 12+  
**Npgsql Version**: 8.0.0+  
**Entity Framework Core**: 8.0.0+  
**.NET**: 8.0+
