using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace procreate_api.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRebate : Migration
    {
        // Only the real schema change is kept. EF also scaffolded UpdateData for
        // every seeded row because the HasData seed uses dynamic values
        // (CreatedAt = now, BCrypt hashes), which differ each build; those
        // rewrites are noise and are intentionally omitted so the migration — and
        // the Supabase deploy — touch nothing but the new column.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RebateAmount",
                table: "Products",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RebateAmount",
                table: "Products");
        }
    }
}
