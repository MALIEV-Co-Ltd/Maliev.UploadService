using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Maliev.UploadService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlatformUploadPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "service_authorization_policies",
                columns: new[] { "policy_id", "allow_overwrite", "allow_resumable_upload", "allowed_content_types", "allowed_path_prefixes", "created_at", "is_active", "max_file_size_bytes", "service_id", "service_name", "storage_quota_bytes", "updated_at" },
                values: new object[,]
                {
                    { "policy-intranet-bff", true, true, "[\"application/octet-stream\",\"application/pdf\",\"application/step\",\"application/iges\",\"image/jpeg\",\"image/png\",\"image/webp\",\"model/3mf\",\"model/obj\",\"model/stl\"]", "[\"customers/\",\"projects/\"]", new DateTime(2026, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc), true, 10737418240L, "Intranet", "MALIEV Intranet BFF", 107374182400L, new DateTime(2026, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "policy-pdf-service", true, true, "[\"application/pdf\"]", "[\"pdfs/\"]", new DateTime(2026, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc), true, 104857600L, "PdfService", "PDF Service", 10737418240L, new DateTime(2026, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "service_authorization_policies",
                keyColumn: "policy_id",
                keyValue: "policy-intranet-bff");

            migrationBuilder.DeleteData(
                table: "service_authorization_policies",
                keyColumn: "policy_id",
                keyValue: "policy-pdf-service");
        }
    }
}
