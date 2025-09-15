using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                name: "uploaded_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subcategory = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ObjectName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Bucket = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UploadedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    QuotationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InvoiceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceiptId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    AccessLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RetentionPolicy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProcessingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProcessingNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Md5Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Sha256Hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uploaded_files", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "file_access_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    AccessedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AdditionalInfo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_access_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_file_access_logs_uploaded_files_FileId",
                        column: x => x.FileId,
                        principalTable: "uploaded_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_file_access_logs_accessed_at",
                table: "file_access_logs",
                column: "AccessedAt");

            migrationBuilder.CreateIndex(
                name: "idx_file_access_logs_accessed_by",
                table: "file_access_logs",
                column: "AccessedBy");

            migrationBuilder.CreateIndex(
                name: "idx_file_access_logs_access_type",
                table: "file_access_logs",
                column: "AccessType");

            migrationBuilder.CreateIndex(
                name: "idx_file_access_logs_file_id",
                table: "file_access_logs",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "idx_uploaded_files_category",
                table: "uploaded_files",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "idx_uploaded_files_category_entity",
                table: "uploaded_files",
                columns: new[] { "Category", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "idx_uploaded_files_customer_id",
                table: "uploaded_files",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "idx_uploaded_files_entity_id",
                table: "uploaded_files",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "idx_uploaded_files_is_deleted",
                table: "uploaded_files",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "idx_uploaded_files_object_name_unique",
                table: "uploaded_files",
                column: "ObjectName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_uploaded_files_order_id",
                table: "uploaded_files",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "idx_uploaded_files_uploaded_at",
                table: "uploaded_files",
                column: "UploadedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_access_logs");

            migrationBuilder.DropTable(
                name: "uploaded_files");
        }
    }
}
