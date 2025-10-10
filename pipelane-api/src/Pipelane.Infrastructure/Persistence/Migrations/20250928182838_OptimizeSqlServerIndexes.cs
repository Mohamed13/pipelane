using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pipelane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeSqlServerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_consents_contact_channel",
                table: "Consents");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderMessageId",
                table: "Messages",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderThreadId",
                table: "Conversations",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Contacts",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Status_ScheduledAtUtc_LockedUntilUtc_CreatedAt",
                table: "Outbox",
                columns: new[] { "Status", "ScheduledAtUtc", "LockedUntilUtc", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId_Channel_CreatedAt",
                table: "Messages",
                columns: new[] { "TenantId", "Channel", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId_ProviderMessageId_Channel",
                table: "Messages",
                columns: new[] { "TenantId", "ProviderMessageId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadScores_TenantId_ContactId",
                table: "LeadScores",
                columns: new[] { "TenantId", "ContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversions_TenantId_CampaignId_RevenueAtUtc",
                table: "Conversions",
                columns: new[] { "TenantId", "CampaignId", "RevenueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversions_TenantId_ContactId_RevenueAtUtc",
                table: "Conversions",
                columns: new[] { "TenantId", "ContactId", "RevenueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_ContactId",
                table: "Conversations",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_ProviderThreadId",
                table: "Conversations",
                columns: new[] { "TenantId", "ProviderThreadId" });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_TenantId_Email",
                table: "Contacts",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_consents_contact_channel",
                table: "Consents",
                columns: new[] { "ContactId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelSettings_TenantId_Channel",
                table: "ChannelSettings",
                columns: new[] { "TenantId", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Outbox_Status_ScheduledAtUtc_LockedUntilUtc_CreatedAt",
                table: "Outbox");

            migrationBuilder.DropIndex(
                name: "IX_Messages_TenantId_Channel_CreatedAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_TenantId_ProviderMessageId_Channel",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_LeadScores_TenantId_ContactId",
                table: "LeadScores");

            migrationBuilder.DropIndex(
                name: "IX_Conversions_TenantId_CampaignId_RevenueAtUtc",
                table: "Conversions");

            migrationBuilder.DropIndex(
                name: "IX_Conversions_TenantId_ContactId_RevenueAtUtc",
                table: "Conversions");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_TenantId_ContactId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_TenantId_ProviderThreadId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_TenantId_Email",
                table: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_consents_contact_channel",
                table: "Consents");

            migrationBuilder.DropIndex(
                name: "IX_ChannelSettings_TenantId_Channel",
                table: "ChannelSettings");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderMessageId",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderThreadId",
                table: "Conversations",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Contacts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_consents_contact_channel",
                table: "Consents",
                columns: new[] { "ContactId", "Channel" });
        }
    }
}
