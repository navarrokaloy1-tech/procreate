using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace procreate_api.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleLabCategories : Migration
    {
        // Only the real schema change is kept; EF's seed-data UpdateData churn
        // (dynamic HasData values) is omitted so the deploy adds just the column.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LabCategories",
                table: "CustomRoles",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabCategories",
                table: "CustomRoles");
        }
    }
}
