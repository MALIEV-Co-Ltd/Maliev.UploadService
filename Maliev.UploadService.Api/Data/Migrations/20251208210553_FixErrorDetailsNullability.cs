using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.UploadService.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixErrorDetailsNullability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeleteFilesOlderThan",
                table: "bulk_delete_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Errors",
                table: "bulk_delete_jobs",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FilesFailed",
                table: "bulk_delete_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "bulk_delete_jobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalFiles",
                table: "bulk_delete_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<List<string>>(
                name: "UploadIds",
                table: "bulk_delete_jobs",
                type: "text[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeleteFilesOlderThan",
                table: "bulk_delete_jobs");

            migrationBuilder.DropColumn(
                name: "Errors",
                table: "bulk_delete_jobs");

            migrationBuilder.DropColumn(
                name: "FilesFailed",
                table: "bulk_delete_jobs");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "bulk_delete_jobs");

            migrationBuilder.DropColumn(
                name: "TotalFiles",
                table: "bulk_delete_jobs");

            migrationBuilder.DropColumn(
                name: "UploadIds",
                table: "bulk_delete_jobs");
        }
    }
}
