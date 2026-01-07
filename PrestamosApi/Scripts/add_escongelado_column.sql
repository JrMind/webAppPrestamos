-- =====================================================
-- SQL Script para agregar campo EsCongelado a tabla Prestamos
-- Ejecutar en PostgreSQL
-- =====================================================

-- Agregar columna EsCongelado a la tabla Prestamos
ALTER TABLE "Prestamos" 
ADD COLUMN IF NOT EXISTS "EsCongelado" boolean NOT NULL DEFAULT false;

-- Comentario explicativo
COMMENT ON COLUMN "Prestamos"."EsCongelado" IS 'Préstamo congelado: solo paga intereses por período, el capital no reduce a menos que haya sobrepago';

-- Verificar que la columna fue creada
SELECT column_name, data_type, column_default 
FROM information_schema.columns 
WHERE table_name = 'Prestamos' AND column_name = 'EsCongelado';
