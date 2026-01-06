using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PrestamosApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DiaSemana",
                table: "prestamos",
                newName: "diasemana");

            migrationBuilder.CreateTable(
                name: "smscampaigns",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mensaje = table.Column<string>(type: "text", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    diasenvio = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    horasenvio = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    vecespordia = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    tipodestinatario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_smscampaigns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "smshistory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    smscampaignid = table.Column<int>(type: "integer", nullable: true),
                    clienteid = table.Column<int>(type: "integer", nullable: true),
                    numerotelefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mensaje = table.Column<string>(type: "text", nullable: false),
                    fechaenvio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    twiliosid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    errormessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_smshistory", x => x.id);
                    table.ForeignKey(
                        name: "FK_smshistory_clientes_clienteid",
                        column: x => x.clienteid,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_smshistory_smscampaigns_smscampaignid",
                        column: x => x.smscampaignid,
                        principalTable: "smscampaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_smshistory_campaign",
                table: "smshistory",
                column: "smscampaignid");

            migrationBuilder.CreateIndex(
                name: "idx_smshistory_fecha",
                table: "smshistory",
                column: "fechaenvio");

            migrationBuilder.CreateIndex(
                name: "IX_smshistory_clienteid",
                table: "smshistory",
                column: "clienteid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "smshistory");

            migrationBuilder.DropTable(
                name: "smscampaigns");

            migrationBuilder.RenameColumn(
                name: "diasemana",
                table: "prestamos",
                newName: "DiaSemana");
        }
    }
}
