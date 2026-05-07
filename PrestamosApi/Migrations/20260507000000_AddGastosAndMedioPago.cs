using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PrestamosApi.Migrations
{
    /// <inheritdoc />
    public partial class AddGastosAndMedioPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tabla de Gastos
            migrationBuilder.CreateTable(
                name: "gastos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    mediopago = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Efectivo"),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    categoria = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gastos", x => x.id);
                });

            // Columna CobradoMedioPago en CuotasPrestamo
            migrationBuilder.AddColumn<string>(
                name: "cobradomediopago",
                table: "cuotasprestamo",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "gastos");

            migrationBuilder.DropColumn(
                name: "cobradomediopago",
                table: "cuotasprestamo");
        }
    }
}
