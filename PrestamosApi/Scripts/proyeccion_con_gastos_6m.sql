/*
    Proyección Financiera a 4 meses con DEDUCCIÓN MENSUAL
    -----------------------------------------------------
    Objetivo: Calcular valor futuro con:
      - Capital Base: Suma de 'saldopendiente' (aprox 132M)
      - Tasa: 8% mensual
      - Gasto Mensual: 6,000,000 (se resta CADA mes antes de reinvertir o después del rendimiento)
    
    Lógica de flujo de caja:
    Mes 1 = (Capital * 1.08) - 6,000,000
    Mes 2 = (Mes1 * 1.08) - 6,000,000
    ...
*/

WITH CapitalInicial AS (
    -- Usamos la vista creada anteriormente o recalculamos
    SELECT SUM(saldopendiente) AS CapitalBase FROM cuotasprestamo WHERE estadocuota <> 'Pagado'
),
CalculoMes1 AS (
    SELECT CapitalBase, ((CapitalBase * 1.08) - 6000000) as SaldoMes1
    FROM CapitalInicial
),
CalculoMes2 AS (
    SELECT SaldoMes1, ((SaldoMes1 * 1.08) - 6000000) as SaldoMes2
    FROM CalculoMes1
),
CalculoMes3 AS (
    SELECT SaldoMes2, ((SaldoMes2 * 1.08) - 6000000) as SaldoMes3
    FROM CalculoMes2
),
CalculoMes4 AS (
    SELECT SaldoMes3, ((SaldoMes3 * 1.08) - 6000000) as SaldoMes4
    FROM CalculoMes3
)
SELECT 
    ROUND(CalculoMes1.CapitalBase, 2) as "Capital Inicial",
    ROUND(SaldoMes1, 2) as "Mes 1 (Invierte - 6M)",
    ROUND(SaldoMes2, 2) as "Mes 2 (Invierte - 6M)",
    ROUND(SaldoMes3, 2) as "Mes 3 (Invierte - 6M)",
    ROUND(SaldoMes4, 2) as "Mes 4 (Final)",
    
    -- Comparación con la proyección simple (sin gastos)
    ROUND(CalculoMes1.CapitalBase * POWER(1.08, 4), 2) as "Sin Gastos (Referencia)",
    ROUND((CalculoMes1.CapitalBase * POWER(1.08, 4)) - SaldoMes4, 2) as "Diferencia (Costo Total de los 6M Mensuales)"
FROM 
    CalculoMes4
    CROSS JOIN CalculoMes1;
