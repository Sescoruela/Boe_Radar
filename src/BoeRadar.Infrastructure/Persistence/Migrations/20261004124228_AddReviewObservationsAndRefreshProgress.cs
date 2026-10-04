using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BoeRadar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewObservationsAndRefreshProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_refresh_progress",
                columns: table => new
                {
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    next_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_refresh_progress", x => x.source);
                });

            migrationBuilder.CreateTable(
                name: "source_review_observations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_review_observations", x => x.id);
                    table.ForeignKey(
                        name: "FK_source_review_observations_source_reviews_external_id_sourc~",
                        columns: x => new { x.external_id, x.source_hash, x.version },
                        principalTable: "source_reviews",
                        principalColumns: new[] { "external_id", "source_hash", "version" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_source_review_observations_external_id_observed_at_id",
                table: "source_review_observations",
                columns: new[] { "external_id", "observed_at", "id" });

            // Retain the historical first observation of versions saved before this migration.
            migrationBuilder.Sql("""
                INSERT INTO source_review_observations (external_id, source_hash, version, observed_at)
                SELECT external_id, source_hash, version, recorded_at FROM source_reviews
                ORDER BY recorded_at, external_id, source_hash
                """);

            migrationBuilder.CreateIndex(
                name: "IX_source_review_observations_external_id_source_hash_version",
                table: "source_review_observations",
                columns: new[] { "external_id", "source_hash", "version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_refresh_progress");

            migrationBuilder.DropTable(
                name: "source_review_observations");
        }
    }
}
