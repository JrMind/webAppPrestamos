-- Script para crear tabla de configuración del sistema
-- Permite almacenar valores manuales como la reserva disponible real

CREATE TABLE IF NOT EXISTS configuracionsistema (
    id SERIAL PRIMARY KEY,
    clave VARCHAR(100) NOT NULL UNIQUE,
    valor TEXT NOT NULL,
    fechaactualizacion TIMESTAMP DEFAULT NOW(),
    descripcion TEXT
);

-- Crear índice para búsquedas rápidas por clave
CREATE INDEX IF NOT EXISTS idx_configuracion_clave ON configuracionsistema(clave);

-- Comentario en la tabla
COMMENT ON TABLE configuracionsistema IS 'Configuraciones globales del sistema como reserva disponible manual';
COMMENT ON COLUMN configuracionsistema.clave IS 'Identificador único de la configuración (ej: ReservaDisponibleManual)';
COMMENT ON COLUMN configuracionsistema.valor IS 'Valor de la configuración almacenado como texto';
COMMENT ON COLUMN configuracionsistema.fechaactualizacion IS 'Última vez que se actualizó esta configuración';
