using System.Data;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class NumberSequenceService(AppDbContext dbContext) : INumberSequenceService
{
    public async Task<long> NextAsync(
        string sequenceKey,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var scopeTenantId = tenantId ?? Guid.Empty;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var sequence = await dbContext.NumberSequences
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.TenantId == scopeTenantId && x.SequenceKey == sequenceKey,
                cancellationToken);

        if (sequence is null)
        {
            sequence = new NumberSequence
            {
                Id = Guid.NewGuid(),
                TenantId = scopeTenantId,
                SequenceKey = sequenceKey,
                LastNumber = 1,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.NumberSequences.Add(sequence);
        }
        else
        {
            sequence.LastNumber++;
            sequence.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return sequence.LastNumber;
    }
}
