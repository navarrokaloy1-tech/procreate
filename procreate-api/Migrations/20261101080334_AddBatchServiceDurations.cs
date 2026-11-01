using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace procreate_api.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchServiceDurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultServiceMinutes",
                table: "AppointmentBatchTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<string>(
                name: "ServiceDurationsJson",
                table: "AppointmentBatchTemplates",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<int>(
                name: "DefaultServiceMinutes",
                table: "AppointmentBatches",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<string>(
                name: "ServiceDurationsJson",
                table: "AppointmentBatches",
                type: "text",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultServiceMinutes",
                table: "AppointmentBatchTemplates");

            migrationBuilder.DropColumn(
                name: "ServiceDurationsJson",
                table: "AppointmentBatchTemplates");

            migrationBuilder.DropColumn(
                name: "DefaultServiceMinutes",
                table: "AppointmentBatches");

            migrationBuilder.DropColumn(
                name: "ServiceDurationsJson",
                table: "AppointmentBatches");
        }
    }
}
