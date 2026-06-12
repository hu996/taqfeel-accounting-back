using AccountingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingSaaS.Infrastructure.Persistence;

public sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.StoredFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ErrorSummary).HasMaxLength(2000);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.ImportType });
        builder.HasIndex(x => new { x.TenantId, x.FinancialYearId });
        builder.HasIndex(x => new { x.TenantId, x.AccountingPeriodId });
        builder.HasIndex(x => new { x.TenantId, x.UploadedAt });
        builder.HasMany(x => x.Rows).WithOne(x => x.ImportBatch).HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ImportBatchRowConfiguration : IEntityTypeConfiguration<ImportBatchRow>
{
    public void Configure(EntityTypeBuilder<ImportBatchRow> builder)
    {
        builder.Property(x => x.RawJson).IsRequired();
        builder.Property(x => x.ErrorMessages).HasMaxLength(4000);
        builder.Property(x => x.WarningMessages).HasMaxLength(4000);
        builder.Property(x => x.ImportedEntityName).HasMaxLength(120);
        builder.Property(x => x.ImportedEntityId).HasMaxLength(80);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.ImportBatchId });
        builder.HasIndex(x => new { x.TenantId, x.ImportBatchId, x.Status });
    }
}
