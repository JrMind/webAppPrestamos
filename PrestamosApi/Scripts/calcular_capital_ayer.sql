-- ============================================================
-- CÁLCULO DE CAPITAL HISTÓRICO (al 15/01/2026 12:00 PM)
-- ============================================================
-- Nota: 12:00 PM hora local (-5) = 17:00 PM UTC

SELECT 
    'CAPITAL AL 15/01/2026 12:00 PM' as corte,
    
    -- Aportes
    SUM(CASE WHEN tipomovimiento = 'Aporte' THEN monto ELSE 0 END) as total_aportado,
    
    -- Retiros
    SUM(CASE WHEN tipomovimiento = 'Retiro' THEN monto ELSE 0 END) as total_retirado,
    
    -- Capital Inicial Neto (Aportado - Retirado)
    (SUM(CASE WHEN tipomovimiento = 'Aporte' THEN monto ELSE 0 END) - 
     SUM(CASE WHEN tipomovimiento = 'Retiro' THEN monto ELSE 0 END)) as capital_inicial_neto,

    -- Interés Compuesto Generado
    SUM(CASE WHEN tipomovimiento = 'InteresGenerado' THEN monto ELSE 0 END) as interes_generado,
    
    -- Capital Total (Aportado - Retirado + Interés)
    SUM(CASE 
        WHEN tipomovimiento = 'Aporte' THEN monto 
        WHEN tipomovimiento = 'Retiro' THEN -monto 
        WHEN tipomovimiento = 'InteresGenerado' THEN monto 
        ELSE 0 
    END) as capital_total_acumulado

FROM movimientoscapital
WHERE fechamovimiento <= '2026-01-15 17:00:00'; -- UTC

-- DESGLOSE POR SOCIO
SELECT 
    u.nombre,
    
    -- Capital Inicial Neto
    (SUM(CASE WHEN mc.tipomovimiento = 'Aporte' THEN mc.monto ELSE 0 END) - 
     SUM(CASE WHEN mc.tipomovimiento = 'Retiro' THEN mc.monto ELSE 0 END)) as capital_inicial,
     
    -- Capital Total
    SUM(CASE 
        WHEN mc.tipomovimiento = 'Aporte' THEN mc.monto 
        WHEN mc.tipomovimiento = 'Retiro' THEN -mc.monto 
        WHEN mc.tipomovimiento = 'InteresGenerado' THEN mc.monto 
        ELSE 0 
    END) as capital_total

FROM movimientoscapital mc
JOIN usuarios u ON mc.usuarioid = u.id
WHERE mc.fechamovimiento <= '2026-01-15 17:00:00'
GROUP BY u.nombre;
