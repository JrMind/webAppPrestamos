-- CONSULTA DE CAPITAL REAL EN LA CALLE (SALDO DE CAPITAL PENDIENTE)
-- Excluye intereses, excluye préstamos pagados/finalizados.
-- Resta el capital que ya ha sido amortizado en las cuotas.

SELECT 
    -- Capital Total
    SUM(
        CASE 
            WHEN p.EsCongelado = true THEN p.MontoPrestado -- En congelados, el monto bajará si hay abonos a capital
            ELSE (p.MontoPrestado - COALESCE(amortizado.CapitalPagado, 0))
        END
    ) as CapitalPendienteReal,
    
    COUNT(p.Id) as TotalPrestamosActivos

FROM Prestamos p
LEFT JOIN (
    -- Subconsulta para calcular cuánto capital se ha amortizado en cada préstamo normal
    SELECT 
        PrestamoId,
        SUM(
            CASE 
                -- Si la cuota está 100% pagada, tomamos todo su capital programado
                WHEN EstadoCuota = 'Pagada' THEN MontoCapital
                -- Si es pago parcial y cubre intereses, el resto va a capital (tope MontoCapital)
                WHEN MontoPagado > MontoInteres THEN LEAST(MontoCapital, MontoPagado - MontoInteres)
                ELSE 0 
            END
        ) as CapitalPagado
    FROM CuotasPrestamo
    GROUP BY PrestamoId
) amortizado ON p.Id = amortizado.PrestamoId

WHERE p.EstadoPrestamo = 'Activo';


-- DETALLE POR CLIENTE (Para verificar)
SELECT 
    c.Nombre,
    p.EsCongelado,
    p.MontoPrestado as MontoOriginal,
    COALESCE(amortizado.CapitalPagado, 0) as CapitalAmortizado,
    (CASE 
        WHEN p.EsCongelado = true THEN p.MontoPrestado
        ELSE (p.MontoPrestado - COALESCE(amortizado.CapitalPagado, 0))
     END) as SaldoCapitalPendiente
FROM Prestamos p
JOIN Clientes c ON p.ClienteId = c.Id
LEFT JOIN (
    SELECT 
        PrestamoId,
        SUM(
            CASE 
                WHEN EstadoCuota = 'Pagada' THEN MontoCapital
                WHEN MontoPagado > MontoInteres THEN LEAST(MontoCapital, MontoPagado - MontoInteres)
                ELSE 0 
            END
        ) as CapitalPagado
    FROM CuotasPrestamo
    GROUP BY PrestamoId
) amortizado ON p.Id = amortizado.PrestamoId
WHERE p.EstadoPrestamo = 'Activo'
ORDER BY SaldoCapitalPendiente DESC;
