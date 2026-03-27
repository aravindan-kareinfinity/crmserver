# CRM Server - Complete API Reference

## Overview
**Total Endpoints**: 37+  
**Controllers**: 10  
**Services**: 10  
**HTTP Methods**: GET, POST, PUT, DELETE

---

## API Endpoints Reference

### 1. CUSTOMERS API (6 endpoints)

#### List Customers (Paginated + Searchable)
```http
GET /api/customers?pageNumber=1&pageSize=10&searchTerm=acme
```
**Response**: `ApiResponse<PaginatedResponse<CustomerResponseDto>>`

#### Get Customer by ID
```http
GET /api/customers/{id}
```
**Response**: `ApiResponse<CustomerResponseDto>`

#### Get Customers by Type
```http
GET /api/customers/type/{type}
```
**Types**: `lead`, `prospect`, `customer`

#### Create Customer
```http
POST /api/customers
Content-Type: application/json

{
  "company": "Acme Corp",
  "regName": "Acme Corporation",
  "name": "John Smith",
  "mobile": "+91 9876543210",
  "email": "john@acme.com",
  "addressLine1": "123 Tech Street",
  "addressLine2": "Tech Park",
  "pincode": "560066",
  "shopSizeId": 32,
  "tierId": 35
}
```

#### Update Customer
```http
PUT /api/customers/{id}
Content-Type: application/json

{
  "name": "John Smith Updated",
  "email": "john.new@acme.com",
  "status": "Active"
}
```

#### Delete Customer
```http
DELETE /api/customers/{id}
```

---

### 2. SERVICES API (7 endpoints)

#### List Services (Paginated)
```http
GET /api/services?pageNumber=1&pageSize=10
```

#### Get Service by ID
```http
GET /api/services/{id}
```

#### Get Services by Customer
```http
GET /api/services/customer/{customerId}
```

#### Create Service
```http
POST /api/services
Content-Type: application/json

{
  "customerId": 1,
  "serviceTypeId": 16,
  "frequencyId": 44,
  "dueDate": 15,
  "implementationRequired": true,
  "projectTitle": "CRM Implementation",
  "projectManagerId": 1,
  "budgetAmount": 500000
}
```

#### Update Service
```http
PUT /api/services/{id}
Content-Type: application/json

{
  "status": "on_hold",
  "implementationRequired": false
}
```

#### Delete Service
```http
DELETE /api/services/{id}
```

---

### 3. INVOICES API (3 endpoints)

#### List Invoices (Paginated)
```http
GET /api/invoices?pageNumber=1&pageSize=10
```

#### Get Invoices by Customer
```http
GET /api/invoices/customer/{customerId}
```

#### Create Invoice
```http
POST /api/invoices
Content-Type: application/json

{
  "invoiceNumber": "INV-2026-001",
  "customerId": 1,
  "serviceId": 1,
  "paymentModeId": 52,
  "paymentStatusId": 60,
  "receivable": 250000,
  "subscriptionStartAt": "2026-03-01T00:00:00",
  "subscriptionEndAt": "2027-03-01T00:00:00"
}
```

---

### 4. TICKETS API (4 endpoints)

#### List Tickets (Paginated)
```http
GET /api/tickets?pageNumber=1&pageSize=10
```

#### Get Ticket by ID
```http
GET /api/tickets/{id}
```

#### Create Ticket
```http
POST /api/tickets
Content-Type: application/json

{
  "customerId": 1,
  "locationId": 1,
  "subject": "Login authentication failing",
  "description": "Users unable to login via SSO",
  "priority": "critical",
  "assignedTo": 1,
  "category": "Bug"
}
```
**Priority**: `critical`, `high`, `medium`, `low`

#### Update Ticket
```http
PUT /api/tickets/{id}
Content-Type: application/json

{
  "status": "in_progress",
  "priority": "high",
  "assignedTo": 3
}
```
**Status**: `open`, `in_progress`, `waiting`, `resolved`, `closed`

---

### 5. BRANCHES API (3 endpoints)

#### Get Branches by Customer
```http
GET /api/branches/customer/{customerId}?pageNumber=1&pageSize=10
```

#### Create Branch
```http
POST /api/branches
Content-Type: application/json

{
  "customerId": 1,
  "name": "Mumbai Branch",
  "regName": "Acme Corp - Mumbai",
  "pincode": "400051",
  "cityId": 8,
  "stateId": 11,
  "countryId": 14,
  "addressLine1": "Bandra Kurla Complex",
  "addressLine2": "Bandra East",
  "contactPersons": ["Sarah Chen"],
  "emails": ["sarah@acme.com"],
  "mobiles": ["+91 9876543210"],
  "shopSizeId": 32,
  "tierId": 35,
  "isPrimary": true
}
```

