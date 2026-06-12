# Frontend API Contract

The Angular workspace is not present in this repository. The files under
`docs/frontend-contract` are the integration contract to copy into the real
frontend after it is added to this workspace.

## URL rules

- Development root: `https://localhost:7147/`
- Development API root: `https://localhost:7147/api/`
- Resource values are relative to `baseUrl`.
- Resource values never begin with `/api/`.
- Actions with `{id}` in the mapping append the encoded ID in the API service.
- `X-Tenant-Id` is required when an office-level user selects a company.

## Response rules

- JSON business responses use `BaseResponseDto<T>`.
- Business failures from `AccountingControllerBase` return HTTP 200 with
  `success: false`.
- Authentication/authorization, malformed requests, infrastructure failures,
  and file transfers retain transport-level HTTP semantics.
- File upload actions use `multipart/form-data`.
- File download actions return binary responses rather than
  `BaseResponseDto<T>` on success.

## Mapping

| Module | Resource key | Method | Relative route | Request | Response | Status |
|---|---|---:|---|---|---|---|
| Auth | login | POST | `Auth/Login` | `LoginRequest` | `BaseResponseDto<AuthResponse>` | Matched |
| Auth | refreshToken | POST | `Auth/RefreshToken` | `RefreshTokenRequest` | `BaseResponseDto<AuthResponse>` | Matched |
| Auth | logout | POST | `Auth/Logout` | `LogoutRequest` | `BaseResponseDto<object>` | Matched |
| Auth | me | GET | `Auth/GetCurrentUser` | None | `BaseResponseDto<CurrentUserDto>` | Matched |
| TenantSwitch | myTenants | GET | `TenantSwitch/GetMyTenants` | None | `BaseResponseDto<IReadOnlyList<TenantDto>>` | Matched |
| TenantSwitch | validate | POST | `TenantSwitch/ValidateTenantAccess` | `ValidateTenantRequest` | `BaseResponseDto<TenantDto>` | Matched |
| Tenants | getList | GET | `Tenants/GetTenants` | None | `BaseResponseDto<IReadOnlyList<TenantDto>>` | Matched |
| Tenants | getById | GET | `Tenants/GetTenantById/{id}` | Route ID | `BaseResponseDto<TenantDto>` | Matched |
| Tenants | create | POST | `Tenants/AddTenant` | `CreateTenantRequest` | `BaseResponseDto<TenantDto>` | Matched |
| Tenants | update | PUT | `Tenants/UpdateTenant/{id}` | Route ID + `UpdateTenantRequest` | `BaseResponseDto<TenantDto>` | Matched |
| Tenants | activate | PATCH | `Tenants/ActivateTenant/{id}` | Route ID | `BaseResponseDto<TenantDto>` | Matched |
| Tenants | deactivate | PATCH | `Tenants/DeactivateTenant/{id}` | Route ID | `BaseResponseDto<TenantDto>` | Matched |
| Users | getList | GET | `Users/GetUsers` | None | `BaseResponseDto<IReadOnlyList<UserDto>>` | Matched |
| Users | create | POST | `Users/AddUser` | `CreateUserRequest` | `BaseResponseDto<UserDto>` | Matched |
| Users | update | PUT | `Users/UpdateUser/{id}` | Route ID + `UpdateUserRequest` | `BaseResponseDto<UserDto>` | Matched |
| Users | assignRoles | POST | `Users/AssignRoles/{id}` | Route ID + `AssignRolesRequest` | `BaseResponseDto<object>` | Matched |
| Users | assignTenantAccess | POST | `Users/AssignTenantAccess/{id}` | Route ID + `AssignTenantAccessRequest` | `BaseResponseDto<object>` | Matched |
| FinancialYears | getFilterList | GET | `FinancialYears/GetFinancialYearsByFilter` | Query `AccountingPagedRequest` | `BaseResponseDto<PaginatedResult<FinancialYearDto>>` | Matched |
| FinancialYears | getById | GET | `FinancialYears/GetFinancialYearById/{id}` | Route ID | `BaseResponseDto<FinancialYearDto>` | Matched |
| FinancialYears | create | POST | `FinancialYears/AddFinancialYear` | `CreateFinancialYearRequest` | `BaseResponseDto<FinancialYearDto>` | Matched |
| FinancialYears | update | PUT | `FinancialYears/UpdateFinancialYear/{id}` | Route ID + `UpdateFinancialYearRequest` | `BaseResponseDto<FinancialYearDto>` | Matched |
| FinancialYears | close | POST | `FinancialYears/CloseFinancialYear/{id}` | Route ID | `BaseResponseDto<FinancialYearDto>` | Matched |
| AccountingPeriods | getFilterList | GET | `AccountingPeriods/GetAccountingPeriodsByFilter` | Query `AccountingPagedRequest` | `BaseResponseDto<PaginatedResult<AccountingPeriodDto>>` | Matched |
| AccountingPeriods | getById | GET | `AccountingPeriods/GetAccountingPeriodById/{id}` | Route ID | `BaseResponseDto<AccountingPeriodDto>` | Matched |
| AccountingPeriods | create | POST | `AccountingPeriods/AddAccountingPeriod` | `CreateAccountingPeriodRequest` | `BaseResponseDto<AccountingPeriodDto>` | Matched |
| AccountingPeriods | update | PUT | `AccountingPeriods/UpdateAccountingPeriod/{id}` | Route ID + request | `BaseResponseDto<AccountingPeriodDto>` | Matched |
| AccountingPeriods | lock | POST | `AccountingPeriods/LockAccountingPeriod/{id}` | Route ID | `BaseResponseDto<AccountingPeriodDto>` | Matched |
| AccountingPeriods | submitForReview | POST | `AccountingPeriods/SubmitForReview/{id}` | Route ID | `BaseResponseDto<AccountingPeriodDto>` | Matched |
| AccountingPeriods | close | POST | `AccountingPeriods/CloseAccountingPeriod/{id}` | Route ID | `BaseResponseDto<AccountingPeriodDto>` | Matched |
| AccountingPeriods | reopen | POST | `AccountingPeriods/ReopenAccountingPeriod/{id}` | Route ID + `ReopenPeriodRequest` | `BaseResponseDto<AccountingPeriodDto>` | Matched |
| ChartOfAccounts | getFilterList | GET | `ChartOfAccounts/GetChartOfAccountsByFilter` | Query `AccountingPagedRequest` | `BaseResponseDto<PaginatedResult<AccountDto>>` | Matched |
| ChartOfAccounts | getTree | GET | `ChartOfAccounts/GetChartOfAccountsTree` | None | `BaseResponseDto<IReadOnlyList<AccountDto>>` | Matched |
| ChartOfAccounts | getById | GET | `ChartOfAccounts/GetChartOfAccountById/{id}` | Route ID | `BaseResponseDto<AccountDto>` | Matched |
| ChartOfAccounts | create | POST | `ChartOfAccounts/AddChartOfAccount` | `CreateAccountRequest` | `BaseResponseDto<AccountDto>` | Matched |
| ChartOfAccounts | update | PUT | `ChartOfAccounts/UpdateChartOfAccount/{id}` | Route ID + request | `BaseResponseDto<AccountDto>` | Matched |
| ChartOfAccounts | activate/deactivate | PATCH | `ChartOfAccounts/(Activate|Deactivate)ChartOfAccount/{id}` | Route ID | `BaseResponseDto<AccountDto>` | Matched |
| CostCenters | getFilterList | GET | `CostCenters/GetCostCentersByFilter` | Query `AccountingPagedRequest` | `BaseResponseDto<PaginatedResult<CostCenterDto>>` | Matched |
| CostCenters | create/update | POST/PUT | `CostCenters/(Add|Update)CostCenter[/{id}]` | Typed request | `BaseResponseDto<CostCenterDto>` | Matched |
| CostCenters | activate/deactivate | PATCH | `CostCenters/(Activate|Deactivate)CostCenter/{id}` | Route ID | `BaseResponseDto<CostCenterDto>` | Matched |
| JournalEntries | getFilterList | GET | `JournalEntries/GetJournalEntriesByFilter` | Query `AccountingPagedRequest` | `BaseResponseDto<PaginatedResult<JournalEntryDto>>` | Matched |
| JournalEntries | getById | GET | `JournalEntries/GetJournalEntryById/{id}` | Route ID | `BaseResponseDto<JournalEntryDto>` | Matched |
| JournalEntries | create | POST | `JournalEntries/AddJournalEntry` | `CreateJournalEntryRequest` | `BaseResponseDto<JournalEntryDto>` | Matched |
| JournalEntries | update | PUT | `JournalEntries/UpdateJournalEntry/{id}` | Route ID + `UpdateJournalEntryRequest` | `BaseResponseDto<JournalEntryDto>` | Matched |
| JournalEntries | post | POST | `JournalEntries/PostJournalEntry/{id}` | Route ID + `PostJournalEntryRequest` | `BaseResponseDto<JournalEntryDto>` | Matched |
| JournalEntries | reverse | POST | `JournalEntries/ReverseJournalEntry/{id}` | Route ID + `ReverseJournalEntryRequest` | `BaseResponseDto<JournalEntryDto>` | Matched |
| JournalEntries | cancel | POST | `JournalEntries/CancelJournalEntry/{id}` | Route ID | `BaseResponseDto<JournalEntryDto>` | Matched |
| ClosingChecklist | all keys | POST/PUT | `ClosingChecklist/{action}[/{id}]` | Typed request | `BaseResponseDto<T>` | Matched |
| ClosingTasks | all keys | GET/POST | `ClosingTasks/{action}/{id}` | Route ID + optional typed request | `BaseResponseDto<T>` | Matched |
| ClosingSubmissions | all keys | GET/POST | `ClosingSubmissions/{action}/{accountingPeriodId}` | Route ID + optional typed request | `BaseResponseDto<T>` | Matched |
| Imports | upload | POST | `Imports/Upload` | Multipart form | `BaseResponseDto<ImportPreviewDto>` | Matched |
| Imports | getFilterList/getById | GET | `Imports/GetImportsByFilter`, `Imports/GetImportById/{id}` | Query/route | `BaseResponseDto<T>` | Matched |
| Imports | commit/cancel | POST | `Imports/(Commit|Cancel)/{id}` | Route ID + typed request | `BaseResponseDto<ImportBatchSummaryDto>` | Matched |
| Imports | downloadTemplate | GET | `Imports/DownloadTemplate/{importType}` | Route enum | File | Matched |
| Documents | all keys | Mixed | `Documents/{action}[/{id}]` | Query/multipart/route | Wrapper or file | Matched |
| AccountingReports | all keys | GET | `AccountingReports/{action}[/{accountingPeriodId}]` | Query/route | `BaseResponseDto<T>` | Matched |
| Lookups | all keys | GET | `Lookups/{action}` | Query `LookupRequest` | `BaseResponseDto<IReadOnlyList<LookupDto>>` | Matched |

## Missing modules

No backend controller currently exists for `Dashboard`, standalone `Roles`,
standalone `Permissions`, or a separate import `Validate` action. Roles and
permissions are currently exposed through `Users` and `Lookups`. These resource
keys must not be added to the Angular environment until corresponding backend
actions exist.

## Frontend audit status

No Angular files were available to inspect, so hardcoded URL removal, loading
`finalize` fixes, component error handling, button normalization, and runtime
frontend flow testing remain blocked on adding the actual Angular workspace.
