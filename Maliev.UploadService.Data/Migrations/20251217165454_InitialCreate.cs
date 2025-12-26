using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.UploadService.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bulk_delete_jobs",
                columns: table => new
                {
                    job_id = table.Column<string>(type: "text", nullable: false),
                    service_id = table.Column<string>(type: "text", nullable: false),
                    path_prefix = table.Column<string>(type: "text", nullable: false),
                    initiated_by = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    files_total = table.Column<int>(type: "integer", nullable: false),
                    files_processed = table.Column<int>(type: "integer", nullable: false),
                    files_deleted = table.Column<int>(type: "integer", nullable: false),
                    error_count = table.Column<int>(type: "integer", nullable: false),
                    upload_ids = table.Column<List<string>>(type: "text[]", nullable: true),
                    delete_files_older_than = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_details = table.Column<string>(type: "jsonb", nullable: true),
                    total_files = table.Column<int>(type: "integer", nullable: false),
                    files_failed = table.Column<int>(type: "integer", nullable: false),
                    errors = table.Column<List<string>>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bulk_delete_jobs", x => x.job_id);
                });

            migrationBuilder.CreateTable(
                name: "retention_policies",
                columns: table => new
                {
                    policy_id = table.Column<string>(type: "text", nullable: false),
                    policy_name = table.Column<string>(type: "text", nullable: false),
                    service_id = table.Column<string>(type: "text", nullable: true),
                    retention_days = table.Column<int>(type: "integer", nullable: false),
                    storage_class_transitions = table.Column<string>(type: "jsonb", nullable: true),
                    apply_to_path_prefix = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retention_policies", x => x.policy_id);
                });

            migrationBuilder.CreateTable(
                name: "service_authorization_policies",
                columns: table => new
                {
                    policy_id = table.Column<string>(type: "text", nullable: false),
                    service_id = table.Column<string>(type: "text", nullable: false),
                    service_name = table.Column<string>(type: "text", nullable: false),
                    allowed_path_prefixes = table.Column<string>(type: "jsonb", nullable: false),
                    allowed_content_types = table.Column<string>(type: "jsonb", nullable: false),
                    max_file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_quota_bytes = table.Column<long>(type: "bigint", nullable: false),
                    allow_overwrite = table.Column<bool>(type: "boolean", nullable: false),
                    allow_resumable_upload = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_authorization_policies", x => x.policy_id);
                });

            migrationBuilder.CreateTable(
                name: "upload_events",
                columns: table => new
                {
                    event_id = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    service_id = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: true),
                    upload_id = table.Column<string>(type: "text", nullable: true),
                    file_id = table.Column<string>(type: "text", nullable: true),
                    storage_path = table.Column<string>(type: "text", nullable: true),
                    event_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_result = table.Column<string>(type: "text", nullable: false),
                    error_details = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_upload_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "uploads",
                columns: table => new
                {
                    upload_id = table.Column<string>(type: "text", nullable: false),
                    service_id = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: true),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    checksum = table.Column<string>(type: "text", nullable: true),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    session_uri = table.Column<string>(type: "text", nullable: true),
                    bytes_uploaded = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    retention_policy_id = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_uploads", x => x.upload_id);
                });

            migrationBuilder.CreateTable(
                name: "file_metadata",
                columns: table => new
                {
                    file_id = table.Column<string>(type: "text", nullable: false),
                    upload_id = table.Column<string>(type: "text", nullable: false),
                    service_id = table.Column<string>(type: "text", nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    version_e_tag = table.Column<string>(type: "text", nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    checksum = table.Column<string>(type: "text", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_accessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    retention_policy_id = table.Column<string>(type: "text", nullable: true),
                    storage_class = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_metadata", x => x.file_id);
                    table.ForeignKey(
                        name: "fk_file_metadata_retention_policies_retention_policy_id",
                        column: x => x.retention_policy_id,
                        principalTable: "retention_policies",
                        principalColumn: "policy_id");
                    table.ForeignKey(
                        name: "fk_file_metadata_uploads_upload_id",
                        column: x => x.upload_id,
                        principalTable: "uploads",
                        principalColumn: "upload_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_bulk_delete_created_at",
                table: "bulk_delete_jobs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_bulk_delete_service_id",
                table: "bulk_delete_jobs",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "idx_bulk_delete_status",
                table: "bulk_delete_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_filemetadata_expires_at",
                table: "file_metadata",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_filemetadata_service_id",
                table: "file_metadata",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "idx_filemetadata_storage_path",
                table: "file_metadata",
                column: "storage_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_filemetadata_uploaded_at",
                table: "file_metadata",
                column: "uploaded_at");

            migrationBuilder.CreateIndex(
                name: "ix_file_metadata_retention_policy_id",
                table: "file_metadata",
                column: "retention_policy_id");

            migrationBuilder.CreateIndex(
                name: "ix_file_metadata_upload_id",
                table: "file_metadata",
                column: "upload_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_retention_policy_is_active",
                table: "retention_policies",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_retention_policy_service_id",
                table: "retention_policies",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "idx_authz_policy_is_active",
                table: "service_authorization_policies",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_authz_policy_service_id",
                table: "service_authorization_policies",
                column: "service_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_upload_events_event_type",
                table: "upload_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "idx_upload_events_file_id",
                table: "upload_events",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "idx_upload_events_service_id",
                table: "upload_events",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "idx_upload_events_timestamp",
                table: "upload_events",
                column: "event_timestamp");

            migrationBuilder.CreateIndex(
                name: "idx_upload_events_upload_id",
                table: "upload_events",
                column: "upload_id");

            migrationBuilder.CreateIndex(
                name: "idx_uploads_service_id",
                table: "uploads",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "idx_uploads_status",
                table: "uploads",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_uploads_storage_path",
                table: "uploads",
                column: "storage_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_uploads_uploaded_at",
                table: "uploads",
                column: "uploaded_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bulk_delete_jobs");

            migrationBuilder.DropTable(
                name: "file_metadata");

            migrationBuilder.DropTable(
                name: "service_authorization_policies");

            migrationBuilder.DropTable(
                name: "upload_events");

            migrationBuilder.DropTable(
                name: "retention_policies");

            migrationBuilder.DropTable(
                name: "uploads");
        }
    }
}

