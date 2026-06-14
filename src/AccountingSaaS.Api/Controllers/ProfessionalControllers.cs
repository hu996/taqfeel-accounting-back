using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController, Route("api/Notifications")]
public sealed class NotificationsController(INotificationService service) : AccountingControllerBase
{
    [HttpGet("GetMyNotifications"), HasPermission("Notifications.View")]
    public async Task<IActionResult> Get([FromQuery] PaginationRequest request, CancellationToken ct) => ApiResult(await service.GetMyNotificationsAsync(request, ct));
    [HttpGet("GetUnreadCount"), HasPermission("Notifications.View")]
    public async Task<IActionResult> Count(CancellationToken ct) => ApiResult(await service.GetUnreadCountAsync(ct));
    [HttpPost("MarkAsRead/{id:guid}"), HasPermission("Notifications.View")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct) => ApiResult(await service.MarkAsReadAsync(id, ct));
}

[ApiController, Route("api/Activities")]
public sealed class ActivitiesController(IActivityService service) : AccountingControllerBase
{
    [HttpGet("GetLatest"), HasPermission("Activities.View")]
    public async Task<IActionResult> Get([FromQuery] int take, CancellationToken ct) => ApiResult(await service.GetLatestAsync(take, ct));
}

[ApiController, Route("api/Workflows")]
public sealed class WorkflowsController(IDynamicWorkflowService service) : AccountingControllerBase
{
    [HttpGet("GetDefinitions"), HasPermission("Workflows.View")]
    public async Task<IActionResult> Get(CancellationToken ct) => ApiResult(await service.GetDefinitionsAsync(ct));
    [HttpPost("AddDefinition"), HasPermission("Workflows.Manage")]
    public async Task<IActionResult> Add(SaveWorkflowDefinitionRequest request, CancellationToken ct) => ApiResult(await service.SaveDefinitionAsync(null, request, ct));
    [HttpPut("UpdateDefinition/{id:guid}"), HasPermission("Workflows.Manage")]
    public async Task<IActionResult> Update(Guid id, SaveWorkflowDefinitionRequest request, CancellationToken ct) => ApiResult(await service.SaveDefinitionAsync(id, request, ct));
}

