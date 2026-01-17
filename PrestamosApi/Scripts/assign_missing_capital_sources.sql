-- Script para asignar fuentes de capital a préstamos que no tienen ninguna
-- Esto es necesario para que funcione correctamente la distribución de ganancias

-- PASO 1: Ver préstamos sin fuentes de capital
SELECT 
    p.id,
    p.montoprestado,
    p.fechaprestamo,
    p.estadoprestamo,
    c.nombre as cliente
FROM prestamos p
INNER JOIN clientes c ON c.id = p.clienteid
LEFT JOIN fuentescapitalprestamo f ON f.prestamoid = p.id
WHERE f.id IS NULL
ORDER BY p.fechaprestamo DESC;

-- PASO 2: Asignar fuente "Reserva" a préstamos sin fuentes
-- Esto permite que las ganancias se distribuyan equitativamente entre los socios
INSERT INTO fuentescapitalprestamo (prestamoid, tipo, usuarioid, aportadorexternoid, montoaportado, fecharegistro)
SELECT 
    p.id,
    'Reserva' as tipo,
    NULL as usuarioid,
    NULL as aportadorexternoid,
    p.montoprestado,
    p.fechaprestamo
FROM prestamos p
LEFT JOIN fuentescapitalprestamo f ON f.prestamoid = p.id
WHERE f.id IS NULL;

-- PASO 3: Verificar que todos los préstamos ahora tienen fuentes
SELECT 
    COUNT(*) as prestamos_sin_fuentes
FROM prestamos p
LEFT JOIN fuentescapitalprestamo f ON f.prestamoid = p.id
WHERE f.id IS NULL;
-- Debería retornar 0

-- NOTA: Si algunos préstamos específicos deberían tener fuente "Externo"
-- en lugar de "Reserva", pueden ser actualizados manualmente después:
-- 
-- UPDATE fuentescapitalprestamo 
-- SET tipo = 'Externo', aportadorexternoid = <ID_APORTADOR>
-- WHERE prestamoid = <ID_PRESTAMO> AND tipo = 'Reserva';
