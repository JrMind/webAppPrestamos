-- Verificar por qué Admin tiene más que los otros socios

-- 1. Ver campos de usuarios socios
SELECT 
    id,
    nombre,
    email,
    porcentajeparticipacion,
    capitalactual,
    gananciasacumuladas
FROM usuarios
WHERE rol = 'Socio';

-- 2. Ver si Admin es cobrador de algún préstamo
SELECT 
    p.id as prestamo_id,
    c.nombre as cliente,
    p.montoprestado,
    p.porcentajecobrador,
    u.nombre as cobrador
FROM prestamos p
JOIN usuarios u ON p.cobradorid = u.id
JOIN clientes c ON p.clienteid = c.id
WHERE u.nombre ILIKE '%admin%';

-- 3. Ver distribución de ganancias si existe la tabla
-- SELECT * FROM distribucionesganancia WHERE usuarioid = (SELECT id FROM usuarios WHERE nombre ILIKE '%admin%');
