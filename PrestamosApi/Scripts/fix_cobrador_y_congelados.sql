-- ============================================================
-- FIX 1: Poner porcentajecobrador = 0 donde NO hay cobrador
-- ============================================================

-- Ver cuántos préstamos tienen % cobrador pero no tienen cobrador asignado
SELECT 
    'PRÉSTAMOS SIN COBRADOR CON % > 0' as msg,
    COUNT(*) as cantidad,
    SUM(porcentajecobrador) as suma_porcentajes
FROM prestamos 
WHERE cobradorid IS NULL AND porcentajecobrador > 0;

-- Ver detalle
SELECT 
    id,
    montoprestado,
    tasainteres,
    porcentajecobrador,
    estadoprestamo
FROM prestamos 
WHERE cobradorid IS NULL AND porcentajecobrador > 0;

-- EJECUTAR FIX
UPDATE prestamos 
SET porcentajecobrador = 0 
WHERE cobradorid IS NULL AND porcentajecobrador > 0;

-- Verificar
SELECT 
    'DESPUÉS DEL FIX' as msg,
    COUNT(*) as prestamos_sin_cobrador_con_porcentaje
FROM prestamos 
WHERE cobradorid IS NULL AND porcentajecobrador > 0;

-- ============================================================
-- FIX 2: Préstamos congelados - cuotas solo interés
-- ============================================================

-- Ver préstamos congelados
SELECT 
    'PRÉSTAMOS CONGELADOS' as msg,
    p.id,
    c.nombre as cliente,
    p.montoprestado,
    p.tasainteres,
    COUNT(cp.id) as total_cuotas
FROM prestamos p
JOIN clientes c ON p.clienteid = c.id
LEFT JOIN cuotasprestamo cp ON p.id = cp.prestamoid
WHERE p."EsCongelado" = true
GROUP BY p.id, c.nombre, p.montoprestado, p.tasainteres;

-- Ver cuotas de congelados con MontoCapital > 0 (incorrecto)
SELECT 
    'CUOTAS CONGELADAS CON CAPITAL > 0 (ERROR)' as msg,
    cp.id as cuota_id,
    cp.prestamoid,
    cp.numerocuota,
    cp.montocuota,
    cp."MontoCapital" as capital_actual,
    cp."MontoInteres" as interes_actual,
    cp.estadocuota
FROM cuotasprestamo cp
JOIN prestamos p ON cp.prestamoid = p.id
WHERE p."EsCongelado" = true 
  AND cp."MontoCapital" > 0;

-- EJECUTAR FIX: En préstamos congelados, todo el monto es interés
UPDATE cuotasprestamo cp
SET 
    "MontoInteres" = montocuota,
    "MontoCapital" = 0
FROM prestamos p
WHERE cp.prestamoid = p.id 
  AND p."EsCongelado" = true;

-- Verificar
SELECT 
    'DESPUÉS DEL FIX CONGELADOS' as msg,
    COUNT(*) as cuotas_corregidas
FROM cuotasprestamo cp
JOIN prestamos p ON cp.prestamoid = p.id
WHERE p."EsCongelado" = true 
  AND cp."MontoCapital" = 0 
  AND cp."MontoInteres" = cp.montocuota;
