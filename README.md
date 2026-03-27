# CRM Server - .NET MVC API

A comprehensive Customer Relationship Management (CRM) server built with .NET 8, implementing the Model-View-Controller pattern with Entity Framework Core and SQL Server database.

## Project Structure

```
crm-server/
├── Models/
│   └── CrmModels.cs           # All data models and enums
├── Data/
│   └── CrmDbContext.cs        # Entity Framework DbContext
├── Services/
│   ├── CustomerService.cs     # Customer business logic
│   ├── ServiceService.cs      # Service business logic
│   └── AdditionalServices.cs  # Invoice, Ticket, Branch, Payment, Reference services
├── DTOs/
│   └── CrmDtos.cs             # Data Transfer Objects for requests/responses
├── Controllers/
│   └── CrmControllers.cs      # API endpoints (REST controllers)
├── Program.cs                 # Application startup and configuration
├── appsettings.json           # Configuration settings
└── crm-server.csproj          # Project file with dependencies
```

## Architecture Overview

### Model Layer
- **Models**: All business entities (Customer, Service, Invoice, Ticket, Branch, Payment, etc.)
- **Enums**: Status types, priorities, and customer types
- **Timeline Entities**: Base timeline class for audit trails

### Data Layer
- **DbContext**: Entity Framework configuration and mappings
- **Database**: SQL Server with relationship management

### Service Layer
- **Business Logic**: Core CRUD operations and business rules
- **Data Access**: Repository pattern implementation via DbContext
- **Response Wrapping**: Standardized API responses

### Presentation Layer
- **Controllers**: REST API endpoints
- **DTOs**: Input validation and output formatting
- **Status Codes**: RESTful HTTP status responses

## API Endpoints

### Customers
- `GET /api/customers` - List all customers (paginated)
- `GET /api/customers/{id}` - Get customer by ID
- `GET /api/customers/type/{type}` - Get customers by type (lead/prospect/customer)
- `POST /api/customers` - Create new customer
- `PUT /api/customers/{id}` - Update customer
- `DELETE /api/customers/{id}` - Delete customer

### Services
- `GET /api/services` - List all services (paginated)
- `GET /api/services/{id}` - Get service by ID
- `GET /api/services/customer/{customerId}` - Get services by customer
- `POST /api/services` - Create new service
- `PUT /api/services/{id}` - Update service
- `DELETE /api/services/{id}` - Delete service

### Invoices
- `GET /api/invoices` - List all invoices (paginated)
- `GET /api/invoices/customer/{customerId}` - Get invoices by customer
- `POST /api/invoices` - Create new invoice

### Tickets
- `GET /api/tickets` - List all tickets (paginated)
- `GET /api/tickets/{id}` - Get ticket by ID
- `POST /api/tickets` - Create new ticket
- `PUT /api/tickets/{id}` - Update ticket

### Branches (Locations)
- `GET /api/branches/customer/{customerId}` - Get branches by customer
- `POST /api/branches` - Create new branch
- `PUT /api/branches/{id}` - Update branch

### Payments
- `GET /api/payments/service/{serviceId}` - Get payments by service
- `POST /api/payments` - Create new payment

### References
- `GET /api/references/category/{category}` - Get references by category
- `GET /api/references/{id}` - Get reference by ID

## Core Models

### Customer
Represents a lead, prospect, or customer in the CRM system.
- **Fields**: Company, Name, Email, Mobile, Address, GST Number, Tier, Shop Size
- **Relationships**: Services, Invoices, Branches, Ledgers, Trademarks, Tickets, Investments

### Service
Represents a billable service provided to a customer.
- **Fields**: Service Type, Due Date, Budget, Status, Implementation Required
- **Tracking**: Start Date, End Date, Progress Percentage, Project Manager
- **Implementation**: Can require implementation with stages and timeline

### Invoice
Represents billing for services.
- **Fields**: Invoice Number, Amount Receivable, Amount Received, Payment Mode, Payment Status
- **Relationship**: Links to Customer and Service

### Ticket
Support ticket management.
- **Fields**: Subject, Description, Priority, Status, Assigned User
- **Tracking**: SLA Deadline, Category

### Branch
Customer locations/branches.
- **Fields**: Location Name, Address, GST Number, Contact Persons, Emails, Mobiles
- **Tracking**: Primary branch indicator, Status

### Payment
Payment tracking for services.
- **Fields**: Amount Raised, Amount Received, Payment Mode, Date Received
- **Relationship**: Links to Service and optional Invoice

### Other Models
- **Ledger**: Billing entity information for GST mapping
- **Trademark**: Brand registration and management
- **Investment**: Capital investments in customers
- **SchedulerEvent**: Meeting and event scheduling
- **ReferenceEntry**: System dropdowns and lookup values

## Setup Instructions

### Prerequisites
- .NET 8 SDK
- SQL Server 2019 or later
- Visual Studio 2022 or VS Code

### Installation

1. **Clone/Navigate to project**
   ```bash
   cd crm-server
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Update database connection** (appsettings.json)
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=YOUR_SERVER;Database=CRM_DB;Integrated Security=true;TrustServerCertificate=True"
   }
   ```

