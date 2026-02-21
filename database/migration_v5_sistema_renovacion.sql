-- ============================================================
-- Migration v5: Cargos adicionales por préstamo
-- Sistema y Renovación (dinero aparte del préstamo)
-- ============================================================

-- Cuánto se cobra por concepto de Sistema al crear el préstamo
ALTER TABLE prestamos
    ADD COLUMN IF NOT EXISTS valorsistema     DECIMAL(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS sistemacobrado   BOOLEAN       NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS fechasistemacobrado  TIMESTAMP WITH TIME ZONE NULL;

-- Cuánto se cobra por concepto de Renovación al crear el préstamo
ALTER TABLE prestamos
    ADD COLUMN IF NOT EXISTS valorrenovacion      DECIMAL(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS renovacioncobrada    BOOLEAN       NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS fecharenovacioncobrada TIMESTAMP WITH TIME ZONE NULL;

-- ─── Verificación ───────────────────────────────────────────
SELECT
    id,
    valorsistema,
    sistemacobrado,
    fechasistemacobrado,
    valorrenovacion,
    renovacioncobrada,
    fecharenovacioncobrada
FROM prestamos
LIMIT 5;

-- ─── Totales acumulados (consulta rápida de trazabilidad) ───
SELECT
    COUNT(*)                                    AS TotalPrestamos,
    SUM(valorsistema)                           AS TotalSistemaFacturado,
    SUM(CASE WHEN sistemacobrado THEN valorsistema ELSE 0 END)   AS TotalSistemaCobrado,
    SUM(CASE WHEN NOT sistemacobrado THEN valorsistema ELSE 0 END) AS TotalSistemaXCobrar,
    SUM(valorrenovacion)                        AS TotalRenovacionFacturada,
    SUM(CASE WHEN renovacioncobrada THEN valorrenovacion ELSE 0 END) AS TotalRenovacionCobrada,
    SUM(CASE WHEN NOT renovacioncobrada THEN valorrenovacion ELSE 0 END) AS TotalRenovacionXCobrar
FROM prestamos
WHERE valorsistema > 0 OR valorrenovacion > 0;
