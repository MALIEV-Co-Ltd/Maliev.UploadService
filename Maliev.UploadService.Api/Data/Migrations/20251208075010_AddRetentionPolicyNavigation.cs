using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.UploadService.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionPolicyNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_file_metadata_RetentionPolicyId",
                table: "file_metadata",
                column: "RetentionPolicyId");

            migrationBuilder.AddForeignKey(
                name: "FK_file_metadata_retention_policies_RetentionPolicyId",
                table: "file_metadata",
                column: "RetentionPolicyId",
                principalTable: "retention_policies",
                principalColumn: "PolicyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_file_metadata_retention_policies_RetentionPolicyId",
                table: "file_metadata");

            migrationBuilder.DropIndex(
                name: "IX_file_metadata_RetentionPolicyId",
                table: "file_metadata");
        }
    }
}
