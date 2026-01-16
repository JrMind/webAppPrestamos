-- Verificar si hay fuentes "Interno" con UsuarioId específico (que irían a un solo socio)

SELECT 
    'FUENTES INTERNO CON USUARIOID' as verificacion,
    fcp.id,
    fcp.prestamoid,
    fcp.tipo,
    fcp.usuarioid,
    u.nombre as socio_especifico,
    fcp.montoaportado
FROM fuentescapitalprestamo fcp
LEFT JOIN usuarios u ON fcp.usuarioid = u.id
WHERE fcp.tipo = 'Interno' AND fcp.usuarioid IS NOT NULL;

-- Contar cuántos por socio
SELECT 
    'RESUMEN POR SOCIO' as verificacion,
    u.nombre,
    COUNT(*) as cantidad_prestamos,
    SUM(fcp.montoaportado) as total_aportado
FROM fuentescapitalprestamo fcp
JOIN usuarios u ON fcp.usuarioid = u.id
WHERE fcp.tipo = 'Interno' AND fcp.usuarioid IS NOT NULL
GROUP BY u.nombre
ORDER BY total_aportado DESC;
