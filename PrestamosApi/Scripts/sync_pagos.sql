-- ============================================================
-- SCRIPT DE SINCRONIZACIÓN DE PAGOS
-- Nombres exactos de columnas según la DB
-- ============================================================

-- PASO 0: Verificar estado actual
SELECT 'ANTES DE CAMBIOS' as etapa,
    (SELECT COUNT(*) FROM pagos) as total_pagos,
    (SELECT SUM(montopago) FROM pagos) as suma_pagos,
    (SELECT COUNT(*) FROM cuotasprestamo WHERE estadocuota = 'Pagada') as cuotas_pagadas,
    (SELECT SUM(montopagado) FROM cuotasprestamo) as suma_monto_pagado;

-- ============================================================
-- PASO 1: BACKUP DE CUOTAS PAGADAS
-- ============================================================
DROP TABLE IF EXISTS cuotaspagadasbackup;

CREATE TABLE cuotaspagadasbackup AS
SELECT 
    cp.id as cuota_id,
    cp.prestamoid,
    cp.numerocuota,
    cp.fechacobro,
    cp.montocuota,
    cp."MontoCapital" as montocapital,
    cp."MontoInteres" as montointeres,
    cp.montopagado,
    cp.saldopendiente,
    cp.estadocuota,
    cp.fechapago,
    p.id as prestamo_record_id,
    c.nombre as cliente_nombre,
    c.cedula as cliente_cedula,
    NOW() as backup_timestamp
FROM cuotasprestamo cp
JOIN prestamos p ON cp.prestamoid = p.id
JOIN clientes c ON p.clienteid = c.id
WHERE cp.montopagado > 0 OR cp.estadocuota = 'Pagada';

SELECT 'BACKUP CUOTAS CREADO' as msg, COUNT(*) as registros FROM cuotaspagadasbackup;

-- ============================================================
-- PASO 2: BACKUP DE PAGOS ACTUALES
-- ============================================================
DROP TABLE IF EXISTS pagosbackup;

CREATE TABLE pagosbackup AS
SELECT 
    pg.id as pago_id,
    pg.prestamoid,
    pg.cuotaid,
    pg.montopago,
    pg.fechapago,
    pg.metodopago,
    pg.observaciones,
    c.nombre as cliente_nombre,
    NOW() as backup_timestamp
FROM pagos pg
JOIN prestamos p ON pg.prestamoid = p.id
JOIN clientes c ON p.clienteid = c.id;

SELECT 'BACKUP PAGOS CREADO' as msg, COUNT(*) as registros FROM pagosbackup;

-- ============================================================
-- PASO 3: CUOTAS PAGADAS SIN PAGO CORRESPONDIENTE
-- ============================================================
SELECT 
    cp.id as cuota_id,
    cp.prestamoid,
    c.nombre as cliente,
    cp.numerocuota as cuota,
    cp.montocuota as monto_cuota,
    cp.montopagado as monto_pagado,
    cp.estadocuota as estado,
    cp.fechapago as fecha_pago,
    COALESCE(pg.suma_pagos, 0) as pagos_en_tabla,
    cp.montopagado - COALESCE(pg.suma_pagos, 0) as diferencia
FROM cuotasprestamo cp
JOIN prestamos p ON cp.prestamoid = p.id
JOIN clientes c ON p.clienteid = c.id
LEFT JOIN (
    SELECT cuotaid, SUM(montopago) as suma_pagos
    FROM pagos
    WHERE cuotaid IS NOT NULL
    GROUP BY cuotaid
) pg ON cp.id = pg.cuotaid
WHERE cp.montopagado > 0 
  AND (COALESCE(pg.suma_pagos, 0) < cp.montopagado * 0.99)
ORDER BY diferencia DESC;

-- ============================================================
-- PASO 4: CONTAR PAGOS A CREAR
-- ============================================================
SELECT 'PAGOS A CREAR' as msg,
    COUNT(*) as cantidad,
    SUM(cp.montopagado - COALESCE(pg.suma_pagos, 0)) as monto_total
FROM cuotasprestamo cp
LEFT JOIN (
    SELECT cuotaid, SUM(montopago) as suma_pagos
    FROM pagos
    WHERE cuotaid IS NOT NULL
    GROUP BY cuotaid
) pg ON cp.id = pg.cuotaid
WHERE cp.montopagado > 0 
  AND (COALESCE(pg.suma_pagos, 0) < cp.montopagado * 0.99);

-- ============================================================
-- PASO 5: INSERTAR PAGOS FALTANTES
-- *** DESCOMENTAR PARA EJECUTAR ***
-- ============================================================
/*
INSERT INTO pagos (prestamoid, cuotaid, montopago, fechapago, metodopago, observaciones)
SELECT 
    cp.prestamoid,
    cp.id as cuotaid,
    cp.montopagado - COALESCE(pg.suma_pagos, 0) as montopago,
    COALESCE(cp.fechapago, cp.fechacobro) as fechapago,
    'Sincronizado' as metodopago,
    'Pago sincronizado desde CuotasPrestamo - ' || NOW()::text as observaciones
FROM cuotasprestamo cp
LEFT JOIN (
    SELECT cuotaid, SUM(montopago) as suma_pagos
    FROM pagos
    WHERE cuotaid IS NOT NULL
    GROUP BY cuotaid
) pg ON cp.id = pg.cuotaid
WHERE cp.montopagado > 0 
  AND (COALESCE(pg.suma_pagos, 0) < cp.montopagado * 0.99);
*/

-- ============================================================
-- PASO 6: PAGOS HUÉRFANOS
-- ============================================================
SELECT 
    pg.id as pago_id,
    pg.prestamoid,
    c.nombre as cliente,
    pg.montopago as monto,
    pg.fechapago as fecha,
    pg.observaciones as obs
FROM pagos pg
JOIN prestamos p ON pg.prestamoid = p.id
JOIN clientes c ON p.clienteid = c.id
WHERE pg.cuotaid IS NULL;

-- ============================================================
-- PASO 7: VERIFICACIÓN FINAL
-- ============================================================
SELECT 'DESPUÉS' as etapa,
    (SELECT COUNT(*) FROM pagos) as total_pagos,
    (SELECT SUM(montopago) FROM pagos) as suma_pagos,
    (SELECT COUNT(*) FROM cuotasprestamo WHERE estadocuota = 'Pagada') as cuotas_pagadas,
    (SELECT SUM(montopagado) FROM cuotasprestamo) as suma_cuotas;

SELECT 
    'DISCREPANCIA' as verificacion,
    (SELECT SUM(montopago) FROM pagos) as total_pagos,
    (SELECT SUM(montopagado) FROM cuotasprestamo) as total_cuotas,
    (SELECT SUM(montopago) FROM pagos) - (SELECT SUM(montopagado) FROM cuotasprestamo) as diferencia;
