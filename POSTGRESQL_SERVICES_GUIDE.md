# PostgreSQL Services Organization Guide

## Overview
This guide explains how services are organized for PostgreSQL in the CRM Server, following a modular pattern similar to the frontend architecture.

## File Structure

```
Services/
├── PostgreSQL/                          # PostgreSQL-specific services
│   ├── CustomerService.cs              # Customer management
│   ├── InvoiceService.cs               # Invoice management  
│   ├── TicketService.cs                # Ticket management
│   ├── ServiceService.cs               # Service management
│   ├── BranchService.cs                # Branch management
│   ├── PaymentService.cs               # Payment management
│   ├── ReferenceService.cs             # Reference/lookup management
│   ├── LedgerService.cs                # Ledger management
│   ├── TrademarkService.cs             # Trademark management
│   └── InvestmentService.cs            # Investment management
│
└── Shared/                              # Shared utilities
    └── BaseService.cs                   # Base service class (optional)
```

## Service Architecture

### 1. Interface-Based Design
Each service has an interface defining the contract:

```csharp
public interface ICustomerService
{
    Task<ApiResponse<PaginatedResponse<CustomerResponseDto>>> GetAll(int pageNumber = 1, int pageSize = 10, string? searchTerm = null);
    Task<ApiResponse<CustomerResponseDto>> GetById(int id);
    Task<ApiResponse<List<CustomerResponseDto>>> GetByType(string type);
    Task<ApiResponse<CustomerResponseDto>> Create(CreateCustomerDto dto);
    Task<ApiResponse<CustomerResponseDto>> Update(int id, UpdateCustomerDto dto);
    Task<ApiResponse<bool>> Delete(int id);
}
```

### 2. PostgreSQL-Specific Implementation
Each service includes PostgreSQL-specific queries and optimizations:

**Case-Insensitive Search (ILIKE)**
```csharp
query = query.Where(c => 
    EF.Functions.ILike(c.Company, $"%{searchTerm}%") ||
    EF.Functions.ILike(c.Name, $"%{searchTerm}%"));
```

**Pagination (LIMIT/OFFSET)**
```csharp
.Skip((pageNumber - 1) * pageSize)
.Take(pageSize)
```

**Aggregation (SUM, COUNT, AVG)**
```csharp
var total = await _context.Invoices
    .Where(i => i.CustomerId == customerId)
    .SumAsync(i => (decimal?)i.Receivable) ?? 0;
```

**ENUM Type Filtering**
```csharp
.Where(t => t.Status.ToString() == status)
```

## Service Implementations

### CustomerService

**Features:**
- Get all customers with pagination and search
- Get customer by ID
- Get customers by type (lead/prospect/customer)
- Create, update, delete customers

**PostgreSQL Queries:**
```sql
-- Search with ILIKE (case-insensitive)
SELECT * FROM customers 
WHERE company ILIKE '%search%' 
   OR name ILIKE '%search%'
ORDER BY created_at DESC
LIMIT 10 OFFSET 0;

-- Get by type using ENUM
SELECT * FROM customers 
WHERE type = 'customer'::customer_type
ORDER BY created_at DESC;

-- Count total
SELECT COUNT(*) FROM customers;
```

---

### InvoiceService

**Features:**
- Get all invoices with pagination
- Get invoices by customer
- Create invoices
- Calculate total receivable
- Calculate total received

**PostgreSQL Queries:**
```sql
-- Total receivable with SUM aggregate
SELECT SUM(receivable) as total 
FROM invoices 
WHERE customer_id = 1;

-- Get invoices with LIMIT/OFFSET
SELECT * FROM invoices 
WHERE customer_id = 1 
ORDER BY created_at DESC
LIMIT 10 OFFSET 0;

-- Get by unique invoice number
SELECT * FROM invoices 
WHERE invoice_number = 'INV-2024-001';
```

---

### TicketService

**Features:**
- Get all tickets with pagination
- Get ticket by ID
- Get tickets by customer
- Filter by status (open/in_progress/waiting/resolved/closed)
- Filter by priority (critical/high/medium/low)
- Create and update tickets

