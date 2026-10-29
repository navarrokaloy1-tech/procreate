using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace procreate_api.Migrations
{
    /// <inheritdoc />
    public partial class AddSoapNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SoapNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Clinic = table.Column<string>(type: "text", nullable: false),
                    NoteDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ChiefComplaint = table.Column<string>(type: "text", nullable: false),
                    HistoryOfPresentIllness = table.Column<string>(type: "text", nullable: false),
                    ObjectiveRemarks = table.Column<string>(type: "text", nullable: false),
                    Assessment = table.Column<string>(type: "text", nullable: false),
                    DiagnosesJson = table.Column<string>(type: "text", nullable: false),
                    Plan = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoapNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoapNotes_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoapNotes_PatientId",
                table: "SoapNotes",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SoapNotes");
        }
    }
}
