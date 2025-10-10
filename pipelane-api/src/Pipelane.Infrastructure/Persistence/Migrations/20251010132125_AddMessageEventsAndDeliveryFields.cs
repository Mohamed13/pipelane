using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pipelane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageEventsAndDeliveryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_TenantId_ProviderMessageId_Channel",
                table: "Messages");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorReason",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAt",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MessageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProviderEventId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Raw = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageEvents_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId_ProviderMessageId",
                table: "Messages",
                columns: new[] { "TenantId", "ProviderMessageId" },
                unique: true,
                filter: "[ProviderMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEvents_MessageId",
                table: "MessageEvents",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEvents_Tenant_Message_Created",
                table: "MessageEvents",
                columns: new[] { "TenantId", "MessageId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageEvents_Provider_EventId",
                table: "MessageEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true,
                filter: "[ProviderEventId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageEvents");

            migrationBuilder.DropIndex(
                name: "IX_Messages_TenantId_ProviderMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ErrorReason",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "OpenedAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId_ProviderMessageId_Channel",
                table: "Messages",
                columns: new[] { "TenantId", "ProviderMessageId", "Channel" });
        }
    }
}
