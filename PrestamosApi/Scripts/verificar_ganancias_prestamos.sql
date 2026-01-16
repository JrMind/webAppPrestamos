-- ============================================================
-- VERIFICACIÓN: ¿El capital de ganancias viene de los préstamos?
-- Rastrear cada centavo desde cuotas pagadas hasta socios
-- ============================================================

-- 1. CUOTAS PAGADAS - ¿Cuánto interés se ha cobrado en TOTAL?
SELECT 
    'INTERÉS TOTAL COBRADO' as verificacion,
    SUM("MontoInteres") as interes_de_cuotas_pagadas
FROM cuotasprestamo
WHERE estadocuota = 'Pagada';

-- 2. INTERÉS POR PRÉSTAMO (detallado)
SELECT 
    p.id as prestamo_id,
    c.nombre as cliente,
    p.montoprestado,
    p.tasainteres,
    p.porcentajecobrador,
    cob.nombre as cobrador,
    SUM(cp."MontoInteres") as interes_cobrado,
    CASE 
        WHEN p.tasainteres > 0 AND p.cobradorid IS NOT NULL THEN 
            SUM(cp."MontoInteres") * (p.porcentajecobrador / p.tasainteres)
        ELSE 0 
    END as para_cobrador,
    CASE 
        WHEN p.tasainteres > 0 AND p.cobradorid IS NOT NULL THEN 
            SUM(cp."MontoInteres") - (SUM(cp."MontoInteres") * (p.porcentajecobrador / p.tasainteres))
        ELSE SUM(cp."MontoInteres")
    END as para_socios
FROM cuotasprestamo cp
JOIN prestamos p ON cp.prestamoid = p.id
JOIN clientes c ON p.clienteid = c.id
LEFT JOIN usuarios cob ON p.cobradorid = cob.id
WHERE cp.estadocuota = 'Pagada'
GROUP BY p.id, c.nombre, p.montoprestado, p.tasainteres, p.porcentajecobrador, cob.nombre
ORDER BY interes_cobrado DESC;

-- 3. TOTALES AGREGADOS
SELECT 
    'TOTALES' as verificacion,
    SUM(interes_cobrado) as total_interes,
    SUM(para_cobrador) as total_cobradores,
    SUM(para_socios) as total_para_socios,
    SUM(para_socios) / 3 as por_socio
FROM (
    SELECT 
        SUM(cp."MontoInteres") as interes_cobrado,
        CASE 
            WHEN p.tasainteres > 0 AND p.cobradorid IS NOT NULL THEN 
                SUM(cp."MontoInteres") * (p.porcentajecobrador / p.tasainteres)
            ELSE 0 
        END as para_cobrador,
        CASE 
            WHEN p.tasainteres > 0 AND p.cobradorid IS NOT NULL THEN 
                SUM(cp."MontoInteres") - (SUM(cp."MontoInteres") * (p.porcentajecobrador / p.tasainteres))
            ELSE SUM(cp."MontoInteres")
        END as para_socios
    FROM cuotasprestamo cp
    JOIN prestamos p ON cp.prestamoid = p.id
    WHERE cp.estadocuota = 'Pagada'
    GROUP BY p.id, p.tasainteres, p.porcentajecobrador, p.cobradorid
) sub;

-- 4. COMPARAR CON LO QUE TIENE EL SISTEMA
SELECT 
    'COMPARACIÓN' as verificacion,
    nombre,
    gananciasacumuladas as en_sistema
FROM usuarios
WHERE rol = 'Socio'
ORDER BY nombre;

-- 5. ¿COINCIDE?
WITH teorico AS (
    SELECT SUM(para_socios) / 3 as ganancia_teorica_por_socio
    FROM (
        SELECT 
            CASE 
                WHEN p.tasainteres > 0 AND p.cobradorid IS NOT NULL THEN 
                    SUM(cp."MontoInteres") - (SUM(cp."MontoInteres") * (p.porcentajecobrador / p.tasainteres))
                ELSE SUM(cp."MontoInteres")
            END as para_socios
        FROM cuotasprestamo cp
        JOIN prestamos p ON cp.prestamoid = p.id
        WHERE cp.estadocuota = 'Pagada'
        GROUP BY p.id, p.tasainteres, p.porcentajecobrador, p.cobradorid
    ) sub
)
SELECT 
    '¿COINCIDE?' as verificacion,
    u.nombre,
    u.gananciasacumuladas as en_sistema,
    t.ganancia_teorica_por_socio as teorico,
    u.gananciasacumuladas - t.ganancia_teorica_por_socio as diferencia
FROM usuarios u, teorico t
WHERE u.rol = 'Socio'
ORDER BY u.nombre;
