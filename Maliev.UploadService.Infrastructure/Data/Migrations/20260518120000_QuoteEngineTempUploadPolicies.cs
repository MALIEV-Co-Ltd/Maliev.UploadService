using System;
using Maliev.UploadService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Maliev.UploadService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuoteEngineTempUploadPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "retention_policies",
                columns: new[] { "policy_id", "apply_to_path_prefix", "created_at", "is_active", "policy_name", "retention_days", "service_id", "storage_class_transitions", "updated_at" },
                columnTypes: new[] { "text", "text", "timestamp with time zone", "boolean", "text", "integer", "text", "jsonb", "timestamp with time zone" },
                values: new object[] { "quote-temp-uploads", "quotes/temp/", new DateTime(2026, 5, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, "Temporary Quote Uploads (7 days)", 7, null, null, new DateTime(2026, 5, 18, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "service_authorization_policies",
                columns: new[] { "policy_id", "allow_overwrite", "allow_resumable_upload", "allowed_content_types", "allowed_path_prefixes", "created_at", "is_active", "max_file_size_bytes", "service_id", "service_name", "storage_quota_bytes", "updated_at" },
                columnTypes: new[] { "text", "boolean", "boolean", "jsonb", "jsonb", "timestamp with time zone", "boolean", "bigint", "text", "text", "bigint", "timestamp with time zone" },
                values: new object[,]
                {
                    { "policy-web-bff", true, true, "[\"application/octet-stream\",\"application/step\",\"application/iges\",\"model/3mf\",\"model/obj\",\"model/stl\"]", "[\"quotes/temp/\"]", new DateTime(2026, 5, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 10737418240L, "WebBff", "MALIEV Web BFF", 107374182400L, new DateTime(2026, 5, 18, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "policy-quote-engine", true, true, "[\"application/octet-stream\",\"application/step\",\"application/iges\",\"model/3mf\",\"model/obj\",\"model/stl\"]", "[\"quotes/temp/\",\"customers/\"]", new DateTime(2026, 5, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, 10737418240L, "QuoteEngine", "MALIEV Quote Engine", 268435456000L, new DateTime(2026, 5, 18, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "service_authorization_policies",
                keyColumn: "policy_id",
                keyValue: "policy-web-bff");

            migrationBuilder.DeleteData(
                table: "service_authorization_policies",
                keyColumn: "policy_id",
                keyValue: "policy-quote-engine");

            migrationBuilder.DeleteData(
                table: "retention_policies",
                keyColumn: "policy_id",
                keyValue: "quote-temp-uploads");
        }
    }
}
