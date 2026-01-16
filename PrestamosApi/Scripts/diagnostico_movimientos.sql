-- DIAGNÓSTICO DE MOVIMIENTOS CAPITAL

-- 1. Ver qué valores reales tiene tipomovimiento
SELECT DISTINCT tipomovimiento FROM movimientoscapital;

-- 2. Ver las primeras 10 filas para confirmar fechas y formato
SELECT * FROM movimientoscapital ORDER BY fechamovimiento DESC LIMIT 10;

-- 3. Ver si hay usuarios
SELECT count(*) as total_usuarios FROM usuarios;

-- 4. Ver rango de fechas
SELECT MIN(fechamovimiento) as primera_fecha, MAX(fechamovimiento) as ultima_fecha FROM movimientoscapital;
