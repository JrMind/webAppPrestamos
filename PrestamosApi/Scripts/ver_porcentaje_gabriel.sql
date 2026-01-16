-- ============================================================
-- Ver % de Gabriel como cobrador en todos los préstamos
-- ============================================================

-- 1. Préstamos donde Gabriel es cobrador
SELECT 
    p.id as prestamo_id,
    c.nombre as cliente,
    p.montoprestado,
    p.tasainteres as tasa_prestamo,
    p.porcentajecobrador as porcentaje_gabriel,
    p.estadoprestamo,
    CASE 
        WHEN p.porcentajecobrador = 0 THEN 'OK - 0%'
        ELSE 'TIENE ' || p.porcentajecobrador || '%'
    END as status
FROM prestamos p
JOIN clientes c ON p.clienteid = c.id
JOIN usuarios u ON p.cobradorid = u.id
WHERE u.nombre ILIKE '%Gabriel%'
ORDER BY p.porcentajecobrador DESC, p.id;

-- 2. Resumen
SELECT 
    'RESUMEN GABRIEL' as info,
    COUNT(*) as total_prestamos,
    SUM(CASE WHEN porcentajecobrador = 0 THEN 1 ELSE 0 END) as con_0_porciento,
    SUM(CASE WHEN porcentajecobrador > 0 THEN 1 ELSE 0 END) as con_porcentaje,
    SUM(montoprestado) as capital_total
FROM prestamos p
JOIN usuarios u ON p.cobradorid = u.id
WHERE u.nombre ILIKE '%Gabriel%';

-- 3. Si hay préstamos con % > 0, este UPDATE los corrige
-- UPDATE prestamos SET porcentajecobrador = 0 
-- WHERE cobradorid IN (SELECT id FROM usuarios WHERE nombre ILIKE '%Gabriel%');
