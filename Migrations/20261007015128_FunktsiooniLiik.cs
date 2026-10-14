using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FunktsioonideUurimine.Migrations
{
    /// <inheritdoc />
    public partial class FunktsiooniLiik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Liik",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_FunktsiooniUurimised_Liik",
                table: "FunktsiooniUurimised",
                column: "Liik");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FunktsiooniUurimised_Liik",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "Liik",
                table: "FunktsiooniUurimised");
        }
    }
}
