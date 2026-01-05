-- Script para actualizar fechas del préstamo ID 1
-- Fecha primera cuota: 2025-12-31
-- Fecha vencimiento: 2026-02-15
-- 4 cuotas quincenales

-- 1. Actualizar fechas del préstamo
UPDATE prestamos
SET 
    fechaprestamo = '2025-12-16',  -- 15 días antes de la primera cuota
    fechavencimiento = '2026-02-15'
WHERE id = 1;

-- 2. Actualizar fechas de las cuotas
-- Cuota 1: 2025-12-31
UPDATE cuotasprestamo
SET fechacobro = '2025-12-31'
WHERE prestamoid = 1 AND numerocuota = 1;

-- Cuota 2: 2026-01-15
UPDATE cuotasprestamo
SET fechacobro = '2026-01-15'
WHERE prestamoid = 1 AND numerocuota = 2;

-- Cuota 3: 2026-01-31
UPDATE cuotasprestamo
SET fechacobro = '2026-01-31'
WHERE prestamoid = 1 AND numerocuota = 3;

-- Cuota 4: 2026-02-15
UPDATE cuotasprestamo
SET fechacobro = '2026-02-15'
WHERE prestamoid = 1 AND numerocuota = 4;

-- Verificar los cambios
SELECT id, fechaprestamo, fechavencimiento FROM prestamos WHERE id = 1;
SELECT numerocuota, fechacobro, montocuota, estadocuota FROM cuotasprestamo WHERE prestamoid = 1 ORDER BY numerocuota;
