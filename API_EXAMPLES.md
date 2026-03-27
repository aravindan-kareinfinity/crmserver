# CRM Server - API Usage Examples

## Complete API Reference with Code Examples

### Authentication Header (when implemented)
```
Authorization: Bearer YOUR_JWT_TOKEN
Content-Type: application/json
```

---

## CUSTOMERS API

### 1. Get All Customers (Paginated)
```http
GET /api/customers?pageNumber=1&pageSize=10
```

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/customers?pageNumber=1&pageSize=10" \
  -H "accept: application/json" --insecure
```

**Response:**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 1,
        "company": "Acme Corp",
        "regName": "Acme Corporation",
        "name": "Sarah Chen",
        "email": "sarah@acmecorp.com",
        "status": "Active",
        "type": "customer",
        "createdAt": "2024-01-15T00:00:00",
        "totalLocations": 3,
        "totalTradeNames": 2
      }
    ],
    "total": 6,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

---

### 2. Search Customers
```http
GET /api/customers?pageNumber=1&pageSize=10&searchTerm=acme
```

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/customers?searchTerm=acme" --insecure
```

---

### 3. Get Customer by ID
```http
GET /api/customers/{id}
```

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/customers/1" \
  -H "accept: application/json" --insecure
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "company": "Acme Corp",
    "regName": "Acme Corporation",
    "name": "Sarah Chen",
    "email": "sarah@acmecorp.com",
    "status": "Active",
    "type": "customer",
    "createdAt": "2024-01-15T00:00:00"
  }
}
```

---

### 4. Get Customers by Type
```http
GET /api/customers/type/{type}
```

**Valid types:** `lead`, `prospect`, `customer`

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/customers/type/customer" --insecure
```

---

### 5. Create Customer
```http
POST /api/customers
Content-Type: application/json
```

**Request Body:**
```json
{
  "company": "TechFlow Solutions",
  "regName": "TechFlow Solutions Pvt Ltd",
  "name": "Marcus Johnson",
  "mobile": "+91 9876543211",
  "email": "marcus@techflow.io",
  "businessTypeId": 2,
  "industryId": 4,
  "addressLine1": "202, Indiranagar",
  "addressLine2": "Tech Square",
  "cityId": 9,
  "stateId": 12,
  "countryId": 14,
  "pincode": "560038",
  "gstNumber": "29ABCDE1234F1Z5",
  "contactPersons": ["Marcus Johnson", "John Doe"],
  "emails": ["marcus@techflow.io", "john@techflow.io"],
  "mobiles": ["+91 9876543211", "+91 9876543212"],
  "shopSizeId": 31,
  "tierId": 35
}
```

**cURL:**
```bash
curl -X POST "https://localhost:5001/api/customers" \
  -H "Content-Type: application/json" \
  -d '{
    "company": "TechFlow Solutions",
    "regName": "TechFlow Solutions Pvt Ltd",
    "name": "Marcus Johnson",
    "mobile": "+91 9876543211",
    "email": "marcus@techflow.io",
    "addressLine1": "202, Indiranagar",
    "addressLine2": "Tech Square",
    "pincode": "560038",
    "shopSizeId": 31,
    "tierId": 35
  }' --insecure
```

**Response:**
```json
{
  "success": true,
  "message": "Customer created successfully",
  "data": {
    "id": 7,
    "company": "TechFlow Solutions",
    "regName": "TechFlow Solutions Pvt Ltd",
    "name": "Marcus Johnson",
    "email": "marcus@techflow.io",
    "status": "Active",
    "type": "lead",
    "createdAt": "2026-03-23T10:00:00"
  }
}
```

---

### 6. Update Customer
```http
PUT /api/customers/{id}
Content-Type: application/json
```

**Request Body:**
```json
{
  "name": "Marcus Johnson Updated",
  "email": "marcus.new@techflow.io",
  "status": "Active"
}
```

**cURL:**
```bash
curl -X PUT "https://localhost:5001/api/customers/7" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Marcus Johnson Updated",
    "email": "marcus.new@techflow.io"
  }' --insecure
```

---

### 7. Delete Customer
```http
DELETE /api/customers/{id}
```

**cURL:**
```bash
curl -X DELETE "https://localhost:5001/api/customers/7" --insecure
```

**Response:**
```json
{
  "success": true,
  "message": "Customer deleted successfully",
  "data": true
}
```

---

## SERVICES API

