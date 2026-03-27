# Server services ↔ `core-crm-suite` UI

Each **`.cs`** file under `Services/` mirrors the Angular/TS service in `core-crm-suite/src/services/`.

| UI (`src/services`) | Server (`crm-server/Services`) |
|---------------------|--------------------------------|
| `CustomerService.ts` | `CustomerService.cs` |
| `BranchService.ts` → `LocationService` | `LocationService.cs` |
| `InvoiceService.ts` | `InvoiceService.cs` |
| `TicketService.ts` | `TicketService.ts` |
| `InvestmentService.ts` | `InvestmentService.cs` |
| `ReferenceService.ts` | `ReferenceService.cs` |
| `UserService.ts` | `UserService.cs` |
| `index.ts` exports | This table + `Controllers/` routes |

**Also on server (no matching TS file yet):**

- `ServiceService.cs` — CRM **Service** transactions (`types.ts` `Service`).
- `PaymentService.cs` — `types.ts` **Payment**.
- `TrademarkService.ts` — **Trademark** module only on server as `TrademarkService.cs`.

## Method mapping (high level)

- UI **mock** methods → HTTP **api/[controller]** endpoints in `Controllers/CrmControllers.cs`, `UsersController.cs`, `LedgerTrademarkInvestmentControllers.cs`.
- **Timelines**: `GET/POST .../{id}/timeline` (+ ticket timeline uses `AddTicketTimelineEntryDto` with `userId`).
- **Customer `getByType`**: `GET /api/customers/type/{lead|prospect|customer}` resolves `reference_entries` (`Customer Type` / `customer_type` + `value`).
- **Investment list by customer (no pagination)**: `GET /api/investments/customer/{id}/list` (UI-style full list).
- **Locations list by customer (no pagination)**: `GET /api/locations/customer/{id}/list`.

Point the UI `environment.ts` API base URL at this server when replacing mocks.
