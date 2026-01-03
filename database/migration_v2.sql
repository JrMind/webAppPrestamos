-- PrestamosDB Migration Script v2
-- Run this script to update the database schema
-- =====================================================

-- 1. CREATE NEW TABLES
-- =====================================================

-- Table: Usuarios (Users with roles)
CREATE TABLE IF NOT EXISTS usuarios (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(200) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    passwordhash VARCHAR(500) NOT NULL,
    telefono VARCHAR(20),
    rol VARCHAR(20) NOT NULL DEFAULT 'Socio', -- Socio, AportadorInterno, AportadorExterno, Cobrador
    porcentajeparticipacion DECIMAL(5,2) DEFAULT 0,
    tasainteresmensual DECIMAL(5,2) DEFAULT 3,
    activo BOOLEAN DEFAULT TRUE
);

-- Table: Aportes (Capital contributions)
CREATE TABLE IF NOT EXISTS aportes (
    id SERIAL PRIMARY KEY,
    usuarioid INT NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
    montoinicial DECIMAL(18,2) NOT NULL,
    montoactual DECIMAL(18,2) NOT NULL,
    fechaaporte DATE NOT NULL,
    descripcion VARCHAR(500)
);

-- Table: MovimientosCapital (Capital movements)
CREATE TABLE IF NOT EXISTS movimientoscapital (
    id SERIAL PRIMARY KEY,
    usuarioid INT NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
    tipomovimiento VARCHAR(20) NOT NULL, -- Aporte, Retiro, InteresGenerado
    monto DECIMAL(18,2) NOT NULL,
    saldoanterior DECIMAL(18,2) NOT NULL,
    saldonuevo DECIMAL(18,2) NOT NULL,
    fechamovimiento TIMESTAMP NOT NULL,
    descripcion VARCHAR(500)
);

-- Table: DistribucionesGanancias (Profit distribution per loan)
CREATE TABLE IF NOT EXISTS distribucionesganancias (
    id SERIAL PRIMARY KEY,
    prestamoid INT NOT NULL REFERENCES prestamos(id) ON DELETE CASCADE,
    usuarioid INT NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
    porcentajeasignado DECIMAL(5,2) NOT NULL,
    montoganancia DECIMAL(18,2) NOT NULL,
    fechadistribucion TIMESTAMP NOT NULL,
    liquidado BOOLEAN DEFAULT FALSE
);

-- 2. MODIFY EXISTING TABLES - Add new columns
-- =====================================================

-- Add columns to prestamos
ALTER TABLE prestamos ADD COLUMN IF NOT EXISTS cobradorid INT REFERENCES usuarios(id) ON DELETE SET NULL;
ALTER TABLE prestamos ADD COLUMN IF NOT EXISTS porcentajecobrador DECIMAL(5,2) DEFAULT 5;

-- Add column to cuotasprestamo
ALTER TABLE cuotasprestamo ADD COLUMN IF NOT EXISTS cobrado BOOLEAN DEFAULT FALSE;

-- 3. REMOVE AUDIT COLUMNS (Optional - run only if you want to clean up)
-- =====================================================
-- WARNING: This will delete data. Run only if you want to remove audit fields.

-- Remove from clientes
ALTER TABLE clientes DROP COLUMN IF EXISTS usuariocreacion;
ALTER TABLE clientes DROP COLUMN IF EXISTS usuariomodificacion;
ALTER TABLE clientes DROP COLUMN IF EXISTS fechacreacion;
ALTER TABLE clientes DROP COLUMN IF EXISTS fechamodificacion;

-- Remove from prestamos
ALTER TABLE prestamos DROP COLUMN IF EXISTS usuariocreacion;
ALTER TABLE prestamos DROP COLUMN IF EXISTS usuariomodificacion;
ALTER TABLE prestamos DROP COLUMN IF EXISTS fechacreacion;
ALTER TABLE prestamos DROP COLUMN IF EXISTS fechamodificacion;

-- Remove from cuotasprestamo
ALTER TABLE cuotasprestamo DROP COLUMN IF EXISTS usuariocreacion;
ALTER TABLE cuotasprestamo DROP COLUMN IF EXISTS usuariomodificacion;
ALTER TABLE cuotasprestamo DROP COLUMN IF EXISTS fechacreacion;
ALTER TABLE cuotasprestamo DROP COLUMN IF EXISTS fechamodificacion;

-- Remove from pagos
ALTER TABLE pagos DROP COLUMN IF EXISTS usuariocreacion;
ALTER TABLE pagos DROP COLUMN IF EXISTS fechacreacion;

-- 4. CREATE INDEXES
-- =====================================================

CREATE INDEX IF NOT EXISTS idx_usuarios_email ON usuarios(email);
CREATE INDEX IF NOT EXISTS idx_usuarios_rol ON usuarios(rol);
CREATE INDEX IF NOT EXISTS idx_aportes_usuario ON aportes(usuarioid);
CREATE INDEX IF NOT EXISTS idx_movimientos_usuario ON movimientoscapital(usuarioid);
CREATE INDEX IF NOT EXISTS idx_movimientos_fecha ON movimientoscapital(fechamovimiento);
CREATE INDEX IF NOT EXISTS idx_distribuciones_prestamo ON distribucionesganancias(prestamoid);
CREATE INDEX IF NOT EXISTS idx_distribuciones_usuario ON distribucionesganancias(usuarioid);
CREATE INDEX IF NOT EXISTS idx_prestamos_cobrador ON prestamos(cobradorid);
CREATE INDEX IF NOT EXISTS idx_cuotas_cobrado ON cuotasprestamo(cobrado);

-- 5. INSERT DEFAULT ADMIN USER (Optional)
-- =====================================================
-- Password: admin123 (SHA256 hashed)
-- You should change this password after first login

INSERT INTO usuarios (nombre, email, passwordhash, telefono, rol, porcentajeparticipacion, tasainteresmensual, activo)
VALUES ('Administrador', 'admin@prestamos.com', 'JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3THIKk=', '+573001234567', 'Socio', 100, 3, TRUE)
ON CONFLICT (email) DO NOTHING;

-- =====================================================
-- END OF MIGRATION SCRIPT
-- =====================================================
