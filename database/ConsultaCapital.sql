-- CONSULTA DE CAPITAL REAL EN LA CALLE (SALDO DE CAPITAL PENDIENTE)
-- Versión corregida: ¡Ahora usa las columnas correctas con comillas y mayúsculas!
-- "EsCongelado", "MontoCapital", "MontoInteres" deben ir entre comillas.

SELECT 
    -- Capital Pendiente Total = Suma(CapitalOriginal) - Suma(CapitalPagado)
    SUM(
        CASE 
            WHEN p."EsCongelado" = true THEN p.montoprestado -- En congelados, el capital no baja
            ELSE (
                p.montoprestado - (
                    -- Subconsulta para sumar el capital amortizado en cuotas PAGADAS
                    COALESCE((
                        SELECT SUM(c."MontoCapital") 
                        FROM cuotasprestamo c
                        WHERE c.prestamoid = p.id 
                          AND c.estadocuota IN ('Pagada', 'Abonada') -- Solo si está pagada o abonada
                    ), 0)
                    +
                    -- Para pagos PARCIALES, restamos lo pagado menos intereses (hasta tope capital)
                    COALESCE((
                        SELECT SUM(
                            CASE 
                                WHEN c.montopagado > c."MontoInteres" THEN LEAST(c."MontoCapital", c.montopagado - c."MontoInteres")
                                ELSE 0 
                            END
                        )
                        FROM cuotasprestamo c
                        WHERE c.prestamoid = p.id 
                          AND c.estadocuota = 'Parcial'
                    ), 0)
                )
            )
        END
    ) as CapitalPendienteReal,
    
    COUNT(p.id) as TotalPrestamosActivos

FROM prestamos p
WHERE p.estadoprestamo = 'Activo';


-- DETALLE POR CLIENTE (Para verificar)
SELECT 
    c.nombre,
    p."EsCongelado",
    p.montoprestado as MontoOriginal,
    -- Capital Amortizado (Pagado)
    COALESCE((
        SELECT SUM(
            CASE 
                WHEN cp.estadocuota IN ('Pagada', 'Abonada') THEN cp."MontoCapital"
                WHEN cp.estadocuota = 'Parcial' AND cp.montopagado > cp."MontoInteres" THEN LEAST(cp."MontoCapital", cp.montopagado - cp."MontoInteres")
                ELSE 0
            END
        )
        FROM cuotasprestamo cp 
        WHERE cp.prestamoid = p.id
    ), 0) as CapitalPagado,

    -- Saldo Capital Pendiente
    (CASE 
        WHEN p."EsCongelado" = true THEN p.montoprestado
        ELSE (
             p.montoprestado - COALESCE((
                SELECT SUM(
                    CASE 
                        WHEN cp.estadocuota IN ('Pagada', 'Abonada') THEN cp."MontoCapital"
                        WHEN cp.estadocuota = 'Parcial' AND cp.montopagado > cp."MontoInteres" THEN LEAST(cp."MontoCapital", cp.montopagado - cp."MontoInteres")
                        ELSE 0
                    END
                )
                FROM cuotasprestamo cp 
                WHERE cp.prestamoid = p.id
            ), 0)
        )
     END) as SaldoCapitalPendiente

FROM prestamos p
JOIN clientes c ON p.clienteid = c.id
WHERE p.estadoprestamo = 'Activo'
ORDER BY SaldoCapitalPendiente DESC;