### 1. Get All Services
```http
GET /api/services?pageNumber=1&pageSize=10
```

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/services" --insecure
```

---

### 2. Get Services by Customer
```http
GET /api/services/customer/{customerId}
```

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/services/customer/1" --insecure
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "customerId": 1,
      "serviceTypeId": 17,
      "projectTitle": "ERP Migration",
      "progressPercentage": 100,
      "status": "active",
      "implementationRequired": true,
      "createdAt": "2024-01-15T00:00:00"
    }
  ]
}
```

---

### 3. Create Service
```http
POST /api/services
Content-Type: application/json
```

**Request Body:**
```json
{
  "customerId": 1,
  "serviceTypeId": 16,
  "frequencyId": 44,
  "dueDate": 15,
  "implementationRequired": true,
  "projectTitle": "CRM Implementation",
  "projectManagerId": 1,
  "startDate": "2026-03-25T00:00:00",
  "endDate": "2026-06-25T00:00:00",
  "budgetAmount": 500000
}
```

**cURL:**
```bash
curl -X POST "https://localhost:5001/api/services" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": 1,
    "serviceTypeId": 16,
    "frequencyId": 44,
    "dueDate": 15,
    "implementationRequired": true,
    "projectTitle": "CRM Implementation",
    "budgetAmount": 500000
  }' --insecure
```

---

### 4. Update Service
```http
PUT /api/services/{id}
Content-Type: application/json
```

**Request Body:**
```json
{
  "status": "on_hold"
}
```

**cURL:**
```bash
curl -X PUT "https://localhost:5001/api/services/1" \
  -H "Content-Type: application/json" \
  -d '{
    "status": "on_hold"
  }' --insecure
```

---

## INVOICES API

### 1. Get All Invoices
```http
GET /api/invoices?pageNumber=1&pageSize=10
```

---

### 2. Get Invoices by Customer
```http
GET /api/invoices/customer/{customerId}
```

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/invoices/customer/1" --insecure
```

---

### 3. Create Invoice
```http
POST /api/invoices
Content-Type: application/json
```

**Request Body:**
```json
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

**cURL:**
```bash
curl -X POST "https://localhost:5001/api/invoices" \
  -H "Content-Type: application/json" \
  -d '{
    "invoiceNumber": "INV-2026-001",
    "customerId": 1,
    "serviceId": 1,
    "paymentModeId": 52,
    "paymentStatusId": 60,
    "receivable": 250000,
    "subscriptionStartAt": "2026-03-01T00:00:00",
    "subscriptionEndAt": "2027-03-01T00:00:00"
  }' --insecure
```

---

## TICKETS API

### 1. Get All Tickets
```http
GET /api/tickets?pageNumber=1&pageSize=10
```

---

### 2. Get Ticket by ID
```http
GET /api/tickets/{id}
```

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/tickets/1" --insecure
```

---

### 3. Create Ticket
```http
POST /api/tickets
Content-Type: application/json
```

**Request Body:**
```json
{
  "customerId": 1,
  "locationId": 1,
  "subject": "Database performance issue",
  "description": "Users reporting slow query performance",
  "priority": "high",
  "assignedTo": 1,
  "category": "Performance"
}
```

**cURL:**
```bash
curl -X POST "https://localhost:5001/api/tickets" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": 1,
    "locationId": 1,
    "subject": "Database performance issue",
    "description": "Users reporting slow query performance",
    "priority": "high",
    "assignedTo": 1,
    "category": "Performance"
  }' --insecure
```

---

### 4. Update Ticket
```http
PUT /api/tickets/{id}
Content-Type: application/json
```

**Request Body:**
```json
{
  "status": "in_progress",
  "priority": "critical"
}
```

**cURL:**
```bash
curl -X PUT "https://localhost:5001/api/tickets/1" \
  -H "Content-Type: application/json" \
  -d '{
    "status": "in_progress",
    "priority": "critical"
  }' --insecure
```

---

## BRANCHES API

### 1. Get Branches by Customer
```http
GET /api/branches/customer/{customerId}?pageNumber=1&pageSize=10
```

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/branches/customer/1" --insecure
```

---

### 2. Create Branch
```http
POST /api/branches
Content-Type: application/json
```

**Request Body:**
```json
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
  "emails": ["sarah@acmecorp.com"],
  "mobiles": ["+91 9876543210"],
  "shopSizeId": 32,
  "tierId": 35,
  "isPrimary": true
}
```

