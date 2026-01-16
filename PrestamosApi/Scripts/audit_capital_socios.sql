-- ============================================================
-- AUDITORÍA DE CAPITAL DE SOCIOS (simplificado)
-- ============================================================

-- 1. APORTES INICIALES POR SOCIO
SELECT 
    'APORTES INICIALES' as seccion,
    u.nombre as socio,
    COALESCE(SUM(a.montoinicial), 0) as aporte_inicial
FROM usuarios u
LEFT JOIN aportes a ON u.id = a.usuarioid
WHERE u.rol = 'Socio'
GROUP BY u.id, u.nombre
ORDER BY u.nombre;

-- 2. CAPITAL ACTUAL EN TABLA USUARIOS
SELECT 
    'CAPITAL EN USUARIOS' as seccion,
    nombre,
    capitalactual,
    gananciasacumuladas
FROM usuarios
WHERE rol = 'Socio'
ORDER BY nombre;

-- 3. TOTAL COBRADO (PAGOS)
SELECT 
    'TOTAL COBRADO' as seccion,
    SUM(montopago) as total_cobrado,
    COUNT(*) as cantidad_pagos
FROM pagos;

-- 4. DESGLOSE: CAPITAL VS INTERÉS EN CUOTAS PAGADAS
SELECT 
    'DESGLOSE CUOTAS PAGADAS' as seccion,
    SUM(montopagado) as total_pagado,
    SUM("MontoCapital") as capital_recuperado,
    SUM("MontoInteres") as interes_generado,
    COUNT(*) as cuotas_pagadas
FROM cuotasprestamo
WHERE estadocuota = 'Pagada';

-- 5. RESUMEN POR SOCIO
SELECT 
    u.nombre as socio,
    COALESCE(SUM(a.montoinicial), 0) as aporte_inicial,
    u.capitalactual as capital_sistema,
    u.gananciasacumuladas as ganancias_sistema
FROM usuarios u
LEFT JOIN aportes a ON u.id = a.usuarioid
WHERE u.rol = 'Socio'
GROUP BY u.id, u.nombre, u.capitalactual, u.gananciasacumuladas
ORDER BY u.nombre;

-- 6. FUENTES DE CAPITAL POR TIPO (cuánto prestado de cada fuente)
SELECT 
    tipo,
    SUM(montoaportado) as total_prestado,
    COUNT(*) as cantidad_prestamos
FROM fuentescapitalprestamo
GROUP BY tipo;

-- 7. PRÉSTAMOS ACTIVOS Y SUS FUENTES
SELECT 
    p.id as prestamo_id,
    c.nombre as cliente,
    p.montoprestado,
    fcp.tipo as fuente_tipo,
    fcp.montoaportado
FROM prestamos p
JOIN clientes c ON p.clienteid = c.id
LEFT JOIN fuentescapitalprestamo fcp ON p.id = fcp.prestamoid
WHERE p.estadoprestamo = 'Activo'
ORDER BY p.id
LIMIT 30;
