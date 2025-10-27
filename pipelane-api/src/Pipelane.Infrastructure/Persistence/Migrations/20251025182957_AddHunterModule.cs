using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pipelane.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHunterModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Company",
                table: "Prospects",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Prospects",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Prospects",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Prospects",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FailedWebhooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedWebhooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProspectLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProspectScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    FeaturesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProspectScores_Prospects_ProspectId",
                        column: x => x.ProspectId,
                        principalTable: "Prospects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RateLimitSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HitsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateLimitSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProspectListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProspectListItems_ProspectLists_ProspectListId",
                        column: x => x.ProspectListId,
                        principalTable: "ProspectLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProspectListItems_Prospects_ProspectId",
                        column: x => x.ProspectId,
                        principalTable: "Prospects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_Tenant_City",
                table: "Prospects",
                columns: new[] { "TenantId", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_Tenant_Company",
                table: "Prospects",
                columns: new[] { "TenantId", "Company" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedWebhooks_Tenant_NextAttempt",
                table: "FailedWebhooks",
                columns: new[] { "TenantId", "NextAttemptUtc", "RetryCount" });

            migrationBuilder.CreateIndex(
                name: "IX_ProspectListItems_List_Prospect",
                table: "ProspectListItems",
                columns: new[] { "TenantId", "ProspectListId", "ProspectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProspectListItems_ProspectId",
                table: "ProspectListItems",
                column: "ProspectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectListItems_ProspectListId",
                table: "ProspectListItems",
                column: "ProspectListId");

            migrationBuilder.CreateIndex(
                name: "IX_ProspectLists_Tenant_Name",
                table: "ProspectLists",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProspectScores_ProspectId",
                table: "ProspectScores",
                column: "ProspectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProspectScores_Tenant_Prospect",
                table: "ProspectScores",
                columns: new[] { "TenantId", "ProspectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RateLimitSnapshots_Target_Scope",
                table: "RateLimitSnapshots",
                columns: new[] { "TargetTenantId", "Scope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailedWebhooks");

            migrationBuilder.DropTable(
                name: "ProspectListItems");

            migrationBuilder.DropTable(
                name: "ProspectScores");

            migrationBuilder.DropTable(
                name: "RateLimitSnapshots");

            migrationBuilder.DropTable(
                name: "ProspectLists");

            migrationBuilder.DropIndex(
                name: "IX_Prospects_Tenant_City",
                table: "Prospects");

            migrationBuilder.DropIndex(
                name: "IX_Prospects_Tenant_Company",
                table: "Prospects");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Prospects");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Prospects");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Prospects");

            migrationBuilder.AlterColumn<string>(
                name: "Company",
                table: "Prospects",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
