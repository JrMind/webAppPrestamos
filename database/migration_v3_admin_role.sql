-- =============================================
-- Migration: Add Admin role and make Rol nullable
-- Run this BEFORE deploying the new code
-- =============================================

-- Make Rol column nullable for users pending role assignment
ALTER TABLE usuarios ALTER COLUMN rol DROP NOT NULL;

-- Note: The enum value "Admin" (value 0) is automatically handled by .NET
-- The enum order is: Admin=0, Socio=1, AportadorInterno=2, AportadorExterno=3, Cobrador=4
-- In DB, rol is stored as VARCHAR with the enum name string

-- Create the first admin user if needed (update password hash accordingly)
-- You can use the HashTest project to generate the correct hash
-- Default password: admin123 -> jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=
INSERT INTO usuarios (nombre, email, passwordhash, telefono, rol, porcentajeparticipacion, tasainteresmensual, activo)
SELECT 'Administrador', 'admin@prestamos.com', 'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=', NULL, 'Admin', 100, 3, true
WHERE NOT EXISTS (SELECT 1 FROM usuarios WHERE rol = 'Admin');

-- Verify the change
SELECT id, nombre, email, rol FROM usuarios ORDER BY id;

