-- Script para resetear el porcentaje de comisión a 0 para el cobrador Gabriel

DO $$ 
DECLARE
    v_cobrador_id INT;
    v_count INT;
BEGIN
    -- 1. Buscar el ID del cobrador Gabriel
    -- Usamos ILIKE para búsqueda insensible a mayúsculas/minúsculas
    SELECT id INTO v_cobrador_id 
    FROM usuarios 
    WHERE nombre ILIKE '%Gabriel%' 
    AND rol = 'Cobrador' -- Aseguramos que sea cobrador (ajustar si el rol se llama diferente)
    LIMIT 1;

    IF v_cobrador_id IS NOT NULL THEN
        -- 2. Contar cuántos préstamos se van a actualizar
        SELECT COUNT(*) INTO v_count 
        FROM prestamos 
        WHERE cobradorid = v_cobrador_id;

        -- 3. Actualizar el porcentaje a 0
        UPDATE prestamos
        SET porcentajecobrador = 0
        WHERE cobradorid = v_cobrador_id;
        
        RAISE NOTICE 'Se encontró al cobrador Gabriel con ID: %', v_cobrador_id;
        RAISE NOTICE 'Se actualizaron % préstamos con porcentaje a 0%%', v_count;
    ELSE
        RAISE NOTICE 'No se encontró un usuario con nombre Gabriel y rol Cobrador.';
        
        -- Intento alternativo sin filtrar por rol por si acaso
        SELECT id INTO v_cobrador_id 
        FROM usuarios 
        WHERE nombre ILIKE '%Gabriel%' 
        LIMIT 1;
        
        IF v_cobrador_id IS NOT NULL THEN
             SELECT COUNT(*) INTO v_count FROM prestamos WHERE cobradorid = v_cobrador_id;
             
             UPDATE prestamos SET porcentajecobrador = 0 WHERE cobradorid = v_cobrador_id;
             
             RAISE NOTICE 'ALERTA: Se encontró usuario Gabriel (ID %) sin filtrar por rol Cobrador. Se actualizaron % préstamos.', v_cobrador_id, v_count;
        ELSE
             RAISE NOTICE 'Definitivamente no se encontró ningún usuario llamado Gabriel.';
        END IF;
    END IF;
END $$;
