-- CONSULTA DE CAPITAL REAL EN LA CALLE (SALDO DE CAPITAL PENDIENTE)
-- Versión corregida: Usa cálculo proporcional porque la columna 'MontoCapital' no existe en la BD.
-- Fórmula: CapitalPendiente = MontoPrestado - (TotalPagado * (MontoPrestado / MontoTotal))

SELECT 
    -- Capital Total
    SUM(
        CASE 
            WHEN p.EsCongelado = true THEN p.MontoPrestado -- En congelados, el monto bajará si hay abonos a capital
            ELSE (p.MontoPrestado - (
                -- Total pagado en cuotas * Proporción de Capital
                COALESCE((SELECT SUM(MontoPagado) FROM CuotasPrestamo WHERE PrestamoId = p.Id), 0) * 
                (p.MontoPrestado / NULLIF(p.MontoTotal, 0))
            ))
        END
    ) as CapitalPendienteReal,
    
    COUNT(p.Id) as TotalPrestamosActivos

FROM Prestamos p
WHERE p.EstadoPrestamo = 'Activo';


-- DETALLE POR CLIENTE (Para verificar)
SELECT 
    c.Nombre,
    p.EsCongelado,
    p.MontoPrestado as MontoOriginal,
    p.MontoTotal as DeudaTotal,
    COALESCE((SELECT SUM(MontoPagado) FROM CuotasPrestamo WHERE PrestamoId = p.Id), 0) as TotalPagado,
    (CASE 
        WHEN p.EsCongelado = true THEN p.MontoPrestado
        ELSE (p.MontoPrestado - (
            COALESCE((SELECT SUM(MontoPagado) FROM CuotasPrestamo WHERE PrestamoId = p.Id), 0) * 
            (p.MontoPrestado / NULLIF(p.MontoTotal, 0))
        ))
     END) as SaldoCapitalPendiente
FROM Prestamos p
JOIN Clientes c ON p.ClienteId = c.Id
WHERE p.EstadoPrestamo = 'Activo'
ORDER BY SaldoCapitalPendiente DESC;