4. **Create and apply database migrations**
   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

5. **Run the application**
   ```bash
   dotnet run
   ```

6. **Access Swagger UI**
   Navigate to `https://localhost:5001` in your browser

## Seed Data

The application comes with mock data defined in the frontend's `mock-data.ts`. To seed the database with initial data, you can:

1. Use Entity Framework's `OnModelCreating` to seed in `CrmDbContext.cs`
2. Create a separate seed service
3. Use SQL scripts

Example seed implementation:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    modelBuilder.Entity<ReferenceEntry>().HasData(
        new ReferenceEntry { Id = 1, Category = "Business Type", Label = "Startup", Value = "startup", ... }
        // ... more seed data
    );
}
```

## Database Schema

### Key Tables
- **Customers**: Core customer information
- **Services**: Service transactions linked to customers
- **Invoices**: Billing records
- **Tickets**: Support ticket tracking
- **Branches**: Customer locations
- **Payments**: Payment records
- **Ledgers**: Billing entities for GST mapping
- **Trademarks**: Brand registrations
- **Investments**: Capital investments
- **ReferenceEntries**: System lookup values
- **SchedulerEvents**: Meeting scheduling
- **Implementation Timelines**: Implementation progress tracking
- **Customer Timelines**: Customer activity history

### Relationships
- One Customer → Many Services
- One Service → Many Invoices
- One Customer → Many Branches
- One Customer → Many Tickets
- One Service → Many Payments
- One Service → Many Implementation Timelines

## Features

### CRUD Operations
- Full Create, Read, Update, Delete functionality for all entities
- Pagination support for large datasets
- Search functionality for customers

### Data Validation
- Entity-level validation via data annotations
- Business rule validation in services
- API response standardization

### Error Handling
- Try-catch blocks in all service methods
- Standardized error responses
- Detailed error messages for debugging

### Database Integration
- Entity Framework Core with SQL Server
- Automatic migrations on startup
- Relationship management with foreign keys
- Cascade delete policies

### API Documentation
- Swagger/OpenAPI integration
- Automatic API documentation
- Interactive testing interface

## Configuration

### Logging
Configure logging levels in `appsettings.json`:
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.EntityFrameworkCore": "Warning"
  }
}
```

### Database Connection
Update connection string for your SQL Server instance:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=SERVER_NAME;Database=CRM_DB;Integrated Security=true;"
}
```

### CORS
Currently allows all origins. Configure in `Program.cs`:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
```

## Development

### Adding New Entities

1. **Create model** in `Models/CrmModels.cs`
2. **Add DbSet** in `CrmDbContext.cs`
3. **Configure relationships** in `OnModelCreating`
4. **Create DTOs** in `DTOs/CrmDtos.cs`
5. **Implement service** in `Services/`
6. **Create controller** in `Controllers/CrmControllers.cs`
7. **Register service** in `Program.cs`
8. **Create migration**: `dotnet ef migrations add YourMigrationName`
9. **Update database**: `dotnet ef database update`

### Adding New API Endpoints

1. Add method to service interface and implementation
2. Add corresponding DTO models
3. Add controller method with appropriate HTTP attributes
4. Include input validation
5. Return standardized ApiResponse

## Dependencies

- **Microsoft.EntityFrameworkCore** - ORM for database access
- **Microsoft.EntityFrameworkCore.SqlServer** - SQL Server provider
- **Swashbuckle.AspNetCore** - Swagger/OpenAPI support
- **Microsoft.AspNetCore.Mvc.NewtonsoftJson** - JSON serialization

## API Response Format

All API endpoints return standardized responses:

```json
{
  "success": true,
  "message": "Operation successful",
  "data": { /* response data */ },
  "errors": null
}
```

### Paginated Response
```json
{
  "items": [ /* array of items */ ],
  "total": 100,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 10
}
```

## Best Practices

1. **Always use DTOs** for API contracts
2. **Validate input** in controllers and services
3. **Use async/await** for database operations
4. **Handle exceptions** gracefully
5. **Follow RESTful conventions** for endpoints
6. **Document complex business logic**
7. **Use dependency injection** for all services
8. **Keep services testable** with clear separation

## Troubleshooting

### Migration Issues
```bash
# Remove latest migration
dotnet ef migrations remove

# Reset database
dotnet ef database drop
dotnet ef database update
```

### Connection String Issues
- Verify SQL Server is running
- Check server name and authentication
- Ensure database exists or set `Initial Catalog`

### Port Already in Use
```bash
dotnet run --urls "https://localhost:5002"
```

## Future Enhancements

- [ ] Authentication and authorization
- [ ] Advanced filtering and sorting
- [ ] Batch operations
- [ ] Report generation
- [ ] File upload management
- [ ] Audit logging
- [ ] Caching layer
- [ ] Background jobs
- [ ] Integration tests
- [ ] API versioning

## License

This project is part of the CRM Suite and follows the main project's license.

## Support

For issues, questions, or contributions, please refer to the main project repository or contact the development team.
