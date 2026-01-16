-- Script para registrar capital de socios Jair y Jeisson
-- Esto balancea el sistema para mostrar $1.07M disponible correctamente

-- Total a registrar: $11,610,000 (dividido 50/50)
-- Jair (Administrador): $5,805,000
-- Jeisson Restrepo: $5,805,000

BEGIN;

-- 1. Registrar aporte inicial para Jair (Administrador)
INSERT INTO aportes (usuarioid, montoinicial, montoactual, fechaaporte, descripcion)
VALUES (
    1, 
    5805000.00, 
    5805000.00, 
    CURRENT_TIMESTAMP, 
    'Aporte inicial de capital - Balanceo del sistema'
);

-- 2. Registrar aporte inicial para Jeisson Restrepo
INSERT INTO aportes (usuarioid, montoinicial, montoactual, fechaaporte, descripcion)
VALUES (
    4, 
    5805000.00, 
    5805000.00, 
    CURRENT_TIMESTAMP, 
    'Aporte inicial de capital - Balanceo del sistema'
);

-- 3. Verificar totales
SELECT 
    'Verificación' as tipo,
    (SELECT SUM(montototalaportado) FROM aportadoresexternos WHERE estado = 'Activo') as capital_externos,
    (SELECT SUM(montoinicial) FROM aportes) as capital_socios,
    (SELECT SUM(montototalaportado) FROM aportadoresexternos WHERE estado = 'Activo') + 
    (SELECT SUM(montoinicial) FROM aportes) as total_capital;

COMMIT;

-- Después de ejecutar este script:
-- Total capital = $92.5M (externos) + $11.61M (socios) = $104.11M
-- Prestado activo: $101.9M
-- Disponible esperado: ~$1.07M (después de marcar la cuota de $500k)
