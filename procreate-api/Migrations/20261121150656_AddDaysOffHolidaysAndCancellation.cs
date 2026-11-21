using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace procreate_api.Migrations
{
    /// <summary>
    /// Days a doctor is away, days the clinic is shut, a cancellable
    /// diagnostic order, and the desk's note on where a waiting patient has
    /// got to.
    ///
    /// Scaffolding also produced UpdateData for every seeded CreatedAt and
    /// PasswordHash, because the seed builds those at model-build time. They
    /// are noise, and re-running them would rewrite live seed rows on deploy,
    /// so they are left out here as in the migrations before this one.
    /// </summary>
    public partial class AddDaysOffHolidaysAndCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "LabOrders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "LabOrders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledBy",
                table: "LabOrders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WaitingNote",
                table: "Appointments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ClinicHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicHolidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoctorDaysOff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DoctorId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorDaysOff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorDaysOff_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicHolidays_Date",
                table: "ClinicHolidays",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorDaysOff_DoctorId_Date",
                table: "DoctorDaysOff",
                columns: new[] { "DoctorId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicHolidays");

            migrationBuilder.DropTable(
                name: "DoctorDaysOff");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "WaitingNote",
                table: "Appointments");

        }
    }
}
