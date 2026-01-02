-- PrestamosDB Database Schema
-- Run this script to create the database tables manually if needed

-- Table: Clientes
CREATE TABLE IF NOT EXISTS clientes (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(200) NOT NULL,
    cedula VARCHAR(20) NOT NULL UNIQUE,
    telefono VARCHAR(20),
    direccion VARCHAR(300),
    email VARCHAR(100),
    fecharegistro TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    estado VARCHAR(20) DEFAULT 'Activo',
    usuariocreacion VARCHAR(100),
    fechacreacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    usuariomodificacion VARCHAR(100),
    fechamodificacion TIMESTAMP
);

-- Table: Prestamos
CREATE TABLE IF NOT EXISTS prestamos (
    id SERIAL PRIMARY KEY,
    clienteid INT NOT NULL REFERENCES clientes(id),
    montoprestado DECIMAL(18,2) NOT NULL,
    tasainteres DECIMAL(5,2) NOT NULL,
    tipointeres VARCHAR(20) DEFAULT 'Simple',
    frecuenciapago VARCHAR(20) NOT NULL,
    numerocuotas INT NOT NULL,
    fechaprestamo DATE NOT NULL,
    fechavencimiento DATE NOT NULL,
    montototal DECIMAL(18,2) NOT NULL,
    montointereses DECIMAL(18,2) NOT NULL,
    montocuota DECIMAL(18,2) NOT NULL,
    estadoprestamo VARCHAR(20) DEFAULT 'Activo',
    descripcion TEXT,
    usuariocreacion VARCHAR(100),
    fechacreacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    usuariomodificacion VARCHAR(100),
    fechamodificacion TIMESTAMP
);

-- Table: CuotasPrestamo
CREATE TABLE IF NOT EXISTS cuotasprestamo (
    id SERIAL PRIMARY KEY,
    prestamoid INT NOT NULL REFERENCES prestamos(id) ON DELETE CASCADE,
    numerocuota INT NOT NULL,
    fechacobro DATE NOT NULL,
    montocuota DECIMAL(18,2) NOT NULL,
    montopagado DECIMAL(18,2) DEFAULT 0,
    saldopendiente DECIMAL(18,2) NOT NULL,
    estadocuota VARCHAR(20) DEFAULT 'Pendiente',
    fechapago DATE,
    observaciones TEXT,
    usuariocreacion VARCHAR(100),
    fechacreacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    usuariomodificacion VARCHAR(100),
    fechamodificacion TIMESTAMP,
    UNIQUE(prestamoid, numerocuota)
);

-- Table: Pagos
CREATE TABLE IF NOT EXISTS pagos (
    id SERIAL PRIMARY KEY,
    prestamoid INT NOT NULL REFERENCES prestamos(id),
    cuotaid INT REFERENCES cuotasprestamo(id),
    montopago DECIMAL(18,2) NOT NULL,
    fechapago DATE NOT NULL,
    metodopago VARCHAR(50),
    comprobante VARCHAR(200),
    observaciones TEXT,
    usuariocreacion VARCHAR(100),
    fechacreacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_prestamos_cliente ON prestamos(clienteid);
CREATE INDEX IF NOT EXISTS idx_prestamos_estado ON prestamos(estadoprestamo);
CREATE INDEX IF NOT EXISTS idx_prestamos_fecha ON prestamos(fechaprestamo);
CREATE INDEX IF NOT EXISTS idx_cuotas_prestamo ON cuotasprestamo(prestamoid);
CREATE INDEX IF NOT EXISTS idx_cuotas_estado ON cuotasprestamo(estadocuota);
CREATE INDEX IF NOT EXISTS idx_cuotas_fecha ON cuotasprestamo(fechacobro);
CREATE INDEX IF NOT EXISTS idx_pagos_prestamo ON pagos(prestamoid);
CREATE INDEX IF NOT EXISTS idx_pagos_cuota ON pagos(cuotaid);
