using System;
using System.Collections.Generic;
using Maliev.UploadService.Api.Models.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.UploadService.Api.Data.Migrations
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
                    JobId = table.Column<string>(type: "text", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: false),
                    PathPrefix = table.Column<string>(type: "text", nullable: false),
                    InitiatedBy = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FilesTotal = table.Column<int>(type: "integer", nullable: false),
                    FilesProcessed = table.Column<int>(type: "integer", nullable: false),
                    FilesDeleted = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorDetails = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bulk_delete_jobs", x => x.JobId);
                });

            migrationBuilder.CreateTable(
                name: "retention_policies",
                columns: table => new
                {
                    PolicyId = table.Column<string>(type: "text", nullable: false),
                    PolicyName = table.Column<string>(type: "text", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: true),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    StorageClassTransitions = table.Column<List<StorageClassTransition>>(type: "jsonb", nullable: true),
                    ApplyToPathPrefix = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_policies", x => x.PolicyId);
                });

            migrationBuilder.CreateTable(
                name: "service_authorization_policies",
                columns: table => new
                {
                    PolicyId = table.Column<string>(type: "text", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: false),
                    ServiceName = table.Column<string>(type: "text", nullable: false),
                    AllowedPathPrefixes = table.Column<string>(type: "jsonb", nullable: false),
                    AllowedContentTypes = table.Column<string>(type: "jsonb", nullable: false),
                    MaxFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageQuotaBytes = table.Column<long>(type: "bigint", nullable: false),
                    AllowOverwrite = table.Column<bool>(type: "boolean", nullable: false),
                    AllowResumableUpload = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_authorization_policies", x => x.PolicyId);
                });

            migrationBuilder.CreateTable(
                name: "upload_events",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    UploadId = table.Column<string>(type: "text", nullable: true),
                    FileId = table.Column<string>(type: "text", nullable: true),
                    StoragePath = table.Column<string>(type: "text", nullable: true),
                    EventTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventResult = table.Column<string>(type: "text", nullable: false),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upload_events", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "uploads",
                columns: table => new
                {
                    UploadId = table.Column<string>(type: "text", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "text", nullable: true),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    SessionUri = table.Column<string>(type: "text", nullable: true),
                    BytesUploaded = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RetentionPolicyId = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uploads", x => x.UploadId);
                });

            migrationBuilder.CreateTable(
                name: "file_metadata",
                columns: table => new
                {
                    FileId = table.Column<string>(type: "text", nullable: false),
                    UploadId = table.Column<string>(type: "text", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    VersionETag = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Checksum = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetentionPolicyId = table.Column<string>(type: "text", nullable: true),
                    StorageClass = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_metadata", x => x.FileId);
                    table.ForeignKey(
                        name: "FK_file_metadata_uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "uploads",
                        principalColumn: "UploadId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_bulk_delete_created_at",
                table: "bulk_delete_jobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "idx_bulk_delete_service_id",
                table: "bulk_delete_jobs",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "idx_bulk_delete_status",
                table: "bulk_delete_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "idx_filemetadata_expires_at",
                table: "file_metadata",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "idx_filemetadata_service_id",
                table: "file_metadata",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "idx_filemetadata_storage_path",
                table: "file_metadata",
                column: "StoragePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_filemetadata_uploaded_at",
                table: "file_metadata",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_file_metadata_UploadId",
                table: "file_metadata",
                column: "UploadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_retention_policy_is_active",
                table: "retention_policies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "idx_retention_policy_service_id",
                table: "retention_policies",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "idx_authz_policy_is_active",
                table: "service_authorization_policies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "idx_authz_policy_service_id",
                table: "service_authorization_policies",
                column: "ServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_upload_events_event_type",
                table: "upload_events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "idx_upload_events_file_id",
                table: "upload_events",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "idx_upload_events_service_id",
                table: "upload_events",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "idx_upload_events_timestamp",
                table: "upload_events",
                column: "EventTimestamp");

            migrationBuilder.CreateIndex(
                name: "idx_upload_events_upload_id",
                table: "upload_events",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "idx_uploads_service_id",
                table: "uploads",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "idx_uploads_status",
                table: "uploads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "idx_uploads_storage_path",
                table: "uploads",
                column: "StoragePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_uploads_uploaded_at",
                table: "uploads",
                column: "UploadedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bulk_delete_jobs");

            migrationBuilder.DropTable(
                name: "file_metadata");

            migrationBuilder.DropTable(
                name: "retention_policies");

            migrationBuilder.DropTable(
                name: "service_authorization_policies");

            migrationBuilder.DropTable(
                name: "upload_events");

            migrationBuilder.DropTable(
                name: "uploads");
        }
    }
}
