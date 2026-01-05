-- ============================================
-- SCRIPTS SQL PARA SISTEMA DE CAPITAL Y APORTADORES
-- PostgreSQL - Ejecutar en orden
-- ============================================

-- 1. TABLA: AportadoresExternos
-- Para gestionar aportadores externos que prestan dinero al negocio
CREATE TABLE IF NOT EXISTS aportadoresexternos (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(255) NOT NULL,
    telefono VARCHAR(50),
    email VARCHAR(255),
    tasainteres DECIMAL(5,2) NOT NULL DEFAULT 3, -- % que cobra el aportador
    diasparapago INTEGER NOT NULL DEFAULT 30, -- cada cuántos días pagar
    montototalaportado DECIMAL(18,2) NOT NULL DEFAULT 0,
    montopagado DECIMAL(18,2) NOT NULL DEFAULT 0,
    saldopendiente DECIMAL(18,2) NOT NULL DEFAULT 0,
    estado VARCHAR(50) NOT NULL DEFAULT 'Activo', -- Activo, Pagado
    fechacreacion TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    notas TEXT
);

-- 2. TABLA: FuentesCapitalPrestamo
-- Vincula préstamos con sus fuentes de financiamiento (múltiples fuentes por préstamo)
CREATE TABLE IF NOT EXISTS fuentescapitalprestamo (
    id SERIAL PRIMARY KEY,
    prestamoid INTEGER NOT NULL REFERENCES prestamos(id) ON DELETE CASCADE,
    tipo VARCHAR(50) NOT NULL, -- 'Reserva', 'Interno', 'Externo'
    usuarioid INTEGER REFERENCES usuarios(id), -- Solo para tipo 'Interno' (socio)
    aportadorexternoid INTEGER REFERENCES aportadoresexternos(id), -- Solo para tipo 'Externo'
    montoaportado DECIMAL(18,2) NOT NULL,
    porcentajeparticipacion DECIMAL(5,2) NOT NULL DEFAULT 0, -- % de las ganancias de este préstamo
    fecharegistro TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Índice para búsquedas rápidas
CREATE INDEX IF NOT EXISTS idx_fuentescapital_prestamo ON fuentescapitalprestamo(prestamoid);
CREATE INDEX IF NOT EXISTS idx_fuentescapital_usuario ON fuentescapitalprestamo(usuarioid);
CREATE INDEX IF NOT EXISTS idx_fuentescapital_aportador ON fuentescapitalprestamo(aportadorexternoid);

-- 3. AGREGAR CAMPOS A USUARIOS para mejor tracking de socios
DO $$ 
BEGIN
    -- Capital actual con interés compuesto acumulado
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'usuarios' AND column_name = 'capitalactual') THEN
        ALTER TABLE usuarios ADD COLUMN capitalactual DECIMAL(18,2) NOT NULL DEFAULT 0;
    END IF;
    
    -- Total de ganancias acumuladas por participación en préstamos
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'usuarios' AND column_name = 'gananciasacumuladas') THEN
        ALTER TABLE usuarios ADD COLUMN gananciasacumuladas DECIMAL(18,2) NOT NULL DEFAULT 0;
    END IF;
    
    -- Fecha del último cálculo de interés compuesto
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'usuarios' AND column_name = 'ultimocalculointeres') THEN
        ALTER TABLE usuarios ADD COLUMN ultimocalculointeres TIMESTAMP WITH TIME ZONE;
    END IF;
END $$;

-- 4. TABLA: PagosAportadoresExternos
-- Historial de pagos realizados a aportadores externos
CREATE TABLE IF NOT EXISTS pagosaportadoresexternos (
    id SERIAL PRIMARY KEY,
    aportadorexternoid INTEGER NOT NULL REFERENCES aportadoresexternos(id) ON DELETE CASCADE,
    monto DECIMAL(18,2) NOT NULL,
    montocapital DECIMAL(18,2) NOT NULL DEFAULT 0, -- Porción que reduce la deuda
    montointereses DECIMAL(18,2) NOT NULL DEFAULT 0, -- Porción de intereses
    fechapago TIMESTAMP WITH TIME ZONE NOT NULL,
    metodopago VARCHAR(50),
    comprobante VARCHAR(255),
    notas TEXT
);

CREATE INDEX IF NOT EXISTS idx_pagosaportadores_aportador ON pagosaportadoresexternos(aportadorexternoid);

-- 5. VISTA: Balance de Socios (para consultas rápidas)
CREATE OR REPLACE VIEW vw_balance_socios AS
SELECT 
    u.id,
    u.nombre,
    u.rol,
    u.tasainteresmensual,
    u.porcentajeparticipacion,
    COALESCE(SUM(a.montoinicial), 0) as capital_aportado,
    COALESCE(u.capitalactual, 0) as capital_actual,
    COALESCE(u.gananciasacumuladas, 0) as ganancias_acumuladas,
    COALESCE(u.capitalactual, 0) + COALESCE(u.gananciasacumuladas, 0) as saldo_total,
    u.ultimocalculointeres
FROM usuarios u
LEFT JOIN aportes a ON u.id = a.usuarioid
WHERE u.rol IN ('Admin', 'Socio', 'AportadorInterno')
GROUP BY u.id;

-- ============================================
-- NOTAS DE USO:
-- ============================================
-- 
-- Al crear un préstamo:
--   1. Registrar en fuentescapitalprestamo con tipo='Reserva', 'Interno' o 'Externo'
--   2. Si es 'Interno': usuarioid = ID del socio
--   3. Si es 'Externo': aportadorexternoid = ID del aportador, actualizar su montototalaportado
--
-- Al cobrar una cuota:
--   1. Calcular la distribución de intereses según las fuentes del préstamo
--   2. Actualizar gananciasacumuladas de cada socio
--   3. Si hay aportador externo, el capital de la cuota reduce su saldopendiente
--
-- Job mensual (interés compuesto):
--   UPDATE usuarios 
--   SET capitalactual = capitalactual * (1 + tasainteresmensual / 100),
--       ultimocalculointeres = NOW()
--   WHERE rol IN (0, 1, 2);
