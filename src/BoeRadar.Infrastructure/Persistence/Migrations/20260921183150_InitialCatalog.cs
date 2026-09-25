using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoeRadar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gazette_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    base_url = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gazette_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    trigger = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    counters = table.Column<string>(type: "jsonb", nullable: false),
                    error_summary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_ingestion_runs_gazette_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "gazette_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "publication_issues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issue_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    publication_date = table.Column<DateOnly>(type: "date", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: false),
                    raw_metadata = table.Column<string>(type: "jsonb", nullable: false),
                    discovered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publication_issues", x => x.id);
                    table.ForeignKey(
                        name: "FK_publication_issues_gazette_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "gazette_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "source_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    publication_date = table.Column<DateOnly>(type: "date", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    department_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    department = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    section_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    section_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    epigraph = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    control_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    official_html_url = table.Column<string>(type: "text", nullable: true),
                    official_xml_url = table.Column<string>(type: "text", nullable: true),
                    official_pdf_url = table.Column<string>(type: "text", nullable: true),
                    raw_metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_source_documents_gazette_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "gazette_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_source_documents_publication_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "publication_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_contents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    normalized_text = table.Column<string>(type: "text", nullable: false),
                    storage_uri = table.Column<string>(type: "text", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_contents", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_contents_source_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "source_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "gazette_sources",
                columns: new[] { "id", "base_url", "code", "created_at", "is_active", "name" },
                values: new object[] { new Guid("b0e00000-0000-7000-8000-000000000001"), "https://www.boe.es/", "BOE", new DateTimeOffset(new DateTime(2026, 9, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Boletín Oficial del Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_document_contents_document_id",
                table: "document_contents",
                column: "document_id",
                unique: true,
                filter: "is_current = true");

            migrationBuilder.CreateIndex(
                name: "IX_document_contents_document_id_content_hash",
                table: "document_contents",
                columns: new[] { "document_id", "content_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gazette_sources_code",
                table: "gazette_sources",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_runs_source_id_target_date",
                table: "ingestion_runs",
                columns: new[] { "source_id", "target_date" });

            migrationBuilder.CreateIndex(
                name: "IX_publication_issues_source_id_publication_date_external_id",
                table: "publication_issues",
                columns: new[] { "source_id", "publication_date", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_documents_department",
                table: "source_documents",
                column: "department");

            migrationBuilder.CreateIndex(
                name: "IX_source_documents_issue_id",
                table: "source_documents",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "IX_source_documents_publication_date",
                table: "source_documents",
                column: "publication_date");

            migrationBuilder.CreateIndex(
                name: "IX_source_documents_section_code",
                table: "source_documents",
                column: "section_code");

            migrationBuilder.CreateIndex(
                name: "IX_source_documents_source_id_external_id",
                table: "source_documents",
                columns: new[] { "source_id", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_contents");

            migrationBuilder.DropTable(
                name: "ingestion_runs");

            migrationBuilder.DropTable(
                name: "source_documents");

            migrationBuilder.DropTable(
                name: "publication_issues");

            migrationBuilder.DropTable(
                name: "gazette_sources");
        }
    }
}
