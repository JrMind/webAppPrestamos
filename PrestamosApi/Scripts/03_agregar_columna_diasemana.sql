-- Script para agregar columna DiaSemana a la tabla prestamos
-- Entidad: Prestamo
-- Tabla: prestamos (PostgreSQL)

ALTER TABLE prestamos
ADD COLUMN diasemana text NULL;

-- Comentario: Esta columna se usa para almacenar el día de la semana (Lunes, Martes, etc.)
-- cuando la frecuencia de pago es 'Semanal'.
