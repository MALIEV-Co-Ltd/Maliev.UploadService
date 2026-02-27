using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.UploadService.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModelWithComparers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "service_authorization_policies",
                columns: new[] { "policy_id", "allow_overwrite", "allow_resumable_upload", "allowed_content_types", "allowed_path_prefixes", "created_at", "is_active", "max_file_size_bytes", "service_id", "service_name", "storage_quota_bytes", "updated_at" },
                values: new object[] { "policy-geometry-service", true, true, "[\"application/octet-stream\",\"model/stl\",\"text/plain\"]", "[\"geometry-test\",\"geometry/\"]", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 104857600L, "geometry-service", "Geometry Analysis Service", 1073741824L, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "service_authorization_policies",
                keyColumn: "policy_id",
                keyValue: "policy-geometry-service");
        }
    }
}
