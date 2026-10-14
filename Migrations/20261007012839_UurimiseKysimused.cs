using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FunktsioonideUurimine.Migrations
{
    /// <inheritdoc />
    public partial class UurimiseKysimused : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Asumptoodid",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Kaanupunktid",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Kahanemisvahemikud",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Kasvamisvahemikud",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Kumerusvahemikud",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Muutumispiirkond",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Negatiivsuspiirkond",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Nogususvahemikud",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Paarsus",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Perioodilisus",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Positiivsuspiirkond",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeineTuletis",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "YTeljeLoikepunkt",
                table: "FunktsiooniUurimised",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Asumptoodid",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "Kaanupunktid",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "Kahanemisvahemikud",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "Kasvamisvahemikud",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "Kumerusvahemikud",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "Muutumispiirkond",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "Negatiivsuspiirkond",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "Nogususvahemikud",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "Paarsus",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "Perioodilisus",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "Positiivsuspiirkond",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "TeineTuletis",
                table: "FunktsiooniUurimised");

            migrationBuilder.DropColumn(
                name: "YTeljeLoikepunkt",
                table: "FunktsiooniUurimised");
        }
    }
}
