using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Domain.Enums;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AccountingSaaS.Api.Swagger;

public sealed class SwaggerExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        schema.Example = context.Type.Name switch
        {
            nameof(LoginRequest) => Obj(("email", Str("accountant@accountingsaas.local")), ("password", Str("DevPass123!DevPass123!"))),
            nameof(RefreshTokenRequest) => Obj(("refreshToken", Str("refresh-token-value"))),
            nameof(LogoutRequest) => Obj(("refreshToken", Str("refresh-token-value"))),
            nameof(CreateFinancialYearRequest) => Obj(("yearName", Str("FY-2026")), ("startDate", Str("2026-01-01")), ("endDate", Str("2026-12-31"))),
            nameof(CreateAccountingPeriodRequest) => Obj(("financialYearId", GuidValue()), ("periodName", Str("2026-01")), ("startDate", Str("2026-01-01")), ("endDate", Str("2026-01-31"))),
            nameof(CreateAccountRequest) => Obj(("code", Str("1200")), ("nameAr", Str("بنك تجريبي")), ("nameEn", Str("Development Bank")), ("accountType", Int((int)AccountType.Asset)), ("normalBalance", Int((int)NormalBalance.Debit)), ("parentAccountId", Null()), ("isPostingAccount", Bool(true))),
            nameof(CreateCostCenterRequest) => Obj(("code", Str("MAIN")), ("name", Str("مركز تكلفة رئيسي تجريبي"))),
            nameof(CreateJournalEntryRequest) => Obj(("entryDate", Str("2026-01-10")), ("description", Str("قيد يومية تجريبي موزون")), ("lines", Arr(
                Obj(("accountId", GuidValue()), ("costCenterId", GuidValue()), ("debit", Dec(1500)), ("credit", Dec(0)), ("description", Str("مدين"))),
                Obj(("accountId", GuidValue()), ("costCenterId", GuidValue()), ("debit", Dec(0)), ("credit", Dec(1500)), ("description", Str("دائن")))))),
            nameof(JournalEntryDto) => Obj(("id", GuidValue()), ("entryNumber", Str("JE-000001")), ("entryDate", Str("2026-01-10")), ("description", Str("قيد يومية تجريبي")), ("status", Int((int)JournalEntryStatus.Posted)), ("totalDebit", Dec(1500)), ("totalCredit", Dec(1500)), ("lines", Arr())),
            nameof(UploadImportRequest) => Obj(("importType", Int((int)ImportType.JournalEntries)), ("financialYearId", GuidValue()), ("accountingPeriodId", GuidValue()), ("worksheetName", Str("Sheet1")), ("notes", Str("ملف استيراد تجريبي"))),
            nameof(ImportPreviewDto) => Obj(("batchId", GuidValue()), ("importType", Int((int)ImportType.JournalEntries)), ("status", Int((int)ImportBatchStatus.ReadyToImport)), ("totalRows", Int(2)), ("validRows", Int(2)), ("invalidRows", Int(0)), ("warningRows", Int(0)), ("rows", Arr())),
            nameof(TrialBalanceRowDto) => Obj(("accountCode", Str("1200")), ("accountName", Str("Development Bank")), ("openingDebit", Dec(100000)), ("openingCredit", Dec(0)), ("periodDebit", Dec(0)), ("periodCredit", Dec(3000)), ("closingDebit", Dec(97000)), ("closingCredit", Dec(0))),
            nameof(LookupDto) => Obj(("id", GuidValue()), ("label", Str("بنك تجريبي")), ("labelAr", Str("بنك تجريبي")), ("labelEn", Str("Development Bank")), ("code", Str("1200")), ("extra", Str("Asset")), ("isActive", Bool(true))),
            _ => schema.Example
        };
    }

    private static OpenApiString Str(string value) => new(value);
    private static OpenApiInteger Int(int value) => new(value);
    private static OpenApiDouble Dec(double value) => new(value);
    private static OpenApiBoolean Bool(bool value) => new(value);
    private static OpenApiNull Null() => new();
    private static OpenApiString GuidValue() => new("11111111-1111-1111-1111-111111111111");
    private static OpenApiArray Arr(params IOpenApiAny[] values)
    {
        var array = new OpenApiArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static OpenApiObject Obj(params (string Key, IOpenApiAny Value)[] values)
    {
        var obj = new OpenApiObject();
        foreach (var (key, value) in values)
        {
            obj[key] = value;
        }

        return obj;
    }
}
