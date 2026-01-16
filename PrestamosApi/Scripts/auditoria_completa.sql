-- ============================================================
-- AUDITORÍA COMPLETA: ¿De dónde salen las ganancias?
-- Verificar que todo coincida
-- ============================================================

-- 1. TOTAL COBRADO (tabla pagos)
SELECT 
    '1. TOTAL COBRADO' as seccion,
    COUNT(*) as cantidad_pagos,
    SUM(montopago) as total_cobrado
FROM pagos;

-- 2. DESGLOSE DE CUOTAS PAGADAS (capital vs interés)
SELECT 
    '2. DESGLOSE CUOTAS' as seccion,
    COUNT(*) as cuotas_pagadas,
    SUM(montocuota) as monto_cuotas,
    SUM("MontoCapital") as capital_total,
    SUM("MontoInteres") as interes_total
FROM cuotasprestamo
WHERE estadocuota = 'Pagada';

-- 3. INTERÉS DISTRIBUIDO A COBRADORES
SELECT 
    '3. PARA COBRADORES' as seccion,
    SUM(
        CASE 
            WHEN p.tasainteres > 0 AND p.cobradorid IS NOT NULL THEN 
                cp."MontoInteres" * (p.porcentajecobrador / p.tasainteres)
            ELSE 0 
        END
    ) as interes_cobradores
FROM cuotasprestamo cp
JOIN prestamos p ON cp.prestamoid = p.id
WHERE cp.estadocuota = 'Pagada';

-- 4. GASTO APORTADORES (3% mensual - esto es un gasto fijo mensual, NO por cuota)
SELECT 
    '4. GASTO APORTADORES' as seccion,
    SUM(montototalaportado) as capital_externos,
    SUM(montototalaportado * 0.03) as gasto_mensual
FROM aportadoresexternos
WHERE estado = 'Activo';

-- 5. INTERÉS NETO PARA SOCIOS
WITH calculo AS (
    SELECT 
        (SELECT SUM("MontoInteres") FROM cuotasprestamo WHERE estadocuota = 'Pagada') as interes_total,
        (SELECT SUM(
            CASE 
                WHEN p.tasainteres > 0 AND p.cobradorid IS NOT NULL THEN 
                    cp."MontoInteres" * (p.porcentajecobrador / p.tasainteres)
                ELSE 0 
            END
        ) FROM cuotasprestamo cp JOIN prestamos p ON cp.prestamoid = p.id WHERE cp.estadocuota = 'Pagada') as para_cobradores
)
SELECT 
    '5. INTERÉS NETO SOCIOS' as seccion,
    interes_total,
    para_cobradores,
    interes_total - para_cobradores as interes_neto_socios,
    (interes_total - para_cobradores) / 3.0 as por_socio_teorico
FROM calculo;

-- 6. LO QUE TIENE CADA SOCIO EN EL SISTEMA
SELECT 
    '6. EN SISTEMA' as seccion,
    nombre,
    capitalactual,
    gananciasacumuladas
FROM usuarios
WHERE rol = 'Socio'
ORDER BY nombre;

-- 7. VER SI HAY DISTRIBUCIONES (si existe la tabla)
SELECT 
    '7. TABLA DISTRIBUCIONES' as seccion,
    EXISTS (
        SELECT 1 FROM information_schema.tables 
        WHERE table_name = 'distribucionesganancia'
    ) as existe_tabla;

-- 8. BUSCAR POR QUÉ ADMIN TIENE MÁS
-- Ver si Admin tiene algo especial que los otros no
SELECT 
    '8. COMPARACIÓN ADMIN VS OTROS' as seccion,
    nombre,
    capitalactual,
    gananciasacumuladas,
    porcentajeparticipacion
FROM usuarios
WHERE rol = 'Socio'
ORDER BY gananciasacumuladas DESC;

-- 9. VERIFICAR APORTES DE CADA SOCIO
SELECT 
    '9. APORTES POR SOCIO' as seccion,
    u.nombre,
    COALESCE(SUM(a.montoinicial), 0) as aporte_inicial
FROM usuarios u
LEFT JOIN aportes a ON u.id = a.usuarioid
WHERE u.rol = 'Socio'
GROUP BY u.id, u.nombre
ORDER BY u.nombre;
