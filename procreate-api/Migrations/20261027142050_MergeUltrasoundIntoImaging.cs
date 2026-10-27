using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace procreate_api.Migrations
{
    /// <inheritdoc />
    public partial class MergeUltrasoundIntoImaging : Migration
    {
        // Data-only migration. Ultrasound is merged into Imaging, so every test
        // category on the Ultrasound line (the seeded "Sonography" plus any the
        // clinic added) moves to Imaging. Orders follow automatically — they are
        // grouped by their test's category department. The scaffolded seed-data
        // UpdateData churn is replaced by this single broad UPDATE.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"TestCategories\" SET \"Department\" = 'Imaging' WHERE \"Department\" = 'Ultrasound';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort revert of the seeded sonography category.
            migrationBuilder.Sql(
                "UPDATE \"TestCategories\" SET \"Department\" = 'Ultrasound' WHERE \"Name\" = 'Sonography';");
        }
    }
}
