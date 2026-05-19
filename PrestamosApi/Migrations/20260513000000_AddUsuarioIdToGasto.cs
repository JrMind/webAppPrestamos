using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrestamosApi.Migrations
{
    public partial class AddUsuarioIdToGasto : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE gastos ADD COLUMN IF NOT EXISTS usuarioid integer;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'FK_gastos_usuarios_usuarioid'
                          AND table_name = 'gastos'
                    ) THEN
                        ALTER TABLE gastos
                            ADD CONSTRAINT FK_gastos_usuarios_usuarioid
                            FOREIGN KEY (usuarioid) REFERENCES usuarios(id)
                            ON DELETE SET NULL;
                    END IF;
                END
                $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE gastos DROP CONSTRAINT IF EXISTS FK_gastos_usuarios_usuarioid;
                ALTER TABLE gastos DROP COLUMN IF EXISTS usuarioid;
            ");
        }
    }
}
