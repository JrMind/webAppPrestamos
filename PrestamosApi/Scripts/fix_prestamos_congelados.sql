-- ============================================================
-- CORRECCIÓN DE PRÉSTAMOS CONGELADOS (preserva pagos)
-- ============================================================

-- PASO 1: Ver préstamos congelados y sus cuotas actuales
SELECT 
    p.id as prestamo_id,
    c.nombre as cliente,
    p.montoprestado,
    p.tasainteres,
    p.montocuota as cuota_actual,
    ROUND(p.montoprestado * p.tasainteres / 100, 0) as cuota_correcta,
    p.numerocuotas,
    COUNT(cp.id) as cuotas_existentes
FROM prestamos p
JOIN clientes c ON p.clienteid = c.id
LEFT JOIN cuotasprestamo cp ON p.id = cp.prestamoid
WHERE p."EsCongelado" = true
GROUP BY p.id, c.nombre, p.montoprestado, p.tasainteres, p.montocuota, p.numerocuotas;

-- PASO 2: Actualizar datos del préstamo (sin tocar cuotas aún)
UPDATE prestamos 
SET 
    numerocuotas = 1,
    montocuota = ROUND(montoprestado * tasainteres / 100, 0),
    montointereses = ROUND(montoprestado * tasainteres / 100, 0),
    montototal = montoprestado
WHERE "EsCongelado" = true;

-- PASO 3: Identificar cuotas ya pagadas (no tocar)
SELECT 
    'CUOTAS CON PAGOS' as info,
    cp.id as cuota_id,
    cp.prestamoid,
    cp.numerocuota,
    cp.montocuota,
    cp.montopagado,
    cp.estadocuota
FROM cuotasprestamo cp
JOIN prestamos p ON cp.prestamoid = p.id
WHERE p."EsCongelado" = true
  AND cp.montopagado > 0;

-- PASO 4: Actualizar cuotas NO pagadas de préstamos congelados
-- Convertir montos a solo interés
UPDATE cuotasprestamo cp
SET 
    montocuota = ROUND(p.montoprestado * p.tasainteres / 100, 0),
    "MontoCapital" = 0,
    "MontoInteres" = ROUND(p.montoprestado * p.tasainteres / 100, 0),
    saldopendiente = ROUND(p.montoprestado * p.tasainteres / 100, 0)
FROM prestamos p
WHERE cp.prestamoid = p.id
  AND p."EsCongelado" = true
  AND cp.montopagado = 0
  AND cp.estadocuota = 'Pendiente';

-- PASO 5: Verificar resultado
SELECT 
    'DESPUÉS DE CORRECCIÓN' as info,
    p.id as prestamo_id,
    c.nombre as cliente,
    p.montoprestado,
    p.tasainteres,
    p.montocuota as cuota_prestamo,
    cp.id as cuota_id,
    cp.numerocuota,
    cp.montocuota as monto_cuota,
    cp."MontoCapital" as capital,
    cp."MontoInteres" as interes,
    cp.estadocuota
FROM prestamos p
JOIN clientes c ON p.clienteid = c.id
LEFT JOIN cuotasprestamo cp ON p.id = cp.prestamoid
WHERE p."EsCongelado" = true
ORDER BY p.id, cp.numerocuota;
