-- Script para analizar el problema de la Reserva de Capital
-- Este script muestra los componentes de la fórmula actual y ayuda
-- a identificar el problema de doble conteo

-- 1. Capital de Socios (Aportes Iniciales)
SELECT 'Aportes Iniciales Socios' as concepto, COALESCE(SUM(montoinicial), 0) as monto
FROM aportes;

-- 2. Capital de Aportadores Externos
SELECT 'Capital Aportadores Externos' as concepto, COALESCE(SUM(montototalaportado), 0) as monto
FROM aportadoresexternos
WHERE estado = 'Activo';

-- 3. Capital Actual de Socios (incluye ganancias acumuladas)
SELECT 'CapitalActual de Socios' as concepto, COALESCE(SUM(capitalactual), 0) as monto
FROM usuarios
WHERE activo = true AND rol = 'Socio';

-- 4. Total Capital sin restar nada (suma de los 3 anteriores)
WITH capitales AS (
    SELECT COALESCE(SUM(montoinicial), 0) as aportes_socios
    FROM aportes
),
externos AS (
    SELECT COALESCE(SUM(montototalaportado), 0) as capital_externos
    FROM aportadoresexternos
    WHERE estado = 'Activo'
),
actual_socios AS (
    SELECT COALESCE(SUM(capitalactual), 0) as capital_actual
    FROM usuarios
    WHERE activo = true AND rol = 'Socio'
)
SELECT 'Total Capital (sin restar Capital en Calle)' as concepto,
       (capitales.aportes_socios + externos.capital_externos + actual_socios.capital_actual) as monto
FROM capitales, externos, actual_socios;

-- 5. Capital en préstamos activos (Capital en la Calle)
-- Calculado cuota por cuota según el saldo pendiente
WITH capital_en_calle AS (
    SELECT 
        c.id as cuota_id,
        c.saldopendiente,
        c.montocuota,
        c."MontoCapital",
        CASE 
            WHEN c.montocuota > 0 THEN (c."MontoCapital" / c.montocuota) * c.saldopendiente
            ELSE 0
        END as capital_pendiente
    FROM cuotasprestamo c
    INNER JOIN prestamos p ON c.prestamoid = p.id
    WHERE p.estadoprestamo = 'Activo'
)
SELECT 'Capital en Calle (en préstamos activos)' as concepto, 
       COALESCE(SUM(capital_pendiente), 0) as monto
FROM capital_en_calle;

-- 6. Cálculo de Reserva ACTUAL (con el bug)
-- Fórmula actual: Aportes + Externos + CapitalActual - CapitalEnCalle
WITH capitales AS (
    SELECT COALESCE(SUM(montoinicial), 0) as aportes_socios
    FROM aportes
),
externos AS (
    SELECT COALESCE(SUM(montototalaportado), 0) as capital_externos
    FROM aportadoresexternos
    WHERE estado = 'Activo'
),
actual_socios AS (
    SELECT COALESCE(SUM(capitalactual), 0) as capital_actual
    FROM usuarios
    WHERE activo = true AND rol = 'Socio'
),
capital_en_calle AS (
    SELECT 
        CASE 
            WHEN c.montocuota > 0 THEN (c."MontoCapital" / c.montocuota) * c.saldopendiente
            ELSE 0
        END as capital_pendiente
    FROM cuotasprestamo c
    INNER JOIN prestamos p ON c.prestamoid = p.id
    WHERE p.estadoprestamo = 'Activo'
)
SELECT 'Reserva Disponible (FÓRMULA ACTUAL - CON BUG)' as concepto,
       (capitales.aportes_socios + externos.capital_externos + actual_socios.capital_actual - 
        (SELECT COALESCE(SUM(capital_pendiente), 0) FROM capital_en_calle)) as monto
FROM capitales, externos, actual_socios;

-- 7. Análisis del problema: ¿Hay doble conteo?
-- Mostrar desglose de CapitalActual vs Aportes
SELECT 
    u.email,
    COALESCE(a.montoinicial, 0) as aporte_inicial,
    u.capitalactual as capital_actual,
    u.gananciasacumuladas as ganancias,
    (u.capitalactual - COALESCE(a.montoinicial, 0)) as diferencia
FROM usuarios u
LEFT JOIN aportes a ON u.id = a.usuarioid
WHERE u.activo = true AND u.rol = 'Socio'
ORDER BY u.email;

-- 8. Propuesta de fórmula CORRECTA
-- Solo contar el capital inicial + externos - capital en calle
-- (No sumar CapitalActual porque eso ya incluye las ganancias que no son capital físico)
WITH capitales AS (
    SELECT COALESCE(SUM(montoinicial), 0) as aportes_socios
    FROM aportes
),
externos AS (
    SELECT COALESCE(SUM(montototalaportado), 0) as capital_externos
    FROM aportadoresexternos
    WHERE estado = 'Activo'
),
capital_en_calle AS (
    SELECT 
        CASE 
            WHEN c.montocuota > 0 THEN (c."MontoCapital" / c.montocuota) * c.saldopendiente
            ELSE 0
        END as capital_pendiente
    FROM cuotasprestamo c
    INNER JOIN prestamos p ON c.prestamoid = p.id
    WHERE p.estadoprestamo = 'Activo'
)
SELECT 'Reserva Disponible (PROPUESTA CORRECTA)' as concepto,
       (capitales.aportes_socios + externos.capital_externos - 
        (SELECT COALESCE(SUM(capital_pendiente), 0) FROM capital_en_calle)) as monto
FROM capitales, externos;
