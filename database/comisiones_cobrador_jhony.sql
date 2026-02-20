-- ============================================================
-- COMISIÓN DEL COBRADOR "JHONY" POR CUOTAS PAGADAS
-- ============================================================
-- Lógica:
--   Cada préstamo tiene el campo "PorcentajeCobrador" que indica
--   el % de COMISIÓN que le corresponde al cobrador sobre cada
--   cuota cobrada.
--
--   Solo se consideran cuotas con EstadoCuota IN ('Pagada', 'Abonada')
--   (cuotas totalmente pagadas), y adicionalmente los abonos parciales
--   se muestran en una sección aparte.
--
--   Comisión por cuota = MontoPagado * PorcentajeCobrador / 100
-- ============================================================


-- ─────────────────────────────────────────────────────────────
-- 1. RESUMEN TOTAL: cuánto le corresponde a Jhony
-- ─────────────────────────────────────────────────────────────
SELECT
    u."Nombre"                                                      AS Cobrador,
    COUNT(DISTINCT p."Id")                                          AS TotalPrestamos,
    COUNT(cp."Id")                                                  AS TotalCuotasPagadas,
    ROUND(SUM(cp."MontoPagado"), 2)                                 AS TotalRecaudado,
    -- El % que le corresponde varía por préstamo, calculamos el promedio ponderado
    ROUND(
        SUM(cp."MontoPagado" * p."PorcentajeCobrador" / 100.0), 2
    )                                                               AS TotalComisionCobrador
FROM "Usuarios"        u
JOIN "Prestamos"       p  ON p."CobradorId" = u."Id"
JOIN "CuotasPrestamo"  cp ON cp."PrestamoId" = p."Id"
WHERE
    -- Solo cuotas completamente pagadas
    cp."EstadoCuota" IN ('Pagada', 'Abonada')
    -- Filtro por nombre del cobrador (insensible a mayúsculas)
    AND LOWER(u."Nombre") LIKE '%jhony%'
GROUP BY u."Id", u."Nombre";


-- ─────────────────────────────────────────────────────────────
-- 2. DETALLE POR PRÉSTAMO: qué cobró en cada préstamo
-- ─────────────────────────────────────────────────────────────
SELECT
    u."Nombre"                                                  AS Cobrador,
    p."Id"                                                      AS PrestamoId,
    cl."Nombre"                                                 AS Cliente,
    p."MontoPrestado",
    p."PorcentajeCobrador"                                      AS PctComision,
    COUNT(cp."Id")                                              AS CuotasPagadas,
    ROUND(SUM(cp."MontoPagado"), 2)                             AS MontoTotalPagado,
    ROUND(
        SUM(cp."MontoPagado" * p."PorcentajeCobrador" / 100.0), 2
    )                                                           AS ComisionPrestamo
FROM "Usuarios"        u
JOIN "Prestamos"       p  ON p."CobradorId" = u."Id"
JOIN "Clientes"        cl ON cl."Id" = p."ClienteId"
JOIN "CuotasPrestamo"  cp ON cp."PrestamoId" = p."Id"
WHERE
    cp."EstadoCuota" IN ('Pagada', 'Abonada')
    AND LOWER(u."Nombre") LIKE '%jhony%'
GROUP BY
    u."Id", u."Nombre",
    p."Id", cl."Nombre",
    p."MontoPrestado", p."PorcentajeCobrador"
ORDER BY p."Id";


-- ─────────────────────────────────────────────────────────────
-- 3. DETALLE POR CUOTA: cada cuota con su comisión individual
-- ─────────────────────────────────────────────────────────────
SELECT
    u."Nombre"                                              AS Cobrador,
    p."Id"                                                  AS PrestamoId,
    cl."Nombre"                                             AS Cliente,
    cp."NumeroCuota",
    cp."FechaPago",
    cp."EstadoCuota",
    ROUND(cp."MontoCuota",    2)                            AS MontoCuota,
    ROUND(cp."MontoPagado",   2)                            AS MontoPagado,
    ROUND(cp."MontoCapital",  2)                            AS MontoCapital,
    ROUND(cp."MontoInteres",  2)                            AS MontoInteres,
    p."PorcentajeCobrador"                                  AS PctComision,
    -- Comisión = lo efectivamente pagado × % cobrador
    ROUND(
        cp."MontoPagado" * p."PorcentajeCobrador" / 100.0, 2
    )                                                       AS ComisionCuota
FROM "Usuarios"        u
JOIN "Prestamos"       p  ON p."CobradorId" = u."Id"
JOIN "Clientes"        cl ON cl."Id" = p."ClienteId"
JOIN "CuotasPrestamo"  cp ON cp."PrestamoId" = p."Id"
WHERE
    cp."EstadoCuota" IN ('Pagada', 'Abonada')
    AND LOWER(u."Nombre") LIKE '%jhony%'
ORDER BY p."Id", cp."NumeroCuota";


-- ─────────────────────────────────────────────────────────────
-- 4. PAGOS PARCIALES (BONUS): cuotas con abono pero no completas
--    Se listan aparte para decisión del administrador
-- ─────────────────────────────────────────────────────────────
SELECT
    u."Nombre"                                              AS Cobrador,
    p."Id"                                                  AS PrestamoId,
    cl."Nombre"                                             AS Cliente,
    cp."NumeroCuota",
    cp."FechaPago",
    ROUND(cp."MontoCuota",  2)                              AS MontoCuota,
    ROUND(cp."MontoPagado", 2)                              AS MontoPagado,
    ROUND(cp."SaldoPendiente", 2)                           AS SaldoPendiente,
    p."PorcentajeCobrador"                                  AS PctComision,
    -- Comisión PROPORCIONAL sobre lo que sí cobró
    ROUND(
        cp."MontoPagado" * p."PorcentajeCobrador" / 100.0, 2
    )                                                       AS ComisionProporcional
FROM "Usuarios"        u
JOIN "Prestamos"       p  ON p."CobradorId" = u."Id"
JOIN "Clientes"        cl ON cl."Id" = p."ClienteId"
JOIN "CuotasPrestamo"  cp ON cp."PrestamoId" = p."Id"
WHERE
    cp."EstadoCuota" = 'Parcial'
    AND cp."MontoPagado" > 0
    AND LOWER(u."Nombre") LIKE '%jhony%'
ORDER BY p."Id", cp."NumeroCuota";


-- ─────────────────────────────────────────────────────────────
-- 5. GRAN TOTAL incluyendo parciales (si el admin decide pagarlos)
-- ─────────────────────────────────────────────────────────────
SELECT
    u."Nombre"                                              AS Cobrador,
    SUM(cp."MontoPagado")                                   AS TotalRecaudadoIncluyendoParciales,
    ROUND(
        SUM(cp."MontoPagado" * p."PorcentajeCobrador" / 100.0), 2
    )                                                       AS ComisionTotalIncluyendoParciales
FROM "Usuarios"        u
JOIN "Prestamos"       p  ON p."CobradorId" = u."Id"
JOIN "CuotasPrestamo"  cp ON cp."PrestamoId" = p."Id"
WHERE
    cp."EstadoCuota" IN ('Pagada', 'Abonada', 'Parcial')
    AND cp."MontoPagado" > 0
    AND LOWER(u."Nombre") LIKE '%jhony%'
GROUP BY u."Id", u."Nombre";
