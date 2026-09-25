using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoeRadar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionsAndAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    verification_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    verification_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    management_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    categories = table.Column<string>(type: "jsonb", nullable: false),
                    keywords = table.Column<string>(type: "jsonb", nullable: false),
                    timezone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    digest_hour = table.Column<int>(type: "integer", nullable: false),
                    consented_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    unsubscribed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alert_digests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    digest_date = table.Column<DateOnly>(type: "date", nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    unsubscribe_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_digests", x => x.id);
                    table.ForeignKey(
                        name: "FK_alert_digests_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alert_matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    digest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reasons = table.Column<string>(type: "jsonb", nullable: false),
                    matched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_matches", x => x.id);
                    table.ForeignKey(
                        name: "FK_alert_matches_alert_digests_digest_id",
                        column: x => x.digest_id,
                        principalTable: "alert_digests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alert_matches_document_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalTable: "document_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alert_matches_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recipient = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    digest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_outbox_messages_alert_digests_digest_id",
                        column: x => x.digest_id,
                        principalTable: "alert_digests",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_digests_subscription_id_digest_date",
                table: "alert_digests",
                columns: new[] { "subscription_id", "digest_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_digests_unsubscribe_token_hash",
                table: "alert_digests",
                column: "unsubscribe_token_hash",
                unique: true,
                filter: "unsubscribe_token_hash <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_alert_matches_analysis_id",
                table: "alert_matches",
                column: "analysis_id");

            migrationBuilder.CreateIndex(
                name: "IX_alert_matches_digest_id",
                table: "alert_matches",
                column: "digest_id");

            migrationBuilder.CreateIndex(
                name: "IX_alert_matches_subscription_id_analysis_id",
                table: "alert_matches",
                columns: new[] { "subscription_id", "analysis_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_digest_id",
                table: "outbox_messages",
                column: "digest_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_idempotency_key",
                table: "outbox_messages",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_status_next_attempt_at",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_email",
                table: "subscriptions",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_management_token_hash",
                table: "subscriptions",
                column: "management_token_hash",
                unique: true,
                filter: "management_token_hash <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_verification_token_hash",
                table: "subscriptions",
                column: "verification_token_hash",
                unique: true,
                filter: "verification_token_hash <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_matches");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "alert_digests");

            migrationBuilder.DropTable(
                name: "subscriptions");
        }
    }
}