**PostgreSQL Queries:**
```sql
-- Filter by ENUM status with index
SELECT * FROM tickets 
WHERE status = 'open'::ticket_status
ORDER BY created_at DESC;

-- Filter by ENUM priority
SELECT * FROM tickets 
WHERE priority = 'high'::ticket_priority
ORDER BY created_at DESC;

-- Get for specific user
SELECT * FROM tickets 
WHERE assigned_to = 1 
  AND status != 'closed'::ticket_status;
```

---

### PaymentService

**Features:**
- Get payments by service
- Create payments
- Calculate payment analytics

**PostgreSQL Queries:**
```sql
-- Get payments with date filtering
SELECT * FROM payments 
WHERE service_id = 1 
  AND date_received >= NOW() - INTERVAL '30 days'
ORDER BY date_received DESC;

-- Calculate total paid for service
SELECT SUM(amount_received) as total_paid 
FROM payments 
WHERE service_id = 1;
```

---

### ServiceService

**Features:**
- Get all services with pagination
- Get services by customer
- Get services by status
- Create and update services
- Track implementation progress

**PostgreSQL Queries:**
```sql
-- Get services by customer with JOIN
SELECT s.*, c.company 
FROM services s
JOIN customers c ON s.customer_id = c.id
WHERE s.customer_id = 1
ORDER BY s.created_at DESC;

-- Get services needing implementation
SELECT * FROM services 
WHERE implementation_required = true 
  AND implementation_status_id != 75;
```

---

### BranchService

**Features:**
- Get branches by customer
- Get primary branch
- Create and update branches

**PostgreSQL Queries:**
```sql
-- Get all branches for customer
SELECT * FROM branches 
WHERE customer_id = 1 
ORDER BY is_primary DESC, created_at DESC;

-- Get primary branch
SELECT * FROM branches 
WHERE customer_id = 1 
  AND is_primary = true;
```

---

### ReferenceService

**Features:**
- Get references by category
- Get reference by ID
- Get all active references

**PostgreSQL Queries:**
```sql
-- Get all references for category
SELECT * FROM reference_entries 
WHERE category = 'Business Type' 
  AND is_active = true
ORDER BY sort_order ASC;

-- Search reference
SELECT * FROM reference_entries 
WHERE label ILIKE '%search%' 
  AND is_active = true;
```

---

### LedgerService

**Features:**
- Get ledgers by customer
- Create and update ledgers
- Manage billing entities

**PostgreSQL Queries:**
```sql
-- Get ledgers for customer
SELECT * FROM ledgers 
WHERE customer_id = 1 
ORDER BY created_at DESC;

-- Get active ledgers
SELECT * FROM ledgers 
WHERE status = 'active'::payment_status_enum
ORDER BY created_at DESC;
```

---

### TrademarkService

**Features:**
- Get trademarks by customer
- Filter by status
- Create and update trademarks

**PostgreSQL Queries:**
```sql
-- Get trademarks by status
SELECT * FROM trademarks 
WHERE status = 'active'::trademark_status
ORDER BY created_at DESC;

-- Get expiring trademarks
SELECT * FROM trademarks 
WHERE expiry_date <= NOW() + INTERVAL '30 days'
  AND status = 'active'::trademark_status;
```

---

### InvestmentService

**Features:**
- Get investments by customer
- Calculate total investment
- Create and update investments

**PostgreSQL Queries:**
```sql
-- Total investment for customer
SELECT SUM(amount) as total_invested 
FROM investments 
WHERE customer_id = 1;

-- Get investments with details
SELECT i.*, re.label as investment_type 
FROM investments i
LEFT JOIN reference_entries re ON i.investment_type_id = re.id
WHERE i.customer_id = 1
ORDER BY i.created_at DESC;
```

---

## Service Registration (Program.cs)

```csharp
// Register all PostgreSQL services
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IReferenceService, ReferenceService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<ITrademarkService, TrademarkService>();
builder.Services.AddScoped<IInvestmentService, InvestmentService>();

// Configure logging
builder.Services.AddLogging();
```

---

## Logging in Services

Each service includes logging for debugging and monitoring:

```csharp
private readonly ILogger<CustomerService> _logger;

public CustomerService(CrmDbContext context, ILogger<CustomerService> logger)
{
    _context = context;
    _logger = logger;
}

// Log information
_logger.LogInformation($"Retrieved {customers.Count} customers");

// Log warnings
_logger.LogWarning($"Customer {id} not found");

// Log errors
_logger.LogError($"Error creating customer: {ex.Message}");
```

