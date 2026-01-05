using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PrestamosApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDiaSemanaToPrestamo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "aportadoresexternos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    tasainteres = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 3m),
                    diasparapago = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    montototalaportado = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    montopagado = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    saldopendiente = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Activo"),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    notas = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aportadoresexternos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cedula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fecharegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    passwordhash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    porcentajeparticipacion = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
                    tasainteresmensual = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 3m),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    capitalactual = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    gananciasacumuladas = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    ultimocalculointeres = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pagosaportadoresexternos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    aportadorexternoid = table.Column<int>(type: "integer", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    montocapital = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    montointereses = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    fechapago = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    metodopago = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comprobante = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    notas = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagosaportadoresexternos", x => x.id);
                    table.ForeignKey(
                        name: "FK_pagosaportadoresexternos_aportadoresexternos_aportadorexter~",
                        column: x => x.aportadorexternoid,
                        principalTable: "aportadoresexternos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "aportes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuarioid = table.Column<int>(type: "integer", nullable: false),
                    montoinicial = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    montoactual = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    fechaaporte = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aportes", x => x.id);
                    table.ForeignKey(
                        name: "FK_aportes_usuarios_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "movimientoscapital",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuarioid = table.Column<int>(type: "integer", nullable: false),
                    tipomovimiento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    saldoanterior = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    saldonuevo = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    fechamovimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimientoscapital", x => x.id);
                    table.ForeignKey(
                        name: "FK_movimientoscapital_usuarios_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prestamos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clienteid = table.Column<int>(type: "integer", nullable: false),
                    cobradorid = table.Column<int>(type: "integer", nullable: true),
                    montoprestado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tasainteres = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tipointeres = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Simple"),
                    frecuenciapago = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DiaSemana = table.Column<string>(type: "text", nullable: true),
                    numerocuotas = table.Column<int>(type: "integer", nullable: false),
                    fechaprestamo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fechavencimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    montototal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    montointereses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    montocuota = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    estadoprestamo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Activo"),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    porcentajecobrador = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 5m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prestamos", x => x.id);
                    table.ForeignKey(
                        name: "FK_prestamos_clientes_clienteid",
                        column: x => x.clienteid,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prestamos_usuarios_cobradorid",
                        column: x => x.cobradorid,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cuotasprestamo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    prestamoid = table.Column<int>(type: "integer", nullable: false),
                    numerocuota = table.Column<int>(type: "integer", nullable: false),
                    fechacobro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    montocuota = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    montopagado = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    saldopendiente = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    estadocuota = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    fechapago = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    cobrado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuotasprestamo", x => x.id);
                    table.ForeignKey(
                        name: "FK_cuotasprestamo_prestamos_prestamoid",
                        column: x => x.prestamoid,
                        principalTable: "prestamos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "distribucionesganancias",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    prestamoid = table.Column<int>(type: "integer", nullable: false),
                    usuarioid = table.Column<int>(type: "integer", nullable: false),
                    porcentajeasignado = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    montoganancia = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    fechadistribucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    liquidado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_distribucionesganancias", x => x.id);
                    table.ForeignKey(
                        name: "FK_distribucionesganancias_prestamos_prestamoid",
                        column: x => x.prestamoid,
                        principalTable: "prestamos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_distribucionesganancias_usuarios_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fuentescapitalprestamo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    prestamoid = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    usuarioid = table.Column<int>(type: "integer", nullable: true),
                    aportadorexternoid = table.Column<int>(type: "integer", nullable: true),
                    montoaportado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    porcentajeparticipacion = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
                    fecharegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuentescapitalprestamo", x => x.id);
                    table.ForeignKey(
                        name: "FK_fuentescapitalprestamo_aportadoresexternos_aportadorexterno~",
                        column: x => x.aportadorexternoid,
                        principalTable: "aportadoresexternos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fuentescapitalprestamo_prestamos_prestamoid",
                        column: x => x.prestamoid,
                        principalTable: "prestamos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fuentescapitalprestamo_usuarios_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "pagos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    prestamoid = table.Column<int>(type: "integer", nullable: false),
                    cuotaid = table.Column<int>(type: "integer", nullable: true),
                    montopago = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    fechapago = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    metodopago = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comprobante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    observaciones = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagos", x => x.id);
                    table.ForeignKey(
                        name: "FK_pagos_cuotasprestamo_cuotaid",
                        column: x => x.cuotaid,
                        principalTable: "cuotasprestamo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pagos_prestamos_prestamoid",
                        column: x => x.prestamoid,
                        principalTable: "prestamos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_aportes_usuario",
                table: "aportes",
                column: "usuarioid");

            migrationBuilder.CreateIndex(
                name: "IX_clientes_cedula",
                table: "clientes",
                column: "cedula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_cuotas_estado",
                table: "cuotasprestamo",
                column: "estadocuota");

            migrationBuilder.CreateIndex(
                name: "idx_cuotas_fecha",
                table: "cuotasprestamo",
                column: "fechacobro");

            migrationBuilder.CreateIndex(
                name: "idx_cuotas_prestamo",
                table: "cuotasprestamo",
                column: "prestamoid");

            migrationBuilder.CreateIndex(
                name: "IX_cuotasprestamo_prestamoid_numerocuota",
                table: "cuotasprestamo",
                columns: new[] { "prestamoid", "numerocuota" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_distribuciones_prestamo",
                table: "distribucionesganancias",
                column: "prestamoid");

            migrationBuilder.CreateIndex(
                name: "idx_distribuciones_usuario",
                table: "distribucionesganancias",
                column: "usuarioid");

            migrationBuilder.CreateIndex(
                name: "idx_fuentescapital_aportador",
                table: "fuentescapitalprestamo",
                column: "aportadorexternoid");

            migrationBuilder.CreateIndex(
                name: "idx_fuentescapital_prestamo",
                table: "fuentescapitalprestamo",
                column: "prestamoid");

            migrationBuilder.CreateIndex(
                name: "idx_fuentescapital_usuario",
                table: "fuentescapitalprestamo",
                column: "usuarioid");

            migrationBuilder.CreateIndex(
                name: "idx_movimientos_fecha",
                table: "movimientoscapital",
                column: "fechamovimiento");

            migrationBuilder.CreateIndex(
                name: "idx_movimientos_usuario",
                table: "movimientoscapital",
                column: "usuarioid");

            migrationBuilder.CreateIndex(
                name: "idx_pagos_cuota",
                table: "pagos",
                column: "cuotaid");

            migrationBuilder.CreateIndex(
                name: "idx_pagos_prestamo",
                table: "pagos",
                column: "prestamoid");

            migrationBuilder.CreateIndex(
                name: "idx_pagosaportadores_aportador",
                table: "pagosaportadoresexternos",
                column: "aportadorexternoid");

            migrationBuilder.CreateIndex(
                name: "idx_prestamos_cliente",
                table: "prestamos",
                column: "clienteid");

            migrationBuilder.CreateIndex(
                name: "idx_prestamos_cobrador",
                table: "prestamos",
                column: "cobradorid");

            migrationBuilder.CreateIndex(
                name: "idx_prestamos_estado",
                table: "prestamos",
                column: "estadoprestamo");

            migrationBuilder.CreateIndex(
                name: "idx_prestamos_fecha",
                table: "prestamos",
                column: "fechaprestamo");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aportes");

            migrationBuilder.DropTable(
                name: "distribucionesganancias");

            migrationBuilder.DropTable(
                name: "fuentescapitalprestamo");

            migrationBuilder.DropTable(
                name: "movimientoscapital");

            migrationBuilder.DropTable(
                name: "pagos");

            migrationBuilder.DropTable(
                name: "pagosaportadoresexternos");

            migrationBuilder.DropTable(
                name: "cuotasprestamo");

            migrationBuilder.DropTable(
                name: "aportadoresexternos");

            migrationBuilder.DropTable(
                name: "prestamos");

            migrationBuilder.DropTable(
                name: "clientes");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
