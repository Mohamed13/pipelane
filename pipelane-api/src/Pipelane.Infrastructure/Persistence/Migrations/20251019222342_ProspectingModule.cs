using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pipelane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProspectingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeadScores_TenantId_ContactId",
                table: "LeadScores");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContactId",
                table: "LeadScores",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "LeadScores",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProspectId",
                table: "LeadScores",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "LeadScores",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ProspectingSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TargetPersona = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectingSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProspectingCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SequenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SegmentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PausedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectingCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProspectingCampaigns_ProspectingSequences_SequenceId",
                        column: x => x.SequenceId,
                        principalTable: "ProspectingSequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProspectingSequenceSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    StepType = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    OffsetDays = table.Column<int>(type: "int", nullable: false),
                    SendWindowStartUtc = table.Column<TimeSpan>(type: "time", nullable: true),
                    SendWindowEndUtc = table.Column<TimeSpan>(type: "time", nullable: true),
                    PromptTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubjectTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GuardrailInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectingSequenceSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProspectingSequenceSteps_ProspectingSequences_SequenceId",
                        column: x => x.SequenceId,
                        principalTable: "ProspectingSequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prospects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Company = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OptedOut = table.Column<bool>(type: "bit", nullable: false),
                    OptedOutAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SequenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastContactedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRepliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Persona = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnrichedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prospects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prospects_ProspectingCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "ProspectingCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Prospects_ProspectingSequences_SequenceId",
                        column: x => x.SequenceId,
                        principalTable: "ProspectingSequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EmailGenerations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Variant = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PromptUsed = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Temperature = table.Column<decimal>(type: "decimal(4,3)", nullable: true),
                    PromptTokens = table.Column<int>(type: "int", nullable: true),
                    CompletionTokens = table.Column<int>(type: "int", nullable: true),
                    CostUsd = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Approved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailGenerations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailGenerations_ProspectingCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "ProspectingCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EmailGenerations_ProspectingSequenceSteps_StepId",
                        column: x => x.StepId,
                        principalTable: "ProspectingSequenceSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailGenerations_Prospects_ProspectId",
                        column: x => x.ProspectId,
                        principalTable: "Prospects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProspectingSendLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GenerationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClickedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BouncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ComplainedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeferredUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectingSendLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProspectingSendLogs_EmailGenerations_GenerationId",
                        column: x => x.GenerationId,
                        principalTable: "EmailGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProspectingSendLogs_ProspectingCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "ProspectingCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProspectingSendLogs_ProspectingSequenceSteps_StepId",
                        column: x => x.StepId,
                        principalTable: "ProspectingSequenceSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProspectingSendLogs_Prospects_ProspectId",
                        column: x => x.ProspectId,
                        principalTable: "Prospects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProspectReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SendLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TextBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Intent = table.Column<int>(type: "int", nullable: false),
                    IntentConfidence = table.Column<double>(type: "float", nullable: true),
                    ExtractedDatesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AutoReplySuggested = table.Column<bool>(type: "bit", nullable: false),
                    AutoReplyGenerationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutoReplySendLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProspectReplies_EmailGenerations_AutoReplyGenerationId",
                        column: x => x.AutoReplyGenerationId,
                        principalTable: "EmailGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProspectReplies_ProspectingCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "ProspectingCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProspectReplies_ProspectingSendLogs_SendLogId",
                        column: x => x.SendLogId,
                        principalTable: "ProspectingSendLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProspectReplies_ProspectingSequenceSteps_StepId",
                        column: x => x.StepId,
                        principalTable: "ProspectingSequenceSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProspectReplies_Prospects_ProspectId",
                        column: x => x.ProspectId,
                        principalTable: "Prospects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadScores_Tenant_Contact",
                table: "LeadScores",
                columns: new[] { "TenantId", "ContactId" },
                unique: true,
                filter: "[ContactId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeadScores_Tenant_Prospect",
                table: "LeadScores",
                columns: new[] { "TenantId", "ProspectId" },
                unique: true,
                filter: "[ProspectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailGenerations_CampaignId",
                table: "EmailGenerations",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailGenerations_Prospect_Step_Created",
                table: "EmailGenerations",
                columns: new[] { "TenantId", "ProspectId", "StepId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailGenerations_ProspectId",
                table: "EmailGenerations",
                column: "ProspectId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailGenerations_StepId",
                table: "EmailGenerations",
                column: "StepId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectingCampaigns_SequenceId",
                table: "ProspectingCampaigns",
                column: "SequenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectingCampaigns_Tenant_Status",
                table: "ProspectingCampaigns",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProspectingSendLogs_CampaignId",
                table: "ProspectingSendLogs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectingSendLogs_GenerationId",
                table: "ProspectingSendLogs",
                column: "GenerationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectingSendLogs_ProspectId",
                table: "ProspectingSendLogs",
                column: "ProspectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectingSendLogs_StepId",
                table: "ProspectingSendLogs",
                column: "StepId");

            migrationBuilder.CreateIndex(
                name: "IX_SendLogs_ProviderMessage",
                table: "ProspectingSendLogs",
                columns: new[] { "TenantId", "Provider", "ProviderMessageId" },
                unique: true,
                filter: "[ProviderMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SendLogs_Tenant_Prospect_Status_Scheduled",
                table: "ProspectingSendLogs",
                columns: new[] { "TenantId", "ProspectId", "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProspectingSequences_Tenant_Name",
                table: "ProspectingSequences",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProspectingSequenceSteps_SequenceId",
                table: "ProspectingSequenceSteps",
                column: "SequenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectingSteps_Sequence_Order",
                table: "ProspectingSequenceSteps",
                columns: new[] { "TenantId", "SequenceId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProspectReplies_AutoReplyGenerationId",
                table: "ProspectReplies",
                column: "AutoReplyGenerationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectReplies_CampaignId",
                table: "ProspectReplies",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectReplies_ProspectId",
                table: "ProspectReplies",
                column: "ProspectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectReplies_SendLogId",
                table: "ProspectReplies",
                column: "SendLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectReplies_StepId",
                table: "ProspectReplies",
                column: "StepId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectReplies_Tenant_Prospect_Received",
                table: "ProspectReplies",
                columns: new[] { "TenantId", "ProspectId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_CampaignId",
                table: "Prospects",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_SequenceId",
                table: "Prospects",
                column: "SequenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_Tenant_Email",
                table: "Prospects",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_Tenant_Owner",
                table: "Prospects",
                columns: new[] { "TenantId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_Tenant_Status",
                table: "Prospects",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProspectReplies");

            migrationBuilder.DropTable(
                name: "ProspectingSendLogs");

            migrationBuilder.DropTable(
                name: "EmailGenerations");

            migrationBuilder.DropTable(
                name: "ProspectingSequenceSteps");

            migrationBuilder.DropTable(
                name: "Prospects");

            migrationBuilder.DropTable(
                name: "ProspectingCampaigns");

            migrationBuilder.DropTable(
                name: "ProspectingSequences");

            migrationBuilder.DropIndex(
                name: "IX_LeadScores_Tenant_Contact",
                table: "LeadScores");

            migrationBuilder.DropIndex(
                name: "IX_LeadScores_Tenant_Prospect",
                table: "LeadScores");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "LeadScores");

            migrationBuilder.DropColumn(
                name: "ProspectId",
                table: "LeadScores");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "LeadScores");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContactId",
                table: "LeadScores",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadScores_TenantId_ContactId",
                table: "LeadScores",
                columns: new[] { "TenantId", "ContactId" },
                unique: true);
        }
    }
}
