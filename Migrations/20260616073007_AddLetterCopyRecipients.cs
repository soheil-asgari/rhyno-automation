using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddLetterCopyRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LetterCopyRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LetterId = table.Column<int>(type: "int", nullable: false),
                    RecipientId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetterCopyRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LetterCopyRecipients_AspNetUsers_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LetterCopyRecipients_Letters_LetterId",
                        column: x => x.LetterId,
                        principalTable: "Letters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LetterCopyRecipients_LetterId_RecipientId",
                table: "LetterCopyRecipients",
                columns: new[] { "LetterId", "RecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LetterCopyRecipients_RecipientId",
                table: "LetterCopyRecipients",
                column: "RecipientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LetterCopyRecipients");
        }
    }
}
