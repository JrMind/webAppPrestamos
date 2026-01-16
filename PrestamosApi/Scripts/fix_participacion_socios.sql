-- Poner porcentajeparticipacion = 0 para todos los socios

-- Ver estado actual
SELECT id, nombre, porcentajeparticipacion FROM usuarios WHERE rol = 'Socio';

-- Actualizar
UPDATE usuarios SET porcentajeparticipacion = 0 WHERE rol = 'Socio';

-- Verificar
SELECT id, nombre, porcentajeparticipacion FROM usuarios WHERE rol = 'Socio';
