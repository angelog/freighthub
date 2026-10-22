using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialRuleEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shipping_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rule_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    range_start = table.Column<DateOnly>(type: "date", nullable: true),
                    range_end = table.Column<DateOnly>(type: "date", nullable: true),
                    range_is_free = table.Column<bool>(type: "boolean", nullable: true),
                    range_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    free_above_min_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    min_fixed_threshold = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    min_fixed_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    specific_date = table.Column<DateOnly>(type: "date", nullable: true),
                    specific_is_free = table.Column<bool>(type: "boolean", nullable: true),
                    specific_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    weekday_day = table.Column<int>(type: "integer", nullable: true),
                    weekday_is_free = table.Column<bool>(type: "boolean", nullable: true),
                    weekday_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipping_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cep_ranges",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cep_start = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    cep_end = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    shipping_rule_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cep_ranges", x => x.id);
                    table.ForeignKey(
                        name: "FK_cep_ranges_shipping_rules_shipping_rule_id",
                        column: x => x.shipping_rule_id,
                        principalTable: "shipping_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cep_ranges_shipping_rule_id",
                table: "cep_ranges",
                column: "shipping_rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipping_rules_IsActive_Priority",
                table: "shipping_rules",
                columns: new[] { "IsActive", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cep_ranges");

            migrationBuilder.DropTable(
                name: "shipping_rules");
        }
    }
}
