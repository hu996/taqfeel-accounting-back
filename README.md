# AccountingSaaS Phase 1 Backend

Clean Architecture ASP.NET Core 8 backend foundation for a secure multi-tenant accounting SaaS.

## Structure

- `src/AccountingSaaS.Api` - controllers, middleware, auth/CORS/Swagger/Serilog setup
- `src/AccountingSaaS.Application` - DTOs, interfaces, validators, permission authorization
- `src/AccountingSaaS.Domain` - entities, role and permission constants
- `src/AccountingSaaS.Infrastructure` - EF Core, Identity, JWT, tenant services, seed data
- `src/AccountingSaaS.Shared` - standard response and pagination models

## Run Migrations

Install the EF tool if needed:

```powershell
dotnet tool install --global dotnet-ef
```

Create and apply the initial migration:

```powershell
dotnet ef migrations add InitialCreate --project src/AccountingSaaS.Infrastructure --startup-project src/AccountingSaaS.Api
dotnet ef database update --project src/AccountingSaaS.Infrastructure --startup-project src/AccountingSaaS.Api
```

Run the API:

```powershell
dotnet run --project src/AccountingSaaS.Api
```

Swagger is available at `/swagger` in development.

## Tenant Isolation

Tenant-owned entities implement `ITenantEntity` or inherit `TenantEntity`. `AppDbContext` applies a global query filter for the current tenant and soft deletes, sets `TenantId` automatically for new tenant-owned rows, prevents saving tenant-owned rows without a tenant, and blocks changing `TenantId` after creation.

`Tenant` itself is not tenant-owned and is not tenant-filtered.

## SuperAdmin Login

On startup, seed data creates roles, permissions, role-permission mappings, and the SuperAdmin from `SeedAdmin` settings.

Default development credentials:

```text
admin@accountingsaas.local
ChangeMe123!ChangeMe123!
```

Call `POST /api/auth/login` with email and password. The response includes the JWT access token, refresh token, roles, permissions, and user info.

## Create Tenant

Use the SuperAdmin token in Swagger or an HTTP client:

```http
POST /api/tenants
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "companyName": "Example Company",
  "commercialRegistrationNo": null,
  "taxNumber": null,
  "address": null,
  "phone": null,
  "email": "company@example.com"
}
```

## Select Tenant

SuperAdmin, AccountingOfficeAdmin, and Accountant can select a tenant per request using:

```http
X-Tenant-Id: {tenantGuid}
```

The middleware validates access. Normal tenant users ignore this header and use the `tenant_id` claim from their JWT.

## Add A Tenant-Owned Entity Later

Create an entity that inherits `TenantEntity`:

```csharp
public sealed class JournalEntry : TenantEntity
{
    public string Number { get; set; } = string.Empty;
}
```

Add it to `AppDbContext`, add an `IEntityTypeConfiguration`, and create a migration. The tenant filter, audit fields, soft delete, and tenant assignment rules will apply automatically.

## Phase 2 Accounting Closing Workflow

Phase 2 adds tenant-owned accounting and closing modules:

- Financial years and accounting periods
- Chart of accounts and cost centers
- Journal entries and journal lines
- Document upload/download/delete
- Closing checklist templates and generated tasks
- Closing submissions and approval workflow
- Trial balance, general ledger, account statement, and closing progress reports

All accounting entities inherit `TenantEntity`. The API never accepts `TenantId` for accounting records; tenant context is resolved through the authenticated user and `X-Tenant-Id` for authorized office users.

Typical workflow:

1. Create a financial year.
2. Create accounting periods inside that year.
3. Create chart of accounts and cost centers.
4. Upload supporting documents.
5. Create balanced draft journal entries with at least two lines.
6. Post journal entries while the period is open.
7. Create the default closing checklist template with `POST /api/closing-checklist/templates/default`.
8. Generate tasks for a period.
9. Submit, approve, or reject closing tasks.
10. Submit the period closing for review.
11. Review and approve the closing submission.
12. Close the period.

Closing safeguards:

- Closed or locked periods block journal entry posting and normal accounting edits.
- Draft or unbalanced journal entries block period closing.
- Required closing tasks must be approved or marked not applicable.
- Closing submission must be approved before the period is closed.
- Reopening requires the reopen permission and a reason.

