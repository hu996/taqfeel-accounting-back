using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSaaS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowNumberingAndReviewerAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TenantNo",
                table: "Tenants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedReviewerUserId",
                table: "JournalEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "JournalEntryNo",
                table: "JournalEntries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "JournalEntries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowStatus",
                table: "JournalEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedReviewerUserId",
                table: "ImportBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImportBatchNo",
                table: "ImportBatches",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowStatus",
                table: "ImportBatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedReviewerUserId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DocumentNo",
                table: "Documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowStatus",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "CostCenterNo",
                table: "CostCenters",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedReviewerUserId",
                table: "ClosingSubmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UserNo",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "AccountNo",
                table: "Accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "NumberSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LastNumber = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewerTenantAssignments",
                columns: table => new
                {
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewerTenantAssignments", x => new { x.ReviewerUserId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_ReviewerTenantAssignments_AspNetUsers_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewerTenantAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                ;WITH n AS (SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAt, Id) AS No FROM Tenants)
                UPDATE t SET TenantNo = n.No FROM Tenants t INNER JOIN n ON n.Id = t.Id;

                ;WITH n AS (SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAt, Id) AS No FROM AspNetUsers)
                UPDATE u SET UserNo = n.No FROM AspNetUsers u INNER JOIN n ON n.Id = u.Id;

                ;WITH n AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CreatedAt, Id) AS No FROM Accounts)
                UPDATE a SET AccountNo = n.No FROM Accounts a INNER JOIN n ON n.Id = a.Id;

                ;WITH n AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CreatedAt, Id) AS No FROM CostCenters)
                UPDATE c SET CostCenterNo = n.No FROM CostCenters c INNER JOIN n ON n.Id = c.Id;

                ;WITH n AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY EntryDate, CreatedAt, Id) AS No FROM JournalEntries)
                UPDATE j
                SET JournalEntryNo = n.No,
                    WorkflowStatus = CASE WHEN j.Status = 2 THEN 4 WHEN j.Status = 4 THEN 8 ELSE 1 END
                FROM JournalEntries j INNER JOIN n ON n.Id = j.Id;

                ;WITH n AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY UploadedAt, Id) AS No FROM Documents)
                UPDATE d SET DocumentNo = n.No, WorkflowStatus = 1
                FROM Documents d INNER JOIN n ON n.Id = d.Id;

                ;WITH n AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY UploadedAt, Id) AS No FROM ImportBatches)
                UPDATE b
                SET ImportBatchNo = n.No,
                    WorkflowStatus = CASE WHEN b.Status = 6 THEN 7 WHEN b.Status = 8 THEN 8 ELSE 1 END
                FROM ImportBatches b INNER JOIN n ON n.Id = b.Id;

                INSERT INTO ReviewerTenantAssignments
                    (ReviewerUserId, TenantId, IsActive, CreatedAt, CreatedByUserId)
                SELECT DISTINCT ur.UserId, uta.TenantId, 1, SYSUTCDATETIME(), NULL
                FROM AspNetUserRoles ur
                INNER JOIN AspNetRoles r ON r.Id = ur.RoleId AND r.Name = 'Reviewer'
                INNER JOIN UserTenantAccesses uta ON uta.UserId = ur.UserId;

                INSERT INTO NumberSequences (Id, TenantId, SequenceKey, LastNumber, UpdatedAt)
                SELECT NEWID(), '00000000-0000-0000-0000-000000000000', 'TenantNo', COALESCE(MAX(TenantNo), 0), SYSUTCDATETIME()
                FROM Tenants;
                INSERT INTO NumberSequences (Id, TenantId, SequenceKey, LastNumber, UpdatedAt)
                SELECT NEWID(), '00000000-0000-0000-0000-000000000000', 'UserNo', COALESCE(MAX(UserNo), 0), SYSUTCDATETIME()
                FROM AspNetUsers;
                INSERT INTO NumberSequences (Id, TenantId, SequenceKey, LastNumber, UpdatedAt)
                SELECT NEWID(), TenantId, 'AccountNo', MAX(AccountNo), SYSUTCDATETIME() FROM Accounts GROUP BY TenantId;
                INSERT INTO NumberSequences (Id, TenantId, SequenceKey, LastNumber, UpdatedAt)
                SELECT NEWID(), TenantId, 'CostCenterNo', MAX(CostCenterNo), SYSUTCDATETIME() FROM CostCenters GROUP BY TenantId;
                INSERT INTO NumberSequences (Id, TenantId, SequenceKey, LastNumber, UpdatedAt)
                SELECT NEWID(), TenantId, 'JournalEntryNo', MAX(JournalEntryNo), SYSUTCDATETIME() FROM JournalEntries GROUP BY TenantId;
                INSERT INTO NumberSequences (Id, TenantId, SequenceKey, LastNumber, UpdatedAt)
                SELECT NEWID(), TenantId, 'DocumentNo', MAX(DocumentNo), SYSUTCDATETIME() FROM Documents GROUP BY TenantId;
                INSERT INTO NumberSequences (Id, TenantId, SequenceKey, LastNumber, UpdatedAt)
                SELECT NEWID(), TenantId, 'ImportBatchNo', MAX(ImportBatchNo), SYSUTCDATETIME() FROM ImportBatches GROUP BY TenantId;
                INSERT INTO NumberSequences (Id, TenantId, SequenceKey, LastNumber, UpdatedAt)
                SELECT NEWID(), TenantId,
                       CONCAT('AccountCode:', AccountType, ':', COALESCE(CONVERT(nvarchar(36), ParentAccountId), 'ROOT')),
                       COUNT_BIG(*), SYSUTCDATETIME()
                FROM Accounts GROUP BY TenantId, AccountType, ParentAccountId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantNo",
                table: "Tenants",
                column: "TenantNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_JournalEntryNo",
                table: "JournalEntries",
                columns: new[] { "TenantId", "JournalEntryNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_WorkflowStatus_AssignedReviewerUserId",
                table: "JournalEntries",
                columns: new[] { "TenantId", "WorkflowStatus", "AssignedReviewerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_TenantId_ImportBatchNo",
                table: "ImportBatches",
                columns: new[] { "TenantId", "ImportBatchNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_TenantId_WorkflowStatus_AssignedReviewerUserId",
                table: "ImportBatches",
                columns: new[] { "TenantId", "WorkflowStatus", "AssignedReviewerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TenantId_DocumentNo",
                table: "Documents",
                columns: new[] { "TenantId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TenantId_WorkflowStatus_AssignedReviewerUserId",
                table: "Documents",
                columns: new[] { "TenantId", "WorkflowStatus", "AssignedReviewerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostCenters_TenantId_CostCenterNo",
                table: "CostCenters",
                columns: new[] { "TenantId", "CostCenterNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClosingSubmissions_TenantId_Status_AssignedReviewerUserId",
                table: "ClosingSubmissions",
                columns: new[] { "TenantId", "Status", "AssignedReviewerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UserNo",
                table: "AspNetUsers",
                column: "UserNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TenantId_AccountNo",
                table: "Accounts",
                columns: new[] { "TenantId", "AccountNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_TenantId_SequenceKey",
                table: "NumberSequences",
                columns: new[] { "TenantId", "SequenceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewerTenantAssignments_TenantId_IsActive",
                table: "ReviewerTenantAssignments",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NumberSequences");

            migrationBuilder.DropTable(
                name: "ReviewerTenantAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_TenantNo",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TenantId_JournalEntryNo",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TenantId_WorkflowStatus_AssignedReviewerUserId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_TenantId_ImportBatchNo",
                table: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_TenantId_WorkflowStatus_AssignedReviewerUserId",
                table: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_Documents_TenantId_DocumentNo",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_TenantId_WorkflowStatus_AssignedReviewerUserId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_CostCenters_TenantId_CostCenterNo",
                table: "CostCenters");

            migrationBuilder.DropIndex(
                name: "IX_ClosingSubmissions_TenantId_Status_AssignedReviewerUserId",
                table: "ClosingSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_UserNo",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_TenantId_AccountNo",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TenantNo",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AssignedReviewerUserId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "JournalEntryNo",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "WorkflowStatus",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "AssignedReviewerUserId",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "ImportBatchNo",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "WorkflowStatus",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "AssignedReviewerUserId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DocumentNo",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "WorkflowStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CostCenterNo",
                table: "CostCenters");

            migrationBuilder.DropColumn(
                name: "AssignedReviewerUserId",
                table: "ClosingSubmissions");

            migrationBuilder.DropColumn(
                name: "UserNo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AccountNo",
                table: "Accounts");
        }
    }
}
