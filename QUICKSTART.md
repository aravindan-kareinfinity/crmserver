# CRM Server - Quick Start Guide

## Quick Setup (5 minutes)

### Step 1: Prerequisites Check
```bash
dotnet --version  # Should be 8.0 or higher
```

### Step 2: Open Project
```bash
cd E:\project\crm\crm-server
```

### Step 3: Restore Packages
```bash
dotnet restore
```

### Step 4: Configure Database
Edit `appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(local);Database=CRM_DB;Integrated Security=true;TrustServerCertificate=True"
}
```

**For Azure SQL** (example):
```json
"DefaultConnection": "Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=CRM_DB;Persist Security Info=False;User ID=admin;Password=Password123;Encrypt=True;Connection Timeout=30;"
```

### Step 5: Create Database
```bash
# Create initial migration
dotnet ef migrations add InitialCreate

# Apply migration to database
dotnet ef database update
```

### Step 6: Run Server
```bash
dotnet run
```

The API will start on:
- HTTPS: `https://localhost:5001`
- Swagger UI: `https://localhost:5001`

## Testing API Endpoints

### Using Swagger UI
1. Open browser: `https://localhost:5001`
2. Click on endpoint
3. Click "Try it out"
4. Enter parameters
5. Click "Execute"

### Using cURL

**Get all customers:**
```bash
curl -X GET "https://localhost:5001/api/customers?pageNumber=1&pageSize=10" \
  -H "accept: application/json" --insecure
```

**Create customer:**
```bash
curl -X POST "https://localhost:5001/api/customers" \
  -H "Content-Type: application/json" \
  -d '{
    "company": "Acme Corp",
    "regName": "Acme Corporation",
    "name": "John Doe",
    "mobile": "+91 9876543210",
    "email": "john@acme.com",
    "addressLine1": "123 Tech Street",
    "addressLine2": "Tech Park",
    "pincode": "560066",
    "shopSizeId": 32,
    "tierId": 35
  }' --insecure
```

**Get services by customer:**
```bash
curl -X GET "https://localhost:5001/api/services/customer/1" \
  -H "accept: application/json" --insecure
```

### Using Postman
1. Import the collection from Swagger: `https://localhost:5001/swagger/v1/swagger.json`
2. Set up requests for each endpoint
3. Test CRUD operations

## Project Structure Explained

```
Models/
  └─ CrmModels.cs
     ├─ Enums (CustomerType, TicketStatus, etc.)
     ├─ Customer (+ CustomerTimeline)
     ├─ Service (+ ImplementationTimeline)
     ├─ Invoice (+ InvoiceTimeline)
     ├─ Ticket (+ TicketTimeline)
     ├─ Branch (+ BranchTimeline)
     ├─ Payment
     ├─ Ledger (+ LedgerTimeline)
     ├─ Trademark
     ├─ Investment (+ InvestmentTimeline)
     ├─ ReferenceEntry
     ├─ User, Role
     ├─ Report
     └─ SchedulerEvent

Data/
  └─ CrmDbContext.cs
     ├─ DbSets for each model
     └─ Entity relationships configuration

Services/
  ├─ CustomerService (ICustomerService)
  ├─ ServiceService (IServiceService)
  └─ AdditionalServices.cs
     ├─ InvoiceService
     ├─ TicketService
     ├─ BranchService
     ├─ PaymentService
     └─ ReferenceService

Controllers/
  └─ CrmControllers.cs
     ├─ CustomersController
     ├─ ServicesController
     ├─ InvoicesController
     ├─ TicketsController
     ├─ BranchesController
     ├─ PaymentsController
     └─ ReferencesController

DTOs/
  └─ CrmDtos.cs
     ├─ Request DTOs (Create*/Update*)
     ├─ Response DTOs (*)ResponseDto
     ├─ ApiResponse<T>
     └─ PaginatedResponse<T>
```

