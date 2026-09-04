using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrestamosApi.Migrations
{
    public partial class AddTipoPago : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE pagos ADD COLUMN IF NOT EXISTS tipopago varchar(20) NOT NULL DEFAULT 'Cuota';
            ");

            // Los pagos existentes sin cuota asociada eran abonos al capital
            // (así los mostraba el frontend antes de existir esta columna).
            migrationBuilder.Sql(@"
                UPDATE pagos SET tipopago = 'AbonoCapital'
                WHERE cuotaid IS NULL AND tipopago = 'Cuota';
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_pagos_tipopago ON pagos (tipopago);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS idx_pagos_tipopago;
                ALTER TABLE pagos DROP COLUMN IF EXISTS tipopago;
            ");
        }
    }
}