---

## PostgreSQL Query Patterns Used

### 1. **Case-Insensitive Search (ILIKE)**
```csharp
EF.Functions.ILike(column, $"%{searchTerm}%")
```

### 2. **Pagination (LIMIT/OFFSET)**
```csharp
.Skip((pageNumber - 1) * pageSize)
.Take(pageSize)
```

### 3. **Aggregation (SUM, COUNT, AVG)**
```csharp
.SumAsync(x => x.Amount)
.CountAsync()
.AverageAsync(x => x.Value)
```

### 4. **ENUM Type Filtering**
```csharp
.Where(x => x.Status.ToString() == "active")
```

### 5. **Date Range Filtering**
```csharp
.Where(x => x.CreatedAt >= DateTime.UtcNow.AddDays(-30))
```

### 6. **Indexes and Performance**
- `idx_customer_email` - for email lookups
- `idx_customer_type` - for type filtering
- `idx_invoice_customer_id` - for customer lookups
- `idx_ticket_status` - for status filtering
- `idx_created_at` - for date-based sorting

---

## Error Handling Pattern

All services follow this error handling pattern:

```csharp
public async Task<ApiResponse<T>> Method()
{
    try
    {
        // Execute database operation
        var result = await _context.Entity.ToListAsync();
        
        _logger.LogInformation("Success");
        
        return new ApiResponse<T>
        {
            Success = true,
            Data = result
        };
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error: {ex.Message}");
        
        return new ApiResponse<T>
        {
            Success = false,
            Message = $"Error: {ex.Message}"
        };
    }
}
```

---

## Usage Example

```csharp
// In a controller
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    
    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetCustomers(int pageNumber = 1, string? searchTerm = null)
    {
        var result = await _customerService.GetAll(pageNumber, searchTerm: searchTerm);
        
        if (!result.Success)
            return BadRequest(result);
            
        return Ok(result);
    }
}
```

---

## Testing Services

Unit test example:

```csharp
[TestClass]
public class CustomerServiceTests
{
    private Mock<CrmDbContext> _mockContext;
    private Mock<ILogger<CustomerService>> _mockLogger;
    private CustomerService _service;
    
    [TestInitialize]
    public void Setup()
    {
        _mockContext = new Mock<CrmDbContext>();
        _mockLogger = new Mock<ILogger<CustomerService>>();
        _service = new CustomerService(_mockContext.Object, _mockLogger.Object);
    }
    
    [TestMethod]
    public async Task GetAll_ReturnsCustomers()
    {
        // Arrange
        var customers = new List<Customer> { new Customer { Id = 1, Name = "Test" } };
        var mockSet = new Mock<DbSet<Customer>>();
        
        // Act
        var result = await _service.GetAll();
        
        // Assert
        Assert.IsTrue(result.Success);
    }
}
```

---

## Performance Optimization Tips

1. **Use AsNoTracking() for read-only queries**
   ```csharp
   .AsNoTracking()
   ```

2. **Include related entities efficiently**
   ```csharp
   .Include(c => c.Services)
   ```

3. **Use indexes for common filters**
   - Email, Type, Status, CreatedAt, CustomerId

4. **Avoid N+1 queries**
   ```csharp
   // Bad: N+1 queries
   var customers = await _context.Customers.ToListAsync();
   foreach(var customer in customers)
   {
       var services = await _context.Services.Where(s => s.CustomerId == customer.Id).ToListAsync();
   }
   
   // Good: Single query
   var customers = await _context.Customers
       .Include(c => c.Services)
       .ToListAsync();
   ```

5. **Limit result sets with pagination**
   ```csharp
   .Skip(offset).Take(limit)
   ```

---

## Summary

This modular service organization:
- ✅ Matches the frontend architecture
- ✅ Provides clear separation of concerns
- ✅ Includes PostgreSQL-optimized queries
- ✅ Has comprehensive logging
- ✅ Follows consistent error handling
- ✅ Uses dependency injection
- ✅ Includes ENUM type support
- ✅ Implements pagination and search
- ✅ Follows async/await patterns
- ✅ Is testable and maintainable

All services are ready to use with PostgreSQL!
