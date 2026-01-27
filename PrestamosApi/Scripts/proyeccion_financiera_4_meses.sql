/*
    Proyección Financiera a 4 meses (Corregido con VISTA)
    -----------------------------------------------------
    Nota: Se ha cambiado a una VISTA (VIEW) para que 'Vw_CapitalRecuperable' 
    pueda ser consultada posteriormente sin errores de referencia.
*/

-- 1. Crear la vista para encapsular el cálculo del capital base
CREATE OR REPLACE VIEW Vw_CapitalRecuperable AS
SELECT
    SUM(saldopendiente) AS TotalPorCobrar,
    COUNT(*) as CantidadCuotasPendientes
FROM
    cuotasprestamo
WHERE
    estadocuota <> 'Pagado';

-- 2. Ejecutar la proyección utilizando la vista creada
SELECT
    TotalPorCobrar as "Capital Base (Total por Cobrar)",
    
    -- Proyección a 4 meses: Capital * (1.08)^4
    ROUND(TotalPorCobrar * POWER(1.08, 4), 2) as "Proyección a 4 Meses (8% Mensual)",
    
    -- Ganancia Neta estimada
    ROUND((TotalPorCobrar * POWER(1.08, 4)) - TotalPorCobrar, 2) as "Interés Generado Estimado"
FROM
    Vw_CapitalRecuperable;

/*
    Ahora puedes ejecutar tus propias consultas abajo, por ejemplo:
    SELECT * FROM Vw_CapitalRecuperable;
*/