## Common Tasks

### Seed Initial Data

Create `Services/SeedService.cs`:

```csharp
public class SeedService
{
    public static void SeedData(CrmDbContext context)
    {
        if (!context.ReferenceEntries.Any())
        {
            var references = new[]
            {
                new ReferenceEntry { 
                    Id = 1, 
                    Category = "Business Type", 
                    Label = "Startup", 
                    Value = "startup", 
                    IsActive = true 
                },
                // ... more entries
            };
            context.ReferenceEntries.AddRange(references);
            context.SaveChanges();
        }
    }
}
```

Call in `Program.cs`:
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
    db.Database.Migrate();
    SeedService.SeedData(db);
}
```

### Add Authentication

1. Install package: `dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer`
2. Add to `Program.cs`:
```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* config */ });
```
3. Add `[Authorize]` to controllers

### Enable Pagination

All list endpoints support pagination:
```
GET /api/customers?pageNumber=1&pageSize=20
GET /api/services?pageNumber=2&pageSize=50
GET /api/tickets?pageNumber=1&pageSize=10
```

### Search Functionality

Supported on customers:
```
GET /api/customers?searchTerm=acme
GET /api/customers?pageNumber=1&pageSize=10&searchTerm=john
```

## Debugging

### Enable Debug Mode
Edit `launchSettings.json`:
```json
"profiles": {
  "CRM_Server": {
    "commandName": "Project",
    "dotnetRunMessages": true,
    "launchBrowser": true,
    "applicationUrl": "https://localhost:5001;http://localhost:5000"
  }
}
```

### View SQL Queries
Add to `appsettings.json`:
```json
"Logging": {
  "LogLevel": {
    "Microsoft.EntityFrameworkCore.Database.Command": "Information"
  }
}
```

### Check Database
```bash
# List migrations
dotnet ef migrations list

# See last applied migration
dotnet ef migrations info

# Generate migration script
dotnet ef migrations script
```

## Deployment

### Docker
Create `Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["crm-server.csproj", "."]
RUN dotnet restore "crm-server.csproj"
COPY . .
RUN dotnet build "crm-server.csproj" -c Release -o /app/build

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/build .
EXPOSE 5001
ENTRYPOINT ["dotnet", "crm-server.dll"]
```

### Local IIS
```bash
dotnet publish -c Release -o ./publish
# Copy publish folder to IIS application directory
```

## Environment Variables

Create `.env` file (not committed to source control):
```
ConnectionStrings__DefaultConnection=Server=...;Database=CRM_DB;...
Logging__LogLevel__Default=Information
ASPNETCORE_ENVIRONMENT=Development
```

## Performance Tips

1. **Use pagination** for large datasets
2. **Add indexes** to frequently searched columns
3. **Enable query caching** for references
4. **Use async methods** for I/O operations
5. **Monitor slow queries** with Entity Framework logging

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Connection timeout | Check SQL Server running, verify server name |
| Migrations pending | Run `dotnet ef database update` |
| Port 5001 in use | Change port in `launchSettings.json` |
| Build fails | Run `dotnet clean && dotnet restore` |
| Database locked | Restart SQL Server, check for open connections |

## Next Steps

1. ✅ Server running locally
2. 📊 Connect frontend to API endpoints
3. 🔐 Add authentication/authorization
4. 📝 Seed sample data
5. 🧪 Write unit tests
6. 📦 Deploy to cloud/server

## Resources

- [Entity Framework Core Docs](https://docs.microsoft.com/en-us/ef/core/)
- [ASP.NET Core API Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- [SQL Server Documentation](https://docs.microsoft.com/en-us/sql/sql-server/)
- [Swagger/OpenAPI Spec](https://swagger.io/)

## Support

For issues:
1. Check logs: `dotnet run` output
2. Check database: SQL Server Management Studio
3. Review controller logic
4. Check DTO mappings
5. Verify service implementations