**cURL:**
```bash
curl -X POST "https://localhost:5001/api/branches" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": 1,
    "name": "Mumbai Branch",
    "regName": "Acme Corp - Mumbai",
    "pincode": "400051",
    "cityId": 8,
    "stateId": 11,
    "countryId": 14,
    "addressLine1": "Bandra Kurla Complex",
    "shopSizeId": 32,
    "tierId": 35,
    "isPrimary": true
  }' --insecure
```

---

## PAYMENTS API

### 1. Get Payments by Service
```http
GET /api/payments/service/{serviceId}?pageNumber=1&pageSize=10
```

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/payments/service/1" --insecure
```

---

### 2. Create Payment
```http
POST /api/payments
Content-Type: application/json
```

**Request Body:**
```json
{
  "serviceId": 1,
  "amountRaised": 250000,
  "amountReceived": 250000,
  "dateReceived": "2026-03-20T00:00:00",
  "paymentModeId": 52,
  "invoiceId": 1
}
```

**cURL:**
```bash
curl -X POST "https://localhost:5001/api/payments" \
  -H "Content-Type: application/json" \
  -d '{
    "serviceId": 1,
    "amountRaised": 250000,
    "amountReceived": 250000,
    "dateReceived": "2026-03-20T00:00:00",
    "paymentModeId": 52
  }' --insecure
```

---

## REFERENCES API

### 1. Get References by Category
```http
GET /api/references/category/{category}
```

**Valid categories:** `Business Type`, `Industry`, `City`, `State`, `Service Type`, `Shop Size`, `Payment Mode`, `Payment Status`, etc.

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/references/category/Business%20Type" --insecure
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "category": "Business Type",
      "label": "Startup",
      "value": "startup",
      "isActive": true
    },
    {
      "id": 2,
      "category": "Business Type",
      "label": "SME",
      "value": "sme",
      "isActive": true
    }
  ]
}
```

---

### 2. Get Reference by ID
```http
GET /api/references/{id}
```

**cURL:**
```bash
curl -X GET "https://localhost:5001/api/references/1" --insecure
```

---

## Error Response Examples

### 400 Bad Request
```json
{
  "success": false,
  "message": "Error creating customer",
  "data": null,
  "errors": ["Invalid email format", "Pincode must be 6 digits"]
}
```

### 404 Not Found
```json
{
  "success": false,
  "message": "Customer not found",
  "data": null
}
```

### 500 Internal Server Error
```json
{
  "success": false,
  "message": "Error fetching customers: Database connection failed",
  "data": null
}
```

---

## Pagination Examples

### Request:
```http
GET /api/customers?pageNumber=2&pageSize=5
```

### Response:
```json
{
  "success": true,
  "data": {
    "items": [ /* 5 items */ ],
    "total": 23,
    "pageNumber": 2,
    "pageSize": 5,
    "totalPages": 5
  }
}
```

---

## Filter & Search Examples

### Search Customers:
```bash
GET /api/customers?searchTerm=acme&pageNumber=1&pageSize=10
```

### Filter Customers by Type:
```bash
GET /api/customers/type/prospect
```

---

## Using with Frontend

### JavaScript/TypeScript Example:
```typescript
// Get all customers
async function getCustomers(pageNumber = 1, pageSize = 10) {
  const response = await fetch(
    `https://localhost:5001/api/customers?pageNumber=${pageNumber}&pageSize=${pageSize}`,
    {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json'
      }
    }
  );
  return await response.json();
}

// Create customer
async function createCustomer(customerData) {
  const response = await fetch('https://localhost:5001/api/customers', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(customerData)
  });
  return await response.json();
}

// Update customer
async function updateCustomer(id, updateData) {
  const response = await fetch(`https://localhost:5001/api/customers/${id}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(updateData)
  });
  return await response.json();
}

// Delete customer
async function deleteCustomer(id) {
  const response = await fetch(`https://localhost:5001/api/customers/${id}`, {
    method: 'DELETE'
  });
  return await response.json();
}
```

---

## Response Status Codes

| Code | Meaning |
|------|---------|
| 200 | OK - Request successful |
| 201 | Created - Resource created |
| 400 | Bad Request - Invalid input |
| 404 | Not Found - Resource doesn't exist |
| 500 | Internal Server Error |

---

**API Version**: 1.0.0  
**Last Updated**: 2026-03-23
