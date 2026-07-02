using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddLetterReplyLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReplyToLetterId",
                table: "Letters",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Letters_ReplyToLetterId",
                table: "Letters",
                column: "ReplyToLetterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Letters_Letters_ReplyToLetterId",
                table: "Letters",
                column: "ReplyToLetterId",
                principalTable: "Letters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Letters_Letters_ReplyToLetterId",
                table: "Letters");

            migrationBuilder.DropIndex(
                name: "IX_Letters_ReplyToLetterId",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "ReplyToLetterId",
                table: "Letters");
        }
    }
}