#### Update Branch
```http
PUT /api/branches/{id}
Content-Type: application/json

{
  "name": "Mumbai Branch Updated",
  "addressLine1": "New Address"
}
```

---

### 6. PAYMENTS API (2 endpoints)

#### Get Payments by Service
```http
GET /api/payments/service/{serviceId}?pageNumber=1&pageSize=10
```

#### Create Payment
```http
POST /api/payments
Content-Type: application/json

{
  "serviceId": 1,
  "amountRaised": 250000,
  "amountReceived": 250000,
  "dateReceived": "2026-03-20T00:00:00",
  "paymentModeId": 52,
  "invoiceId": 1
}
```

---

### 7. REFERENCES API (2 endpoints)

#### Get References by Category
```http
GET /api/references/category/{category}
```
**Categories**: 
- `Business Type`, `Industry`, `City`, `State`, `Country`
- `Service Type`, `Shop Size`, `City Tier`
- `Payment Mode`, `Payment Status`, `Tax`
- `Frequency`, `Investment Type`, etc.

#### Get Reference by ID
```http
GET /api/references/{id}
```

---

### 8. LEDGERS API (5 endpoints) - NEW

#### Get Ledgers by Customer
```http
GET /api/ledgers/customer/{customerId}?pageNumber=1&pageSize=10
```

#### Get Ledger by ID
```http
GET /api/ledgers/{id}
```

#### Create Ledger
```http
POST /api/ledgers
Content-Type: application/json

{
  "customerId": 1,
  "company": "Acme Corp - Billing",
  "contactName": "Finance Manager",
  "mobile": "+91 9876543210",
  "email": "finance@acme.com",
  "pincode": "560066",
  "addressLine1": "101, Tech Park",
  "addressLine2": "Whitefield",
  "gstNumber": "18AABCT0202R1Z5"
}
```

#### Update Ledger
```http
PUT /api/ledgers/{id}
Content-Type: application/json

{
  "company": "Updated Company Name",
  "contactName": "New Contact",
  "email": "new.email@acme.com"
}
```

#### Delete Ledger
```http
DELETE /api/ledgers/{id}
```

---

### 9. TRADEMARKS API (6 endpoints) - NEW

#### Get Trademarks by Customer
```http
GET /api/trademarks/customer/{customerId}?pageNumber=1&pageSize=10
```

#### Get Trademark by ID
```http
GET /api/trademarks/{id}
```

#### Get Trademarks by Status
```http
GET /api/trademarks/status/{status}
```
**Status**: `active`, `expired`, `pending`, `rejected`

#### Create Trademark
```http
POST /api/trademarks
Content-Type: application/json

{
  "customerId": 1,
  "locationId": 1,
  "regName": "ACME Tech Solutions",
  "gstNumber": "18AABCT0202R1Z5",
  "pincode": "400051",
  "cityId": 8,
  "stateId": 11,
  "countryId": 14,
  "addressLine1": "101, Tech Park",
  "addressLine2": "Bandra",
  "contactPersons": ["Sarah Chen"],
  "emails": ["sarah@acme.com"],
  "mobiles": ["+91 9876543210"],
  "tierId": 35,
  "category": "Class 09 - Software",
  "description": "Technology solutions brand",
  "registrationDate": "2022-03-15T00:00:00",
  "expiryDate": "2032-03-14T00:00:00"
}
```

#### Update Trademark
```http
PUT /api/trademarks/{id}
Content-Type: application/json

{
  "status": "active",
  "expiryDate": "2032-03-14T00:00:00",
  "remarks": "Renewal approved"
}
```

#### Delete Trademark
```http
DELETE /api/trademarks/{id}
```

---

### 10. INVESTMENTS API (6 endpoints) - NEW

#### Get Investments by Customer
```http
GET /api/investments/customer/{customerId}?pageNumber=1&pageSize=10
```

#### Get Investment by ID
```http
GET /api/investments/{id}
```

#### Get Total Investment by Customer
```http
GET /api/investments/customer/{customerId}/total
```
**Returns**: Total investment amount as decimal

#### Create Investment
```http
POST /api/investments
Content-Type: application/json

{
  "customerId": 1,
  "locationId": 1,
  "amount": 500000,
  "investmentTypeId": 63,
  "staffId": 1,
  "notes": "Series A participation"
}
```
**Investment Types**:
- `63` - Equity
- `64` - Debt
- `65` - Convertible Note

#### Update Investment
```http
PUT /api/investments/{id}
Content-Type: application/json

{
  "amount": 600000,
  "notes": "Updated investment amount"
}
```

