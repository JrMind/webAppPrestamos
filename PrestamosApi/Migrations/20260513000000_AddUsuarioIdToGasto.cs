using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrestamosApi.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioIdToGasto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "usuarioid",
                table: "gastos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_gastos_usuarios_usuarioid",
                table: "gastos",
                column: "usuarioid",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_gastos_usuarios_usuarioid",
                table: "gastos");

            migrationBuilder.DropColumn(
                name: "usuarioid",
                table: "gastos");
        }
    }
}
