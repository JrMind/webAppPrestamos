-- =============================================
-- Migration: Add Admin role and make Rol nullable
-- Run this BEFORE deploying the new code
-- =============================================

-- Make Rol column nullable for users pending role assignment
ALTER TABLE "Usuarios" ALTER COLUMN "Rol" DROP NOT NULL;

-- Note: The enum value "Admin" (value 0) is automatically handled by .NET
-- The enum order is: Admin=0, Socio=1, AportadorInterno=2, AportadorExterno=3, Cobrador=4

-- Create the first admin user if needed (update password hash accordingly)
-- You can use the HashTest project to generate the correct hash
-- Default password: admin123 -> jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=
INSERT INTO "Usuarios" ("Nombre", "Email", "PasswordHash", "Telefono", "Rol", "PorcentajeParticipacion", "TasaInteresMensual", "Activo")
SELECT 'Administrador', 'admin@prestamos.com', 'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=', NULL, 0, 100, 3, true
WHERE NOT EXISTS (SELECT 1 FROM "Usuarios" WHERE "Rol" = 0);

-- Verify the change
SELECT "Id", "Nombre", "Email", "Rol" FROM "Usuarios" ORDER BY "Id";