#### Delete Investment
```http
DELETE /api/investments/{id}
```

---

## Response Format

### Success Response (200/201)
```json
{
  "success": true,
  "message": "Operation successful",
  "data": {
    "id": 1,
    "name": "John Smith",
    "email": "john@example.com",
    ...
  },
  "errors": null
}
```

### Paginated Response (200)
```json
{
  "success": true,
  "data": {
    "items": [
      { "id": 1, "name": "Item 1" },
      { "id": 2, "name": "Item 2" }
    ],
    "total": 25,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 3
  }
}
```

### Error Response (400/404/500)
```json
{
  "success": false,
  "message": "Customer not found",
  "data": null,
  "errors": ["Invalid customer ID"]
}
```

---

## HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | OK - Request successful |
| 201 | Created - Resource created successfully |
| 400 | Bad Request - Invalid input or validation error |
| 404 | Not Found - Resource doesn't exist |
| 500 | Internal Server Error - Server error |

---

## Query Parameters

### Pagination
```
pageNumber=1    (default: 1, min: 1)
pageSize=10     (default: 10, max: 100)
```

### Search (Customers only)
```
searchTerm=text (searches company, name, email)
```

### Filtering (References, Trademarks)
```
category=string
status=string
type=string
```

---

## Data Types

### Enums
```csharp
CustomerType: lead, prospect, customer
TicketStatus: open, in_progress, waiting, resolved, closed
TicketPriority: critical, high, medium, low
TrademarkStatus: active, expired, pending, rejected
PaymentStatus: paid, pending, overdue, failed
```

### Common Fields
```csharp
Id              : int (auto-increment)
CreatedAt       : datetime (UTC)
CreatedBy       : string (username)
Status          : string (enum)
Email           : string (unique)
Mobile          : string (10-20 chars)
Pincode         : string (6 chars for India)
GstNumber       : string (15 chars for India)
```

---

## Request Headers

```http
Content-Type: application/json
Accept: application/json
```

## Optional Authentication Header (when implemented)
```http
Authorization: Bearer {JWT_TOKEN}
```

---

## cURL Examples

### Get All Customers
```bash
curl -X GET "https://localhost:5001/api/customers?pageNumber=1&pageSize=10" \
  -H "Accept: application/json" --insecure
```

### Create Customer
```bash
curl -X POST "https://localhost:5001/api/customers" \
  -H "Content-Type: application/json" \
  -d '{
    "company": "New Company",
    "regName": "New Company Pvt Ltd",
    "name": "Contact Name",
    "mobile": "+91 9876543210",
    "email": "contact@company.com",
    "addressLine1": "123 Street",
    "pincode": "560066",
    "shopSizeId": 32,
    "tierId": 35
  }' --insecure
```

### Get Investments Total
```bash
curl -X GET "https://localhost:5001/api/investments/customer/1/total" \
  -H "Accept: application/json" --insecure
```

### Update Trademark Status
```bash
curl -X PUT "https://localhost:5001/api/trademarks/1" \
  -H "Content-Type: application/json" \
  -d '{
    "status": "active",
    "remarks": "Renewal approved"
  }' --insecure
```

---

## JavaScript/TypeScript Usage

```typescript
// Fetch customers
async function getCustomers() {
  const response = await fetch(
    'https://localhost:5001/api/customers?pageNumber=1&pageSize=10',
    { 
      method: 'GET',
      headers: { 'Content-Type': 'application/json' }
    }
  );
  return await response.json();
}

// Create service
async function createService(serviceData) {
  const response = await fetch(
    'https://localhost:5001/api/services',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(serviceData)
    }
  );
  return await response.json();
}

// Get investment total
async function getInvestmentTotal(customerId) {
  const response = await fetch(
    `https://localhost:5001/api/investments/customer/${customerId}/total`,
    { headers: { 'Content-Type': 'application/json' } }
  );
  return await response.json();
}
```

---

## API Versioning (Future)

When versioning is implemented, use:
```http
GET /api/v2/customers  (for version 2)
```

---

## Rate Limiting (Future)

When implemented, responses will include:
```http
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 999
X-RateLimit-Reset: 1234567890
```

---

## Troubleshooting

### 400 Bad Request
Check:
- Required fields present
- Data types correct
- Email format valid
- Pincode length (6 for India)

### 404 Not Found
Check:
- ID exists in database
- Spelling of endpoint
- Customer/Resource exists

### 500 Internal Server Error
Check:
- Database connection
- Logs for details
- Required dependencies

---

**API Version**: 1.0.0  
**Framework**: ASP.NET Core 8  
**Documentation**: Swagger at `https://localhost:5001`  
**Total Endpoints**: 37+
