using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FunktsioonideUurimine.Migrations
{
    /// <inheritdoc />
    public partial class AlgneLoomine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FunktsiooniUurimised",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Valem = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Maaramispiirkond = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Tuletis = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Nullkohad = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    KriitilisedPunktid = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Ekstreemumid = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    LuodudAeg = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunktsiooniUurimised", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FunktsiooniUurimised_LuodudAeg",
                table: "FunktsiooniUurimised",
                column: "LuodudAeg");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FunktsiooniUurimised");
        }
    }
}
