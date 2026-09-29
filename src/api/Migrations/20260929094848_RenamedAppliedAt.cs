using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobAppTrackerApi.Migrations
{
    /// <inheritdoc />
    public partial class RenamedAppliedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedAt",
                table: "JobApplications");

            migrationBuilder.AddColumn<DateOnly>(
                name: "AppliedDate",
                table: "JobApplications",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedDate",
                table: "JobApplications");

            migrationBuilder.AddColumn<DateTime>(
                name: "AppliedAt",
                table: "JobApplications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
