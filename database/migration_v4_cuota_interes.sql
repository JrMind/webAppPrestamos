-- Migración v4: Agregar columnas MontoCapital y MontoInteres a CuotasPrestamo
-- Esto permite calcular el interés por cuota individual para las métricas mensuales

-- Agregar columnas
ALTER TABLE "CuotasPrestamo" ADD COLUMN IF NOT EXISTS "MontoCapital" decimal(18,2) NOT NULL DEFAULT 0;
ALTER TABLE "CuotasPrestamo" ADD COLUMN IF NOT EXISTS "MontoInteres" decimal(18,2) NOT NULL DEFAULT 0;

-- Actualizar cuotas existentes (distribución lineal del interés y capital)
UPDATE "CuotasPrestamo" cp
SET 
    "MontoCapital" = ROUND(p."MontoPrestado"::numeric / p."NumeroCuotas", 2),
    "MontoInteres" = ROUND(p."MontoIntereses"::numeric / p."NumeroCuotas", 2)
FROM "Prestamos" p
WHERE cp."PrestamoId" = p."Id"
  AND (cp."MontoCapital" = 0 OR cp."MontoInteres" = 0);

-- Verificar resultados
SELECT 
    cp."Id", 
    cp."PrestamoId", 
    cp."NumeroCuota", 
    cp."MontoCuota", 
    cp."MontoCapital", 
    cp."MontoInteres",
    (cp."MontoCapital" + cp."MontoInteres") as "Total"
FROM "CuotasPrestamo" cp
LIMIT 10;
