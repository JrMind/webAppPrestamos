-- Script para crear tabla pagoscostos y actualizar tabla costos
-- Ejecutar este script manualmente si la migración no se aplicó

-- 1. Crear tabla pagoscostos
CREATE TABLE IF NOT EXISTS pagoscostos (
    id SERIAL PRIMARY KEY,
    costoid INTEGER NOT NULL,
    montopagado DECIMAL(18,2) NOT NULL,
    fechapago TIMESTAMP NOT NULL DEFAULT NOW(),
    metodopago VARCHAR(50),
    comprobante VARCHAR(255),
    observaciones TEXT,
    
    -- Foreign key
    CONSTRAINT fk_pagoscostos_costo FOREIGN KEY (costoid) 
        REFERENCES costos(id) ON DELETE CASCADE
);

-- 2. Agregar columna totalpagado a tabla costos (si no existe)
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'costos' AND column_name = 'totalpagado'
    ) THEN
        ALTER TABLE costos ADD COLUMN totalpagado DECIMAL(18,2) NOT NULL DEFAULT 0;
    END IF;
END $$;

-- 3. Crear índices para mejor rendimiento
CREATE INDEX IF NOT EXISTS idx_pagoscostos_costo ON pagoscostos(costoid);
CREATE INDEX IF NOT EXISTS idx_pagoscostos_fecha ON pagoscostos(fechapago);

-- 4. Verificar que se crearon correctamente
SELECT 'Tabla pagoscostos creada' as resultado
WHERE EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'pagoscostos');

SELECT 'Columna totalpagado agregada a costos' as resultado
WHERE EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'costos' AND column_name = 'totalpagado');
