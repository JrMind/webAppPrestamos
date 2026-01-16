-- ============================================================
-- SCRIPT DE SINCRONIZACIÓN DE PAGOS
-- Objetivo: Crear registros en tabla Pagos para cuotas pagadas
-- que no tienen pago correspondiente
-- ============================================================

-- PASO 0: Verificar estado actual antes de cambios
SELECT 'ANTES DE CAMBIOS' as etapa,
    (SELECT COUNT(*) FROM "Pagos") as total_pagos,
    (SELECT SUM("MontoPago") FROM "Pagos") as suma_pagos,
    (SELECT COUNT(*) FROM "CuotasPrestamo" WHERE "EstadoCuota" = 'Pagada') as cuotas_pagadas,
    (SELECT SUM("MontoPagado") FROM "CuotasPrestamo") as suma_monto_pagado_cuotas;

-- ============================================================
-- PASO 1: CREAR TABLA DE BACKUP DE CUOTAS PAGADAS
-- ============================================================
DROP TABLE IF EXISTS "CuotasPagadasBackup";

CREATE TABLE "CuotasPagadasBackup" AS
SELECT 
    cp."Id" as cuota_id,
    cp."PrestamoId",
    cp."NumeroCuota",
    cp."FechaCobro",
    cp."MontoCuota",
    cp."MontoCapital",
    cp."MontoInteres",
    cp."MontoPagado",
    cp."SaldoPendiente",
    cp."EstadoCuota",
    cp."FechaPago",
    p."Id" as prestamo_record_id,
    c."Nombre" as cliente_nombre,
    c."Cedula" as cliente_cedula,
    NOW() as backup_timestamp
FROM "CuotasPrestamo" cp
JOIN "Prestamos" p ON cp."PrestamoId" = p."Id"
JOIN "Clientes" c ON p."ClienteId" = c."Id"
WHERE cp."MontoPagado" > 0 OR cp."EstadoCuota" = 'Pagada';

-- Verificar backup creado
SELECT 'BACKUP CREADO' as mensaje, COUNT(*) as registros_backup FROM "CuotasPagadasBackup";

-- ============================================================
-- PASO 2: CREAR TABLA DE BACKUP DE PAGOS ACTUALES
-- ============================================================
DROP TABLE IF EXISTS "PagosBackup";

CREATE TABLE "PagosBackup" AS
SELECT 
    pg."Id" as pago_id,
    pg."PrestamoId",
    pg."CuotaId",
    pg."MontoPago",
    pg."FechaPago",
    pg."MetodoPago",
    pg."Observaciones",
    c."Nombre" as cliente_nombre,
    NOW() as backup_timestamp
FROM "Pagos" pg
JOIN "Prestamos" p ON pg."PrestamoId" = p."Id"
JOIN "Clientes" c ON p."ClienteId" = c."Id";

-- Verificar backup de pagos
SELECT 'BACKUP PAGOS' as mensaje, COUNT(*) as registros_backup FROM "PagosBackup";

-- ============================================================
-- PASO 3: IDENTIFICAR CUOTAS PAGADAS SIN REGISTRO EN PAGOS
-- (Solo ver, no modificar todavía)
-- ============================================================
SELECT 'CUOTAS PAGADAS SIN PAGO CORRESPONDIENTE' as analisis;

SELECT 
    cp."Id" as cuota_id,
    cp."PrestamoId",
    c."Nombre" as cliente,
    cp."NumeroCuota",
    cp."MontoCuota",
    cp."MontoPagado",
    cp."EstadoCuota",
    cp."FechaPago" as fecha_pago_cuota,
    COALESCE(pg.suma_pagos, 0) as pagos_registrados_cuota,
    cp."MontoPagado" - COALESCE(pg.suma_pagos, 0) as diferencia
FROM "CuotasPrestamo" cp
JOIN "Prestamos" p ON cp."PrestamoId" = p."Id"
JOIN "Clientes" c ON p."ClienteId" = c."Id"
LEFT JOIN (
    SELECT "CuotaId", SUM("MontoPago") as suma_pagos
    FROM "Pagos"
    WHERE "CuotaId" IS NOT NULL
    GROUP BY "CuotaId"
) pg ON cp."Id" = pg."CuotaId"
WHERE cp."MontoPagado" > 0 
  AND (COALESCE(pg.suma_pagos, 0) < cp."MontoPagado" * 0.99)  -- Tolerancia 1%
