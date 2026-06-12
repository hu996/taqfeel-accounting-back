using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Enums;
using AccountingSaaS.Shared.Responses;

namespace AccountingSaaS.Application.Interfaces;

public interface ILookupService
{
    Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetLookupAsync(LookupType lookupType, LookupRequest request, CancellationToken cancellationToken = default);
    Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetTenantsAsync(LookupRequest request, CancellationToken cancellationToken = default);
    Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetUsersAsync(LookupRequest request, CancellationToken cancellationToken = default);
    Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetRolesAsync(LookupRequest request, CancellationToken cancellationToken = default);
    Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetPermissionsAsync(LookupRequest request, CancellationToken cancellationToken = default);
    Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetFinancialYearsAsync(LookupRequest request, CancellationToken cancellationToken = default);
    Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetAccountingPeriodsAsync(LookupRequest request, CancellationToken cancellationToken = default);
    Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetChartOfAccountsAsync(LookupRequest request, CancellationToken cancellationToken = default);
    Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetPostingAccountsAsync(LookupRequest request, CancellationToken cancellationToken = default);
    Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetParentAccountsAsync(LookupRequest request, CancellationToken cancellationToken = default);
    Task<BaseResponseDto<IReadOnlyList<LookupDto>>> GetCostCentersAsync(LookupRequest request, CancellationToken cancellationToken = default);
}
