-- ============================================================
-- SCRIPT: Poner porcentaje cobrador en 0 para Gabriel
-- ============================================================

-- PASO 1: Ver préstamos actuales de Gabriel
SELECT 
    p.id as prestamo_id,
    c.nombre as cliente,
    p.montoprestado,
    p.tasainteres,
    p.porcentajecobrador,
    u.nombre as cobrador,
    p.estadoprestamo
FROM prestamos p
JOIN clientes c ON p.clienteid = c.id
JOIN usuarios u ON p.cobradorid = u.id
WHERE u.nombre ILIKE '%Gabriel%'
ORDER BY p.id;

-- PASO 2: Contar cuántos se van a actualizar
SELECT 
    'PRÉSTAMOS A ACTUALIZAR' as msg,
    COUNT(*) as cantidad
FROM prestamos p
JOIN usuarios u ON p.cobradorid = u.id
WHERE u.nombre ILIKE '%Gabriel%';

-- ============================================================
-- PASO 3: ACTUALIZAR PORCENTAJE A 0
-- ============================================================
UPDATE prestamos
SET porcentajecobrador = 0
WHERE cobradorid IN (
    SELECT id FROM usuarios WHERE nombre ILIKE '%Gabriel%'
);

-- PASO 4: Verificar cambios
SELECT 
    p.id as prestamo_id,
    c.nombre as cliente,
    p.montoprestado,
    p.tasainteres,
    p.porcentajecobrador as nuevo_porcentaje,
    u.nombre as cobrador
FROM prestamos p
JOIN clientes c ON p.clienteid = c.id
JOIN usuarios u ON p.cobradorid = u.id
WHERE u.nombre ILIKE '%Gabriel%'
ORDER BY p.id;
