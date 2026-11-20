using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace procreate_api.Migrations
{
    /// <summary>
    /// Files filed with a SOAP note — a scan, a photograph of a finding, a
    /// report brought in on paper. Cascades from the note, so deleting the
    /// consultation write-up takes its files with it.
    ///
    /// Scaffolding also produced UpdateData rows for every seeded CreatedAt
    /// and PasswordHash, because the seed builds those at model-build time
    /// (DateTime.UtcNow, BCrypt). They are noise, and re-running them would
    /// rewrite live seed rows on deploy, so they are left out here as in the
    /// migrations before this one.
    /// </summary>
    public partial class AddSoapAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SoapAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SoapNoteId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedBy = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoapAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoapAttachments_SoapNotes_SoapNoteId",
                        column: x => x.SoapNoteId,
                        principalTable: "SoapNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoapAttachments_SoapNoteId",
                table: "SoapAttachments",
                column: "SoapNoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SoapAttachments");
        }
    }
}
