-- ============================================================
-- CÁLCULO DE GANANCIAS CORRECTAS PARA SOCIOS
-- Fórmula: Interés cobrado - % Cobrador = Ganancia Socios
-- ============================================================

-- 1. INTERÉS TOTAL COBRADO (de cuotas pagadas)
SELECT 
    'INTERÉS COBRADO' as seccion,
    SUM("MontoInteres") as interes_total
FROM cuotasprestamo
WHERE estadocuota = 'Pagada';

-- 2. GANANCIA DE COBRADORES (de cuotas pagadas)
-- Para cada cuota pagada, calcular cuánto fue para el cobrador
SELECT 
    'GANANCIA COBRADORES' as seccion,
    SUM(
        CASE 
            WHEN p.tasainteres > 0 THEN 
                cp."MontoInteres" * (p.porcentajecobrador / p.tasainteres)
            ELSE 0 
        END
    ) as ganancia_cobradores
FROM cuotasprestamo cp
JOIN prestamos p ON cp.prestamoid = p.id
WHERE cp.estadocuota = 'Pagada';

-- 3. GASTO MENSUAL APORTADORES (3% mensual de su capital)
SELECT 
    'GASTO APORTADORES MENSUAL' as seccion,
    SUM(montototalaportado) as capital_aportadores,
    SUM(montototalaportado * 0.03) as gasto_mensual_3_porciento
FROM aportadoresexternos
WHERE estado = 'Activo';

-- 4. CÁLCULO NETO PARA SOCIOS
WITH datos AS (
    SELECT 
        (SELECT SUM("MontoInteres") FROM cuotasprestamo WHERE estadocuota = 'Pagada') as interes_total,
        (SELECT SUM(
            CASE 
                WHEN p.tasainteres > 0 THEN 
                    cp."MontoInteres" * (p.porcentajecobrador / p.tasainteres)
                ELSE 0 
            END
        ) FROM cuotasprestamo cp JOIN prestamos p ON cp.prestamoid = p.id WHERE cp.estadocuota = 'Pagada') as ganancia_cobradores,
        (SELECT SUM(montototalaportado * 0.03) FROM aportadoresexternos WHERE estado = 'Activo') as gasto_aportadores_mes
)
SELECT 
    'GANANCIA NETA SOCIOS' as seccion,
    interes_total,
    ganancia_cobradores,
    gasto_aportadores_mes,
    (interes_total - ganancia_cobradores - gasto_aportadores_mes) as ganancia_neta_socios,
    (interes_total - ganancia_cobradores - gasto_aportadores_mes) / 3 as ganancia_por_socio
FROM datos;

-- 5. COMPARAR CON LO QUE TIENE EL SISTEMA
SELECT 
    'CAPITAL ACTUAL EN SISTEMA' as seccion,
    nombre,
    capitalactual,
    gananciasacumuladas
FROM usuarios
WHERE rol = 'Socio';

-- 6. DETALLE: Ganancia cobrador por préstamo (para verificar)
SELECT 
    p.id as prestamo_id,
    c.nombre as cliente,
    p.tasainteres as tasa_prestamo,
    p.porcentajecobrador as porcent_cobrador,
    u.nombre as cobrador,
    SUM(cp."MontoInteres") as interes_cobrado,
    SUM(
        CASE 
            WHEN p.tasainteres > 0 THEN 
                cp."MontoInteres" * (p.porcentajecobrador / p.tasainteres)
            ELSE 0 
        END
    ) as para_cobrador,
    SUM(cp."MontoInteres") - SUM(
        CASE 
            WHEN p.tasainteres > 0 THEN 
                cp."MontoInteres" * (p.porcentajecobrador / p.tasainteres)
            ELSE 0 
        END
    ) as para_socios
FROM cuotasprestamo cp
JOIN prestamos p ON cp.prestamoid = p.id
JOIN clientes c ON p.clienteid = c.id
LEFT JOIN usuarios u ON p.cobradorid = u.id
WHERE cp.estadocuota = 'Pagada'
GROUP BY p.id, c.nombre, p.tasainteres, p.porcentajecobrador, u.nombre
ORDER BY interes_cobrado DESC;
