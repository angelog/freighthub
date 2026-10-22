using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShippingQuoteLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shipping_quote_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DestinationZipCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CartAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RequestBody = table.Column<string>(type: "jsonb", nullable: false),
                    ResponseBody = table.Column<string>(type: "jsonb", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    RuleDecided = table.Column<bool>(type: "boolean", nullable: false),
                    CarrierQuoteCount = table.Column<int>(type: "integer", nullable: false),
                    UsedFallback = table.Column<bool>(type: "boolean", nullable: false),
                    SignatureValid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipping_quote_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipping_quote_logs_CreatedAt",
                table: "shipping_quote_logs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipping_quote_logs");
        }
    }
}
