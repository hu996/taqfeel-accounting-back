using System.Reflection;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AccountingSaaS.Api.Swagger;

public sealed class AccountingSwaggerOperationFilter : IOperationFilter
{

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var controller = context.ApiDescription.ActionDescriptor.RouteValues["controller"] ?? string.Empty;
        var action = context.ApiDescription.ActionDescriptor.RouteValues["action"] ?? context.MethodInfo.Name;
        var route = "/" + (context.ApiDescription.RelativePath ?? string.Empty);
        var httpMethod = context.ApiDescription.HttpMethod ?? "GET";

        var permissions = GetPermissions(context.MethodInfo).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var allowAnonymous = HasAttribute<AllowAnonymousAttribute>(context.MethodInfo);
        var authRequired = !allowAnonymous && (permissions.Count > 0 || HasAttribute<AuthorizeAttribute>(context.MethodInfo));
        var tenantRequired = IsTenantScoped(controller, action);
        var isFile = IsFileEndpoint(controller, action);
        var responseDataType = ResolveResponseDataType(controller, action);

        operation.OperationId = BuildOperationId(controller, action);
        operation.Summary = BuildSummary(controller, action, httpMethod);
        operation.Description = BuildDescription(route, permissions, authRequired, tenantRequired, isFile);

        DocumentParameters(operation, context, tenantRequired);
        DocumentResponses(operation, context, responseDataType, isFile, authRequired, permissions.Count > 0);
        DocumentRequestBody(operation, context, controller, action);
        DocumentSecurity(operation, authRequired);
    }

    private static IEnumerable<string> GetPermissions(MethodInfo methodInfo)
    {
        var attributes = methodInfo.DeclaringType?.GetCustomAttributes(true).Concat(methodInfo.GetCustomAttributes(true)) ?? methodInfo.GetCustomAttributes(true);
        foreach (var attribute in attributes)
        {
            switch (attribute)
            {
                case AuthorizeAttribute { Policy: { } policy } when policy.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase):
                    yield return policy["Permission:".Length..];
                    break;
                case RequiredPermissionAttribute requiredPermission:
                    yield return requiredPermission.Permission;
                    break;
            }
        }
    }

    private static bool HasAttribute<T>(MethodInfo methodInfo) where T : Attribute
    {
        var actionHasAttribute = methodInfo.GetCustomAttributes(true).OfType<T>().Any();
        var controllerHasAttribute = methodInfo.DeclaringType?.GetCustomAttributes(true).OfType<T>().Any() == true;
        return actionHasAttribute || controllerHasAttribute;
    }

    private static bool IsTenantScoped(string controller, string action)
    {
        if (controller == "Auth")
        {
            return false;
        }

        if (controller == "Lookups" && action is "Tenants" or "Roles" or "Permissions" or "Get")
        {
            return false;
        }

        return true;
    }

    private static bool IsFileEndpoint(string controller, string action)
    {
        return controller == "Documents" && action == "Download"
               || controller == "Import" && action == "Template";
    }

    private static Type ResolveResponseDataType(string controller, string action)
    {
        return (controller, action) switch
        {
        ("Auth", "Login") => typeof(LoginResponseDto),
        ("Auth", "RefreshToken") => typeof(LoginResponseDto),
        ("Auth", "Logout") => typeof(object),
        ("Session", "GetContext") => typeof(SessionContextDto),
        ("Session", "SwitchActiveFinancialYear") => typeof(LoginResponseDto),

        ("Tenants", "Get") => typeof(IReadOnlyList<TenantDto>),
        ("Tenants", "GetById") => typeof(TenantDto),
        ("Tenants", "Create") => typeof(TenantDto),
        ("Tenants", "Update") => typeof(TenantDto),
        ("Tenants", "Activate") => typeof(TenantDto),
        ("Tenants", "Deactivate") => typeof(TenantDto),

        ("Users", "Get") => typeof(IReadOnlyList<UserDto>),
        ("Users", "Create") => typeof(UserDto),
        ("Users", "Update") => typeof(UserDto),
        ("Users", "AssignRoles") => typeof(object),
        ("Users", "AssignTenantAccess") => typeof(object),

        ("Lookups", _) => typeof(IReadOnlyList<LookupDto>),

        ("FinancialYears", "GetPaged") => typeof(PaginatedResult<FinancialYearDto>),
        ("FinancialYears", _) => typeof(FinancialYearDto),
        ("AccountingPeriods", "GetPaged") => typeof(PaginatedResult<AccountingPeriodDto>),
        ("AccountingPeriods", _) => typeof(AccountingPeriodDto),
        ("ChartOfAccounts", "GetPaged") => typeof(PaginatedResult<AccountDto>),
        ("ChartOfAccounts", "GetTree") => typeof(IReadOnlyList<AccountDto>),
        ("ChartOfAccounts", _) => typeof(AccountDto),
        ("CostCenters", "GetPaged") => typeof(PaginatedResult<CostCenterDto>),
        ("CostCenters", _) => typeof(CostCenterDto),
        ("JournalEntries", "GetPaged") => typeof(PaginatedResult<JournalEntryDto>),
        ("JournalEntries", _) => typeof(JournalEntryDto),
        ("Documents", "GetPaged") => typeof(PaginatedResult<DocumentDto>),
        ("Documents", "Upload") => typeof(DocumentDto),
        ("Documents", "Delete") => typeof(object),

        ("ClosingChecklist", "CreateTemplate") => typeof(ClosingChecklistTemplateDto),
        ("ClosingChecklist", "CreateDefaultTemplate") => typeof(ClosingChecklistTemplateDto),
        ("ClosingChecklist", "UpdateTemplate") => typeof(ClosingChecklistTemplateDto),
        ("ClosingChecklist", "AddItem") => typeof(ClosingChecklistTemplateItemDto),
        ("ClosingChecklist", "UpdateItem") => typeof(ClosingChecklistTemplateItemDto),
        ("ClosingChecklist", "GenerateTasks") => typeof(IReadOnlyList<ClosingTaskDto>),

        ("ClosingTasks", "ByPeriod") => typeof(IReadOnlyList<ClosingTaskDto>),
        ("ClosingTasks", _) => typeof(ClosingTaskDto),
        ("ClosingSubmissions", _) => typeof(ClosingSubmissionDto),

        ("AccountingReports", "TrialBalance") => typeof(IReadOnlyList<TrialBalanceRowDto>),
        ("AccountingReports", "GeneralLedger") => typeof(LedgerReportDto),
        ("AccountingReports", "AccountStatement") => typeof(LedgerReportDto),
        ("AccountingReports", "ClosingProgress") => typeof(ClosingProgressDto),

        ("Import", "Upload") => typeof(ImportPreviewDto),
        ("Import", "GetBatches") => typeof(PaginatedResult<ImportBatchSummaryDto>),
        ("Import", "GetBatch") => typeof(ImportBatchDetailsDto),
        ("Import", "Confirm") => typeof(ImportBatchSummaryDto),
        ("Import", "Cancel") => typeof(ImportBatchSummaryDto),

        ("AuditLogs", "Get") => typeof(IReadOnlyList<AuditLogDto>),
            _ => typeof(object)
        };
    }

    private static string BuildOperationId(string controller, string action)
    {
        var endpointName = ResolveEndpointName(controller, action);
        return endpointName.OperationId;
    }

    private static string BuildSummary(string controller, string action, string httpMethod)
    {
        var endpointName = ResolveEndpointName(controller, action);
        if (!string.IsNullOrWhiteSpace(endpointName.Summary))
        {
            return endpointName.Summary;
        }

        return BuildFallbackSummary(controller, action, httpMethod);
    }

    private static (string OperationId, string Summary) ResolveEndpointName(string controller, string action)
    {
        (string OperationId, string Summary) explicitName = (controller, action) switch
        {
            ("Auth", "Login") => ("LoginWithEmailAndPassword", "Login with email and password"),
            ("Auth", "RefreshToken") => ("RefreshAccessToken", "Refresh access token"),
            ("Auth", "Logout") => ("LogoutCurrentSession", "Logout current session"),
            ("Auth", "Me") => ("GetCurrentUser", "Get current user"),

            ("Tenants", "Get") => ("ListAccessibleTenants", "List tenants accessible to current user"),
            ("Tenants", "GetById") => ("GetTenantById", "Get tenant details by ID"),
            ("Tenants", "Create") => ("AddTenant", "Add a new tenant"),
            ("Tenants", "Update") => ("UpdateTenant", "Update tenant details"),
            ("Tenants", "Activate") => ("ActivateTenant", "Activate tenant"),
            ("Tenants", "Deactivate") => ("DeactivateTenant", "Deactivate tenant"),

            ("Users", "Get") => ("ListUsersInCurrentScope", "List users visible to current user"),
            ("Users", "Create") => ("AddUser", "Add user account"),
            ("Users", "Update") => ("UpdateUserAccount", "Update user account"),
            ("Users", "AssignRoles") => ("ReplaceUserRoles", "Replace assigned roles for a user"),
            ("Users", "AssignTenantAccess") => ("ReplaceUserTenantAccess", "Replace tenant access for a user"),

            ("Lookups", "Get") => ("GetLookupByType", "Get lookup by type"),
            ("Lookups", "Tenants") => ("GetTenantsLookup", "Get tenants lookup"),
            ("Lookups", "Users") => ("GetUsersLookup", "Get users lookup"),
            ("Lookups", "Roles") => ("GetRolesLookup", "Get roles lookup"),
            ("Lookups", "Permissions") => ("GetPermissionsLookup", "Get permissions lookup"),
            ("Lookups", "FinancialYears") => ("GetFinancialYearsLookup", "Get financial years lookup"),
            ("Lookups", "AccountingPeriods") => ("GetAccountingPeriodsLookup", "Get accounting periods lookup"),
            ("Lookups", "ChartOfAccounts") => ("GetChartOfAccountsLookup", "Get chart of accounts lookup"),
            ("Lookups", "PostingAccounts") => ("GetPostingAccountsLookup", "Get posting accounts lookup"),
            ("Lookups", "ParentAccounts") => ("GetParentAccountsLookup", "Get parent accounts lookup"),
            ("Lookups", "CostCenters") => ("GetCostCentersLookup", "Get cost centers lookup"),
            ("Lookups", "AccountTypes") => ("GetAccountTypesLookup", "Get account types lookup"),
            ("Lookups", "NormalBalances") => ("GetNormalBalancesLookup", "Get normal balances lookup"),
            ("Lookups", "DocumentTypes") => ("GetDocumentTypesLookup", "Get document types lookup"),
            ("Lookups", "JournalEntryStatuses") => ("GetJournalEntryStatusesLookup", "Get journal entry statuses lookup"),
            ("Lookups", "AccountingPeriodStatuses") => ("GetAccountingPeriodStatusesLookup", "Get accounting period statuses lookup"),
            ("Lookups", "ClosingTaskStatuses") => ("GetClosingTaskStatusesLookup", "Get closing task statuses lookup"),
            ("Lookups", "ClosingSubmissionStatuses") => ("GetClosingSubmissionStatusesLookup", "Get closing submission statuses lookup"),
            ("Lookups", "ImportTypes") => ("GetImportTypesLookup", "Get import types lookup"),

            ("FinancialYears", "GetPaged") => ("GetFinancialYearsByFilter", "Get financial years by filter"),
            ("FinancialYears", "Get") => ("GetFinancialYearById", "Get financial year by ID"),
            ("FinancialYears", "Create") => ("AddFinancialYear", "Add financial year"),
            ("FinancialYears", "Update") => ("UpdateFinancialYear", "Update financial year"),
            ("FinancialYears", "Close") => ("CloseFinancialYear", "Close financial year"),

            ("AccountingPeriods", "GetPaged") => ("GetAccountingPeriodsByFilter", "Get accounting periods by filter"),
            ("AccountingPeriods", "Get") => ("GetAccountingPeriodById", "Get accounting period by ID"),
            ("AccountingPeriods", "Create") => ("AddAccountingPeriod", "Add accounting period"),
            ("AccountingPeriods", "Update") => ("UpdateAccountingPeriod", "Update accounting period"),
            ("AccountingPeriods", "Lock") => ("LockAccountingPeriod", "Lock accounting period"),
            ("AccountingPeriods", "SubmitForReview") => ("SubmitAccountingPeriodForReview", "Submit accounting period for review"),
            ("AccountingPeriods", "Close") => ("CloseAccountingPeriod", "Close accounting period"),
            ("AccountingPeriods", "Reopen") => ("ReopenAccountingPeriod", "Reopen accounting period"),

            ("ChartOfAccounts", "GetPaged") => ("GetAccountsByFilter", "Get accounts by filter"),
            ("ChartOfAccounts", "GetTree") => ("GetAccountsTree", "Get accounts tree"),
            ("ChartOfAccounts", "Get") => ("GetAccountById", "Get account by ID"),
            ("ChartOfAccounts", "Create") => ("AddAccount", "Add account"),
            ("ChartOfAccounts", "Update") => ("UpdateAccount", "Update account"),
            ("ChartOfAccounts", "Activate") => ("ActivateAccount", "Activate account"),
            ("ChartOfAccounts", "Deactivate") => ("DeactivateAccount", "Deactivate account"),

            ("CostCenters", "GetPaged") => ("GetCostCentersByFilter", "Get cost centers by filter"),
            ("CostCenters", "Create") => ("AddCostCenter", "Add cost center"),
            ("CostCenters", "Update") => ("UpdateCostCenter", "Update cost center"),
            ("CostCenters", "Activate") => ("ActivateCostCenter", "Activate cost center"),
            ("CostCenters", "Deactivate") => ("DeactivateCostCenter", "Deactivate cost center"),

            ("JournalEntries", "GetPaged") => ("GetJournalEntriesByFilter", "Get journal entries by filter"),
            ("JournalEntries", "Get") => ("GetJournalEntryById", "Get journal entry by ID"),
            ("JournalEntries", "CreateDraft") => ("AddJournalEntryDraft", "Add journal entry draft"),
            ("JournalEntries", "UpdateDraft") => ("UpdateDraftJournalEntry", "Update draft journal entry"),
            ("JournalEntries", "Post") => ("PostJournalEntry", "Post journal entry"),
            ("JournalEntries", "Reverse") => ("ReverseJournalEntry", "Reverse journal entry"),
            ("JournalEntries", "Cancel") => ("CancelJournalEntry", "Cancel journal entry"),

            ("Documents", "GetPaged") => ("GetDocumentsByFilter", "Get documents by filter"),
            ("Documents", "Upload") => ("UploadDocument", "Upload document"),
            ("Documents", "Download") => ("DownloadDocumentFile", "Download document file"),
            ("Documents", "Delete") => ("DeleteDocument", "Delete document"),

            ("ClosingChecklist", "CreateTemplate") => ("AddClosingChecklistTemplate", "Add closing checklist template"),
            ("ClosingChecklist", "CreateDefaultTemplate") => ("AddDefaultClosingChecklistTemplate", "Add default closing checklist template"),
            ("ClosingChecklist", "UpdateTemplate") => ("UpdateClosingChecklistTemplate", "Update closing checklist template"),
            ("ClosingChecklist", "AddItem") => ("AddClosingChecklistTemplateItem", "Add closing checklist template item"),
            ("ClosingChecklist", "UpdateItem") => ("UpdateClosingChecklistTemplateItem", "Update closing checklist template item"),
            ("ClosingChecklist", "GenerateTasks") => ("GenerateClosingTasks", "Generate closing tasks"),

            ("ClosingTasks", "ByPeriod") => ("ListClosingTasksByPeriod", "List closing tasks by accounting period"),
            ("ClosingTasks", "Assign") => ("AssignClosingTask", "Assign closing task"),
            ("ClosingTasks", "Start") => ("StartClosingTask", "Start closing task"),
            ("ClosingTasks", "Submit") => ("SubmitClosingTask", "Submit closing task"),
            ("ClosingTasks", "Approve") => ("ApproveClosingTask", "Approve closing task"),
            ("ClosingTasks", "Reject") => ("RejectClosingTask", "Reject closing task"),
            ("ClosingTasks", "NotApplicable") => ("MarkClosingTaskNotApplicable", "Mark closing task as not applicable"),

            ("ClosingSubmissions", "ByPeriod") => ("GetClosingSubmissionByPeriod", "Get closing submission by accounting period"),
            ("ClosingSubmissions", "Submit") => ("SubmitClosingSubmission", "Submit closing submission"),
            ("ClosingSubmissions", "StartReview") => ("StartClosingSubmissionReview", "Start closing submission review"),
            ("ClosingSubmissions", "Approve") => ("ApproveClosingSubmission", "Approve closing submission"),
            ("ClosingSubmissions", "Reject") => ("RejectClosingSubmission", "Reject closing submission"),
            ("ClosingSubmissions", "ClosePeriod") => ("ClosePeriodFromClosingSubmission", "Close period from closing submission"),
            ("ClosingSubmissions", "ReopenPeriod") => ("ReopenPeriodFromClosingSubmission", "Reopen period from closing submission"),

            ("AccountingReports", "TrialBalance") => ("GetTrialBalanceReport", "Get trial balance report"),
            ("AccountingReports", "GeneralLedger") => ("GetGeneralLedgerReport", "Get general ledger report"),
            ("AccountingReports", "AccountStatement") => ("GetAccountStatementReport", "Get account statement report"),
            ("AccountingReports", "ClosingProgress") => ("GetClosingProgressReport", "Get closing progress report"),

            ("Import", "Upload") => ("UploadImportFile", "Upload import file"),
            ("Import", "GetBatches") => ("GetImportBatchesByFilter", "Get import batches by filter"),
            ("Import", "GetBatch") => ("GetImportBatchById", "Get import batch by ID"),
            ("Import", "Confirm") => ("ConfirmImportBatch", "Confirm import batch"),
            ("Import", "Cancel") => ("CancelImportBatch", "Cancel import batch"),
            ("Import", "Template") => ("DownloadImportTemplate", "Download import template"),

            ("AuditLogs", "Get") => ("GetAuditLogsByFilter", "Get audit logs by filter"),
            _ => (string.Empty, string.Empty)
        };

        if (!string.IsNullOrWhiteSpace(explicitName.OperationId))
        {
            return explicitName;
        }

        var fallbackSummary = BuildFallbackSummary(controller, action, "GET");
        return ($"{SanitizeIdentifier(action)}{SanitizeIdentifier(controller)}", fallbackSummary);
    }

    private static string BuildFallbackSummary(string controller, string action, string httpMethod)
    {
        var resource = SplitWords(controller);
        var verb = action switch
        {
            "Get" or "GetPaged" or "ByPeriod" or "MyTenants" => "List",
            "GetById" or "GetBatch" => "Get",
            "Create" or "CreateTemplate" or "CreateDefaultTemplate" or "AddItem" => "Create",
            "Update" or "UpdateTemplate" or "UpdateItem" => "Update",
            "Delete" => "Delete",
            "Upload" => "Upload",
            "Download" or "Template" => "Download",
            "Login" => "Login",
            "RefreshToken" => "Refresh token",
            "Logout" => "Logout",
            "Me" => "Get current user",
            _ => SplitWords(action)
        };

        return $"{verb} {resource}".Trim();
    }

    private static string SanitizeIdentifier(string value)
    {
        var words = SplitWords(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]);

        return string.Concat(words);
    }

    private static string BuildDescription(string route, IReadOnlyList<string> permissions, bool authRequired, bool tenantRequired, bool isFile)
    {
        var lines = new List<string>
        {
            $"Route: `{route}`.",
            $"Auth Required: {(authRequired ? "Yes" : "No")}."
        };

        lines.Add(permissions.Count == 0
            ? "Required Permission: None."
            : $"Required Permission: {string.Join(", ", permissions)}.");

        lines.Add(tenantRequired
            ? "Tenant Context: Yes. Tenant and active financial year are read exclusively from JWT claims."
            : "Tenant Context: Not required for this anonymous endpoint.");

        if (isFile)
        {
            lines.Add("Frontend note: this endpoint returns a file/blob. Configure the HTTP client with blob response handling.");
        }

        return string.Join("\n\n", lines);
    }

    private static void DocumentParameters(OpenApiOperation operation, OperationFilterContext context, bool tenantRequired)
    {
        foreach (var parameter in operation.Parameters)
        {
            if (parameter.In == ParameterLocation.Path)
            {
                parameter.Description ??= $"Route parameter `{parameter.Name}`.";
                parameter.Required = true;
            }
            else if (parameter.In == ParameterLocation.Query)
            {
                parameter.Description ??= $"Optional query parameter `{parameter.Name}`.";
            }
        }

    }

    private static void DocumentResponses(OperationFilterContext context, OpenApiOperation operation, string statusCode, string description, Type dataType)
    {
        var responseType = typeof(BaseResponseDto<>).MakeGenericType(dataType);
        operation.Responses[statusCode] = new OpenApiResponse
        {
            Description = description,
            Content =
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = context.SchemaGenerator.GenerateSchema(responseType, context.SchemaRepository)
                }
            }
        };
    }

    private static void DocumentResponses(OpenApiOperation operation, OperationFilterContext context, Type responseDataType, bool isFile, bool authRequired, bool hasPermission)
    {
        operation.Responses.Clear();

        if (isFile)
        {
            operation.Responses["200"] = new OpenApiResponse
            {
                Description = "File/blob returned successfully.",
                Content =
                {
                    ["application/octet-stream"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Type = "string", Format = "binary" }
                    }
                }
            };
        }
        else
        {
            DocumentResponses(context, operation, "200", "Success response wrapped in BaseResponseDto<T>.", responseDataType);
        }

        DocumentResponses(context, operation, "400", "Validation or business rule failure returned as BaseResponseDto<object>.", typeof(object));

        if (authRequired)
        {
            DocumentResponses(context, operation, "401", "Authentication is required or the JWT is invalid/expired.", typeof(object));
        }

        if (hasPermission)
        {
            DocumentResponses(context, operation, "403", "The authenticated user does not have the required permission or tenant access.", typeof(object));
        }

        if ((context.ApiDescription.RelativePath ?? string.Empty).Contains("{", StringComparison.Ordinal))
        {
            DocumentResponses(context, operation, "404", "The requested resource was not found.", typeof(object));
        }
    }

    private static void DocumentRequestBody(OpenApiOperation operation, OperationFilterContext context, string controller, string action)
    {
        if (operation.RequestBody is null)
        {
            return;
        }

        if (controller is "Documents" or "Import" && action == "Upload")
        {
            operation.RequestBody.Description = controller == "Documents"
                ? "multipart/form-data fields: financialYearId, accountingPeriodId, documentType, relatedEntityName, relatedEntityId, notes, and file."
                : "multipart/form-data fields: importType, financialYearId, accountingPeriodId, worksheetName, notes, and file.";
        }
        else
        {
            operation.RequestBody.Description ??= "JSON request body. See schema for required fields and data types.";
        }
    }

    private static void DocumentSecurity(OpenApiOperation operation, bool authRequired)
    {
        operation.Security.Clear();
        if (!authRequired)
        {
            return;
        }

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            }] = []
        });
    }

    private static string SplitWords(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var chars = new List<char> { value[0] };
        foreach (var c in value.Skip(1))
        {
            if (char.IsUpper(c))
            {
                chars.Add(' ');
            }

            chars.Add(c);
        }

        return new string(chars.ToArray());
    }
}