ORDER BY diferencia DESC;

-- ============================================================
-- PASO 4: CONTAR CUÁNTOS PAGOS SE VAN A CREAR
-- ============================================================
SELECT 'PAGOS A CREAR' as mensaje,
    COUNT(*) as cantidad,
    SUM(cp."MontoPagado" - COALESCE(pg.suma_pagos, 0)) as monto_total
FROM "CuotasPrestamo" cp
LEFT JOIN (
    SELECT "CuotaId", SUM("MontoPago") as suma_pagos
    FROM "Pagos"
    WHERE "CuotaId" IS NOT NULL
    GROUP BY "CuotaId"
) pg ON cp."Id" = pg."CuotaId"
WHERE cp."MontoPagado" > 0 
  AND (COALESCE(pg.suma_pagos, 0) < cp."MontoPagado" * 0.99);

-- ============================================================
-- PASO 5: INSERTAR PAGOS FALTANTES
-- (DESCOMENTAR PARA EJECUTAR)
-- ============================================================
/*
INSERT INTO "Pagos" ("PrestamoId", "CuotaId", "MontoPago", "FechaPago", "MetodoPago", "Observaciones")
SELECT 
    cp."PrestamoId",
    cp."Id" as "CuotaId",
    cp."MontoPagado" - COALESCE(pg.suma_pagos, 0) as "MontoPago",
    COALESCE(cp."FechaPago", cp."FechaCobro") as "FechaPago",
    'Sincronizado' as "MetodoPago",
    'Pago sincronizado desde CuotasPrestamo - ' || NOW()::text as "Observaciones"
FROM "CuotasPrestamo" cp
LEFT JOIN (
    SELECT "CuotaId", SUM("MontoPago") as suma_pagos
    FROM "Pagos"
    WHERE "CuotaId" IS NOT NULL
    GROUP BY "CuotaId"
) pg ON cp."Id" = pg."CuotaId"
WHERE cp."MontoPagado" > 0 
  AND (COALESCE(pg.suma_pagos, 0) < cp."MontoPagado" * 0.99);
*/

-- ============================================================
-- PASO 6: VINCULAR PAGOS HUÉRFANOS A CUOTAS
-- (Solo ver primero)
-- ============================================================
SELECT 'PAGOS HUÉRFANOS (sin CuotaId)' as analisis;

SELECT 
    pg."Id" as pago_id,
    pg."PrestamoId",
    c."Nombre" as cliente,
    pg."MontoPago",
    pg."FechaPago",
    pg."Observaciones"
FROM "Pagos" pg
JOIN "Prestamos" p ON pg."PrestamoId" = p."Id"
JOIN "Clientes" c ON p."ClienteId" = c."Id"
WHERE pg."CuotaId" IS NULL;

-- ============================================================
-- PASO 7: VERIFICACIÓN FINAL DESPUÉS DE CAMBIOS
-- ============================================================
SELECT 'DESPUÉS DE CAMBIOS' as etapa,
    (SELECT COUNT(*) FROM "Pagos") as total_pagos,
    (SELECT SUM("MontoPago") FROM "Pagos") as suma_pagos,
    (SELECT COUNT(*) FROM "CuotasPrestamo" WHERE "EstadoCuota" = 'Pagada') as cuotas_pagadas,
    (SELECT SUM("MontoPagado") FROM "CuotasPrestamo") as suma_monto_pagado_cuotas;

-- Verificar discrepancia
SELECT 
    'DISCREPANCIA FINAL' as verificacion,
    (SELECT SUM("MontoPago") FROM "Pagos") as total_pagos,
    (SELECT SUM("MontoPagado") FROM "CuotasPrestamo") as total_cuotas,
    (SELECT SUM("MontoPago") FROM "Pagos") - (SELECT SUM("MontoPagado") FROM "CuotasPrestamo") as diferencia;
