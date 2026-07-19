using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.UploadService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuoteEngineCustomerDocumentPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "service_authorization_policies",
                keyColumn: "policy_id",
                keyValue: "policy-quote-engine",
                columns: new[] { "allowed_content_types", "allowed_path_prefixes" },
                values: new object[] { "[\"application/octet-stream\",\"application/pdf\",\"application/step\",\"application/iges\",\"application/msword\",\"application/vnd.ms-excel\",\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet\",\"application/vnd.openxmlformats-officedocument.wordprocessingml.document\",\"image/jpeg\",\"image/png\",\"image/webp\",\"model/3mf\",\"model/obj\",\"model/stl\"]", "[\"quotes/temp/\",\"customers/\",\"customer-documents/\"]" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "service_authorization_policies",
                keyColumn: "policy_id",
                keyValue: "policy-quote-engine",
                columns: new[] { "allowed_content_types", "allowed_path_prefixes" },
                values: new object[] { "[\"application/octet-stream\",\"application/step\",\"application/iges\",\"model/3mf\",\"model/obj\",\"model/stl\"]", "[\"quotes/temp/\",\"customers/\"]" });
        }
    }
}
