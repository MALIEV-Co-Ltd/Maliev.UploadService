using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.SupplierService.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "material_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    onboarding_stage = table.Column<int>(type: "integer", nullable: false),
                    last_order_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_order_value = table.Column<decimal>(type: "numeric", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<int>(type: "integer", nullable: false),
                    transitioned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    transitioned_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    transitioned_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_statuses", x => x.id);
                    table.ForeignKey(
                        name: "FK_onboarding_statuses_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "performance_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating_category = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    evaluation_date = table.Column<DateOnly>(type: "date", nullable: false),
                    evaluator_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evaluator_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_performance_evaluations", x => x.id);
                    table.CheckConstraint("ck_performance_evaluations_score", "score >= 1 AND score <= 5");
                    table.ForeignKey(
                        name: "FK_performance_evaluations_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    changed_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_audit_logs_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_capabilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_capabilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_capabilities_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_certifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    document_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expiration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    external_file_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_certifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_certifications_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_contacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_contacts_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_material_categories",
                columns: table => new
                {
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_material_categories", x => new { x.supplier_id, x.material_category_id });
                    table.ForeignKey(
                        name: "FK_supplier_material_categories_material_categories_material_c~",
                        column: x => x.material_category_id,
                        principalTable: "material_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_supplier_material_categories_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_material_categories_is_active",
                table: "material_categories",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_material_categories_name",
                table: "material_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_statuses_stage",
                table: "onboarding_statuses",
                column: "stage");

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_statuses_supplier_id",
                table: "onboarding_statuses",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_statuses_transitioned_at",
                table: "onboarding_statuses",
                column: "transitioned_at");

            migrationBuilder.CreateIndex(
                name: "ix_performance_evaluations_evaluation_date",
                table: "performance_evaluations",
                column: "evaluation_date");

            migrationBuilder.CreateIndex(
                name: "ix_performance_evaluations_rating_category",
                table: "performance_evaluations",
                column: "rating_category");

            migrationBuilder.CreateIndex(
                name: "ix_performance_evaluations_supplier_id",
                table: "performance_evaluations",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_audit_logs_changed_by",
                table: "supplier_audit_logs",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_audit_logs_entity_type_entity_id",
                table: "supplier_audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_audit_logs_supplier_id",
                table: "supplier_audit_logs",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_audit_logs_timestamp",
                table: "supplier_audit_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_capabilities_name",
                table: "supplier_capabilities",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_capabilities_supplier_id",
                table: "supplier_capabilities",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_certifications_document_type",
                table: "supplier_certifications",
                column: "document_type");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_certifications_expiration_date",
                table: "supplier_certifications",
                column: "expiration_date");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_certifications_supplier_id",
                table: "supplier_certifications",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_contacts_email",
                table: "supplier_contacts",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_contacts_supplier_id",
                table: "supplier_contacts",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_material_categories_category_id",
                table: "supplier_material_categories",
                column: "material_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_company_name",
                table: "suppliers",
                column: "company_name");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_onboarding_stage",
                table: "suppliers",
                column: "onboarding_stage");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_status",
                table: "suppliers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_tax_id",
                table: "suppliers",
                column: "tax_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "onboarding_statuses");

            migrationBuilder.DropTable(
                name: "performance_evaluations");

            migrationBuilder.DropTable(
                name: "supplier_audit_logs");

            migrationBuilder.DropTable(
                name: "supplier_capabilities");

            migrationBuilder.DropTable(
                name: "supplier_certifications");

            migrationBuilder.DropTable(
                name: "supplier_contacts");

            migrationBuilder.DropTable(
                name: "supplier_material_categories");

            migrationBuilder.DropTable(
                name: "material_categories");

            migrationBuilder.DropTable(
                name: "suppliers");
        }
    }
}
