-- Migración: Agregar tabla Gastos y columna CobradoMedioPago
-- Ejecutar si la migración automática no aplica

-- Columna medio de pago en cuotas (para marcar cobrado)
ALTER TABLE cuotasprestamo
  ADD COLUMN IF NOT EXISTS cobradomediopago VARCHAR(20);

-- Tabla de gastos
CREATE TABLE IF NOT EXISTS gastos (
    id SERIAL PRIMARY KEY,
    descripcion VARCHAR(500) NOT NULL,
    monto NUMERIC(18,2) NOT NULL,
    mediopago VARCHAR(20) NOT NULL DEFAULT 'Efectivo',
    fecha TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    categoria VARCHAR(100)
);
