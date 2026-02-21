-- ============================================================
-- Migration v6: Liquidaciones de Cobradores
-- Registra cada pago parcial o total hecho a un cobrador
-- ============================================================

CREATE TABLE IF NOT EXISTS liquidacionescobrador (
    id               SERIAL PRIMARY KEY,
    cobradorid       INT NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
    montoliquidado   DECIMAL(18,2) NOT NULL CHECK (montoliquidado > 0),
    fechaliquidacion TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    observaciones    TEXT NULL,
    realizadopor     INT NULL REFERENCES usuarios(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_liquidaciones_cobrador ON liquidacionescobrador(cobradorid);
CREATE INDEX IF NOT EXISTS idx_liquidaciones_fecha    ON liquidacionescobrador(fechaliquidacion DESC);

-- Verificación
SELECT 'Tabla liquidacionescobrador creada correctamente' AS resultado;
