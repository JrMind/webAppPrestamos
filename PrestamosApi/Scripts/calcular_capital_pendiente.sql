-- ============================================================
-- CÁLCULO COMPLETO: Capital + Interés (columnas separadas)
-- ============================================================

-- 1. RESUMEN CON CAPITAL E INTERÉS SEPARADOS
SELECT 
    'CUOTAS PAGADAS' as tipo,
    COUNT(*) as cantidad,
    SUM(montopagado) as total_cobrado,
    SUM("MontoCapital") as capital,
    SUM("MontoInteres") as interes
FROM cuotasprestamo
WHERE estadocuota = 'Pagada'

UNION ALL

SELECT 
    'CUOTAS PENDIENTES' as tipo,
    COUNT(*) as cantidad,
    SUM(saldopendiente) as total_por_cobrar,
    SUM("MontoCapital") as capital,
    SUM("MontoInteres") as interes
FROM cuotasprestamo
WHERE estadocuota != 'Pagada'

UNION ALL

SELECT 
    'TOTAL GENERAL' as tipo,
    COUNT(*) as cantidad,
    SUM(montocuota) as total_cuotas,
    SUM("MontoCapital") as capital,
    SUM("MontoInteres") as interes
FROM cuotasprestamo;

-- 2. VERIFICACIÓN CAPITAL E INTERÉS
SELECT 
    'VERIFICACIÓN' as info,
    (SELECT SUM(montoprestado) FROM prestamos WHERE estadoprestamo = 'Activo') as capital_prestamos_activos,
    (SELECT SUM("MontoCapital") FROM cuotasprestamo WHERE estadocuota = 'Pagada') as capital_recuperado,
    (SELECT SUM("MontoInteres") FROM cuotasprestamo WHERE estadocuota = 'Pagada') as interes_cobrado,
    (SELECT SUM("MontoCapital") FROM cuotasprestamo WHERE estadocuota != 'Pagada') as capital_pendiente,
    (SELECT SUM("MontoInteres") FROM cuotasprestamo WHERE estadocuota != 'Pagada') as interes_pendiente;
