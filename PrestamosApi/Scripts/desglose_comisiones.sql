-- DESGLOSE DE COMISIONES COBRADORES (PROYECTADO VS MENSUAL)

SELECT 
    p.id as prestamo_id,
    c.nombre as cobrador,
    p.montoprestado,
    p.montointereses as total_interes_prestamo,
    p.tasainteres as tasa_prestamo,
    p.porcentajecobrador,
    
    -- Fórmula Backend: (PorcentajeCobrador / TasaInteres) * MontoIntereses
    (p.porcentajecobrador / NULLIF(p.tasainteres, 0)) * p.montointereses as comision_total_proyectada,
    
    -- Estimación Mensual (Interés total / cuotas) * factor
    ((p.montointereses / NULLIF(p.numerocuotas, 0)) * (p.porcentajecobrador / NULLIF(p.tasainteres, 0))) as comision_mensual_aprox

FROM prestamos p
JOIN usuarios c ON p.cobradorid = c.id
WHERE p.estadoprestamo = 'Activo'
ORDER BY comision_total_proyectada DESC;

-- TOTALES
SELECT 
    SUM((p.porcentajecobrador / NULLIF(p.tasainteres, 0)) * p.montointereses) as gran_total_comisiones_proyectadas
FROM prestamos p
WHERE p.estadoprestamo = 'Activo' AND p.cobradorid IS NOT NULL;
