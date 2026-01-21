using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PrestamosApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPagoCostoAndTotalPagado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracionsistema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    valor = table.Column<string>(type: "text", nullable: false),
                    fechaactualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    descripcion = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracionsistema", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "costos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    frecuencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Mensual"),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    fechafin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    totalpagado = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_costos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pagoscostos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    costoid = table.Column<int>(type: "integer", nullable: false),
                    montopagado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    fechapago = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    metodopago = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comprobante = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    observaciones = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagoscostos", x => x.id);
                    table.ForeignKey(
                        name: "FK_pagoscostos_costos_costoid",
                        column: x => x.costoid,
                        principalTable: "costos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_configuracionsistema_clave",
                table: "configuracionsistema",
                column: "clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_pagoscostos_costo",
                table: "pagoscostos",
                column: "costoid");

            migrationBuilder.CreateIndex(
                name: "idx_pagoscostos_fecha",
                table: "pagoscostos",
                column: "fechapago");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracionsistema");

            migrationBuilder.DropTable(
                name: "pagoscostos");

            migrationBuilder.DropTable(
                name: "costos");
        }
    }
}