[ApiController, Route("api/Comments")]
public sealed class CommentsController(ICommentService service) : AccountingControllerBase
{
    [HttpGet("Get"), HasPermission("Comments.View")]
    public async Task<IActionResult> Get([FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken ct) => ApiResult(await service.GetAsync(entityType, entityId, ct));
    [HttpPost("Add"), HasPermission("Comments.Create")]
    public async Task<IActionResult> Add(CreateCommentRequest request, CancellationToken ct) => ApiResult(await service.AddAsync(request, ct));
    [HttpDelete("Delete/{id:guid}"), HasPermission("Comments.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => ApiResult(await service.DeleteAsync(id, ct));
}

[ApiController, Route("api/Search")]
public sealed class SearchController(IUniversalSearchService service) : AccountingControllerBase
{
    [HttpGet("Universal"), HasPermission("Search.Use")]
    public async Task<IActionResult> Search([FromQuery] UniversalSearchRequest request, CancellationToken ct) => ApiResult(await service.SearchAsync(request, ct));
}

[ApiController, Route("api/CustomFields")]
public sealed class CustomFieldsController(ICustomFieldService service) : AccountingControllerBase
{
    [HttpGet("GetDefinitions"), HasPermission("CustomFields.View")]
    public async Task<IActionResult> Get([FromQuery] string entityType, CancellationToken ct) => ApiResult(await service.GetDefinitionsAsync(entityType, ct));
    [HttpPost("AddDefinition"), HasPermission("CustomFields.Manage")]
    public async Task<IActionResult> Add(SaveCustomFieldDefinitionRequest request, CancellationToken ct) => ApiResult(await service.SaveDefinitionAsync(null, request, ct));
    [HttpPut("UpdateDefinition/{id:guid}"), HasPermission("CustomFields.Manage")]
    public async Task<IActionResult> Update(Guid id, SaveCustomFieldDefinitionRequest request, CancellationToken ct) => ApiResult(await service.SaveDefinitionAsync(id, request, ct));
    [HttpPost("SaveValues"), HasPermission("CustomFields.EditValues")]
    public async Task<IActionResult> SaveValues(SaveCustomFieldValuesRequest request, CancellationToken ct) => ApiResult(await service.SaveValuesAsync(request, ct));
}

[ApiController, Route("api/DocumentNumberTemplates")]
public sealed class DocumentNumberTemplatesController(IDocumentNumberService service) : AccountingControllerBase
{
    [HttpPost("Add"), HasPermission("DocumentTemplates.Manage")]
    public async Task<IActionResult> Add(SaveDocumentNumberTemplateRequest request, CancellationToken ct) => ApiResult(await service.SaveTemplateAsync(null, request, ct));
    [HttpPut("Update/{id:guid}"), HasPermission("DocumentTemplates.Manage")]
    public async Task<IActionResult> Update(Guid id, SaveDocumentNumberTemplateRequest request, CancellationToken ct) => ApiResult(await service.SaveTemplateAsync(id, request, ct));
}

[ApiController, Route("api/OpeningBalances")]
public sealed class OpeningBalancesController(IOpeningBalanceService service) : AccountingControllerBase
{
    [HttpPost("Add"), HasPermission("OpeningBalances.Create")]
    public async Task<IActionResult> Add(CreateOpeningBalanceBatchRequest request, CancellationToken ct) => ApiResult(await service.CreateAsync(request, ct));
    [HttpPost("Submit/{id:guid}"), HasPermission("OpeningBalances.Submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct) => ApiResult(await service.SubmitAsync(id, ct));
    [HttpPost("Approve/{id:guid}"), HasPermission("OpeningBalances.Approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => ApiResult(await service.ApproveAsync(id, ct));
}

[ApiController, Route("api/BankReconciliation")]
public sealed class BankReconciliationController(IBankReconciliationService service) : AccountingControllerBase
{
    [HttpPost("AddBankAccount"), HasPermission("BankReconciliation.Manage")]
    public async Task<IActionResult> AddBank(BankAccountRequest request, CancellationToken ct) => ApiResult(await service.CreateBankAccountAsync(request, ct));
    [HttpPost("AddStatement"), HasPermission("BankReconciliation.Manage")]
    public async Task<IActionResult> AddStatement(BankStatementRequest request, CancellationToken ct) => ApiResult(await service.AddStatementAsync(request, ct));
    [HttpPost("Create"), HasPermission("BankReconciliation.Manage")]
    public async Task<IActionResult> Create(CreateBankReconciliationRequest request, CancellationToken ct) => ApiResult(await service.CreateAsync(request, ct));
    [HttpPost("Match/{id:guid}"), HasPermission("BankReconciliation.Match")]
    public async Task<IActionResult> Match(Guid id, MatchBankStatementRequest request, CancellationToken ct) => ApiResult(await service.MatchAsync(id, request, ct));
    [HttpDelete("Unmatch/{id:guid}/{statementId:guid}"), HasPermission("BankReconciliation.Match")]
    public async Task<IActionResult> Unmatch(Guid id, Guid statementId, CancellationToken ct) => ApiResult(await service.UnmatchAsync(id, statementId, ct));
    [HttpGet("Differences/{id:guid}"), HasPermission("BankReconciliation.View")]
    public async Task<IActionResult> Differences(Guid id, CancellationToken ct) => ApiResult(await service.GetDifferencesAsync(id, ct));
}

[ApiController, Route("api/FixedAssets")]
public sealed class FixedAssetsController(IFixedAssetService service) : AccountingControllerBase
{
    [HttpPost("Add"), HasPermission("FixedAssets.Manage")]
    public async Task<IActionResult> Add(FixedAssetRequest request, CancellationToken ct) => ApiResult(await service.CreateAsync(request, ct));
    [HttpPost("RunDepreciation/{periodId:guid}"), HasPermission("FixedAssets.RunDepreciation")]
    public async Task<IActionResult> Run(Guid periodId, CancellationToken ct) => ApiResult(await service.RunAsync(periodId, ct));
    [HttpPost("ApproveDepreciation/{runId:guid}"), HasPermission("FixedAssets.Approve")]
    public async Task<IActionResult> Approve(Guid runId, CancellationToken ct) => ApiResult(await service.ApproveAsync(runId, ct));
}

[ApiController, Route("api/RecurringEntries")]
public sealed class RecurringEntriesController(IRecurringJournalService service) : AccountingControllerBase
{
    [HttpPost("Add"), HasPermission("RecurringEntries.Manage")]
    public async Task<IActionResult> Add(CreateRecurringJournalRequest request, CancellationToken ct) => ApiResult(await service.CreateAsync(request, ct));
    [HttpPost("GenerateDue"), HasPermission("RecurringEntries.Generate")]
    public async Task<IActionResult> Generate([FromQuery] DateOnly runDate, CancellationToken ct) => ApiResult(await service.GenerateDueAsync(runDate, ct));
}

[ApiController, Route("api/ClosingAssistant")]
public sealed class ClosingAssistantController(IClosingAssistantService service) : AccountingControllerBase
{
    [HttpPost("RunClosingChecks/{periodId:guid}"), HasPermission("ClosingAssistant.Run")]
    public async Task<IActionResult> Run(Guid periodId, CancellationToken ct) => ApiResult(await service.RunAsync(periodId, ct));
}

[ApiController, Route("api/Dashboard")]
public sealed class DashboardController(IDashboardService service) : AccountingControllerBase
{
    [HttpGet("GetMyDashboard"), HasPermission("Dashboard.View")]
    public async Task<IActionResult> Get(CancellationToken ct) => ApiResult(await service.GetMyDashboardAsync(ct));
}

[ApiController, Route("api/ReportBuilder")]
public sealed class ReportBuilderController(IReportBuilderService service) : AccountingControllerBase
{
    [HttpGet("GetDefinitions"), HasPermission("ReportBuilder.View")]
    public async Task<IActionResult> Get(CancellationToken ct) => ApiResult(await service.GetAsync(ct));
    [HttpPost("Add"), HasPermission("ReportBuilder.Manage")]
    public async Task<IActionResult> Add(SaveReportDefinitionRequest request, CancellationToken ct) => ApiResult(await service.SaveAsync(null, request, ct));
    [HttpPut("Update/{id:guid}"), HasPermission("ReportBuilder.Manage")]
    public async Task<IActionResult> Update(Guid id, SaveReportDefinitionRequest request, CancellationToken ct) => ApiResult(await service.SaveAsync(id, request, ct));
    [HttpDelete("Delete/{id:guid}"), HasPermission("ReportBuilder.Manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => ApiResult(await service.DeleteAsync(id, ct));
    [HttpPost("RunReport/{id:guid}"), HasPermission("ReportBuilder.Run")]
    public async Task<IActionResult> Run(Guid id, RunReportRequest request, CancellationToken ct) => ApiResult(await service.RunAsync(id, request, ct));
}

[ApiController, Route("api/BusinessParties")]
public sealed class BusinessPartiesController(IBusinessPartyService service) : AccountingControllerBase
{
    [HttpPost("{type}"), HasPermission("BusinessParties.Manage")]
    public async Task<IActionResult> Add(string type, SaveBusinessPartyRequest request, CancellationToken ct) => ApiResult(await service.SaveAsync(type, null, request, ct));
    [HttpPut("{type}/{id:guid}"), HasPermission("BusinessParties.Manage")]
    public async Task<IActionResult> Update(string type, Guid id, SaveBusinessPartyRequest request, CancellationToken ct) => ApiResult(await service.SaveAsync(type, id, request, ct));
    [HttpDelete("{type}/{id:guid}"), HasPermission("BusinessParties.Manage")]
    public async Task<IActionResult> Delete(string type, Guid id, CancellationToken ct) => ApiResult(await service.DeleteAsync(type, id, ct));
}
