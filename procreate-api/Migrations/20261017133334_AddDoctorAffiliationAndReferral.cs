using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace procreate_api.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorAffiliationAndReferral : Migration
    {
        // Only the real schema changes are kept. EF also scaffolded UpdateData for
        // every seeded row (dynamic CreatedAt / BCrypt hashes in HasData); that
        // churn is omitted so the deploy touches nothing but the new structures.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReferringDoctorId",
                table: "Visits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Affiliation",
                table: "Doctors",
                type: "text",
                nullable: false,
                // Existing doctors are treated as in-house so the list ordering
                // and referral tallies are sensible before anyone is edited.
                defaultValue: "In-House");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_ReferringDoctorId",
                table: "Visits",
                column: "ReferringDoctorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_Doctors_ReferringDoctorId",
                table: "Visits",
                column: "ReferringDoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Visits_Doctors_ReferringDoctorId",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_Visits_ReferringDoctorId",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "ReferringDoctorId",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "Affiliation",
                table: "Doctors");
        }
    }
}