Document settings are configured in `Documents`:

```json
{
  "StorageRoot": "App_Data/Documents",
  "MaxUploadBytes": 10485760
}
```

After Phase 2 changes, create a new migration:

```powershell
dotnet ef migrations add Phase2AccountingClosing --project src/AccountingSaaS.Infrastructure --startup-project src/AccountingSaaS.Api
dotnet ef database update --project src/AccountingSaaS.Infrastructure --startup-project src/AccountingSaaS.Api
```

## Phase 3 Excel Import Workflow

Phase 3 adds a secure staged Excel import module. Uploaded Excel data is never written directly into final accounting tables.

Workflow:

1. Download a template with `GET /api/import/templates/{importType}`.
2. Upload a `.xlsx` file with `POST /api/import/upload`.
3. The API validates extension, MIME type, size, row count, and column count.
4. Rows are parsed into `ImportBatch` and `ImportBatchRow` staging tables.
5. The upload response returns a preview with the first 50 row validation results.
6. Review full details with `GET /api/import/batches/{id}`.
7. Confirm with `POST /api/import/batches/{id}/confirm`.
8. Only valid or warning rows are inserted into final tables, inside a transaction.
9. Invalid rows are skipped and block confirm while present.
10. Cancel unimported batches with `POST /api/import/batches/{id}/cancel`.

Supported import types:

- `ChartOfAccounts`
- `OpeningBalances`
- `Customers` staged only until a customer module exists
- `Suppliers` staged only until a supplier module exists
- `CostCenters`
- `JournalEntries`
- `BankTransactions` staged only until a bank module exists

Security notes:

- Only `.xlsx` is accepted.
- `.xls`, `.csv`, executable, archive, macro, and unknown formats are rejected.
- Files are stored under `Imports:StorageRoot`, outside `wwwroot` by default.
- Stored file names are random GUID names.
- `TenantId` is never accepted from the Excel file or request body.
- All staged and confirmed import rows inherit `TenantEntity` and use the current tenant context.
- Formula cells are read through cached/calculated values, not executed.
- Upload, validation, confirm, cancel, failure, and template download actions are audited.

Import settings:

```json
{
  "Imports": {
    "StorageRoot": "App_Data/Imports",
    "MaxUploadBytes": 10485760,
    "MaxRows": 5000,
    "MaxColumns": 80
  }
}
```

After Phase 3 changes, create a new migration:

```powershell
dotnet ef migrations add Phase3ExcelImports --project src/AccountingSaaS.Infrastructure --startup-project src/AccountingSaaS.Api
dotnet ef database update --project src/AccountingSaaS.Infrastructure --startup-project src/AccountingSaaS.Api
```

## Centralized Lookups

The lookup module provides lightweight dropdown/autocomplete data from a single API surface.

Generic endpoint:

```http
GET /api/lookups/{lookupType}?search=cash&take=20&activeOnly=true
```

Friendly endpoints:

```http
GET /api/lookups/tenants
GET /api/lookups/users
GET /api/lookups/roles
GET /api/lookups/permissions
GET /api/lookups/financial-years
GET /api/lookups/accounting-periods?financialYearId={id}
GET /api/lookups/chart-of-accounts
GET /api/lookups/posting-accounts?search=cash
GET /api/lookups/parent-accounts?parentId={currentAccountId}
GET /api/lookups/cost-centers
GET /api/lookups/account-types
GET /api/lookups/normal-balances
GET /api/lookups/document-types
GET /api/lookups/journal-entry-statuses
GET /api/lookups/accounting-period-statuses
GET /api/lookups/closing-task-statuses
GET /api/lookups/closing-submission-statuses
GET /api/lookups/import-types
```

All lookup endpoints require authentication. Tenant selector data is limited to tenants the current user can access. Tenant-scoped lookups use the current tenant from `X-Tenant-Id` or the user tenant claim; `TenantId` is never accepted in lookup requests.

Lookup responses use:

```json
{
  "id": "guid",
  "label": "display text",
  "labelAr": "Arabic label",
  "labelEn": "English label",
  "code": "optional code",
  "extra": "optional metadata",
  "isActive": true
}
```

`take` defaults to `50` and is capped at `200`.
