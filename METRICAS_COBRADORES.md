# 📊 Métricas de Cobradores - Documentación

## Descripción General

Esta nueva funcionalidad permite visualizar métricas clave sobre el rendimiento de los cobradores y el estado general de los créditos activos, protegiendo la privacidad mediante el uso de alias anónimos.

## 🎯 Funcionalidades Implementadas

### 1. Estadísticas por Cobrador (Anónimas)

**Ubicación**: Pestaña "📈 Métricas" en la aplicación

**Datos mostrados**:
- **Alias**: Los cobradores se identifican como "Cobrador 1", "Cobrador 2", etc. (basado en su ID)
- **Promedio de Tasa de Interés**: Tasa promedio de todos los créditos activos del cobrador
- **Promedio Neto (-8%)**: Tasa promedio después de restar el 8% base
- **Capital Total Prestado**: Suma del monto prestado (capital original) de todos los créditos activos
- **Total de Créditos Activos**: Cantidad de préstamos activos asignados al cobrador

### 2. Promedio de Porcentajes de Créditos Activos

**Visualización**: Tarjeta azul en la parte superior del dashboard

**Cálculo**: Promedio de todas las tasas de interés de los préstamos con estado "Activo"

**Interpretación**: Indica el porcentaje promedio que se está cobrando en todos los créditos activos del sistema.

### 3. Capital Fantasma

**Visualización**: Tarjeta morada en la parte superior del dashboard

**Definición**: Suma total del monto prestado (capital original) de todos los préstamos activos, **sin considerar los pagos realizados**.

**Cálculo**:
```
Capital Fantasma = Σ (MontoPrestado) para todos los préstamos con EstadoPrestamo = 'Activo'
```

**Interpretación**:
- Representa el "capital en libros" o "capital comprometido"
- **No es el capital real en la calle** (ese se calcula restando pagos)
- Útil para conocer el volumen total de créditos activos independientemente de los pagos

**Diferencia con Capital Real**:
- **Capital Fantasma**: Suma de montos originales prestados (ignora pagos)
- **Capital Real en Calle**: Capital original - capital amortizado mediante pagos

### 4. Cobradores Activos

**Visualización**: Tarjeta verde en la parte superior

**Dato**: Cantidad de cobradores que tienen al menos un crédito activo asignado

## 🔧 Implementación Técnica

### Backend (C# / .NET)

**Endpoint**: `GET /api/dashboard/metricas-cobradores`

**Archivo**: `PrestamosApi/Controllers/DashboardController.cs`

**DTO creado**: `Models/DTOs/EstadisticasCobradorDto.cs`

**Lógica**:
1. Filtra todos los préstamos con estado "Activo"
2. Agrupa por CobradorId
3. Calcula promedios y sumas para cada cobrador
4. Asigna alias en orden ("Cobrador 1", "Cobrador 2", ...)
5. Calcula métricas generales (promedio total, capital fantasma)

### Frontend (React / TypeScript)

**Componente**: `src/components/MetricasCobradores.tsx`

**Tipos añadidos en** `src/types.ts`:
- `EstadisticasCobrador`
- `MetricasGenerales`

**Integración**: Nueva pestaña "📈 Métricas" en `App.tsx`

## 📋 Visualizaciones

### Tarjetas Principales (KPIs)

1. **Promedio Tasas Activas** (Azul)
   - Icono: Gráfico de barras
   - Formato: Porcentaje con 2 decimales
   - Ejemplo: "15.50%"

2. **Capital Fantasma** (Morado)
   - Icono: Moneda
   - Formato: Pesos colombianos (COP)
   - Ejemplo: "$25,000,000"
   - Subtítulo: Cantidad de préstamos activos

3. **Cobradores Activos** (Verde)
   - Icono: Grupo de personas
   - Formato: Número entero
   - Ejemplo: "2"

### Tabla Detallada

**Columnas**:
1. **Cobrador**: Avatar con alias + ID
2. **Créditos Activos**: Badge con cantidad
3. **% Promedio**: Tasa promedio del cobrador
4. **% Neto (-8%)**: Tasa después de restar 8%
5. **Capital Total**: Suma de capital prestado

**Características**:
- Filas alternadas (blanco/gris)
- Avatares de colores (azul para Cobrador 1, verde para Cobrador 2)
- Fila de totales al final
- Indicador visual para tasas netas positivas (✓ verde)

### Panel Informativo

Al final del dashboard hay un panel azul con información sobre:
- Explicación de "% Neto (-8%)"
- Definición de "Capital Fantasma"
- Significado de "Promedio Tasas Activas"
- Nota sobre la protección de identidad con alias

## 🔒 Privacidad y Seguridad

### Protección de Identidad

- **Nombres reales NO se muestran** en la interfaz
- Se utilizan alias numéricos: "Cobrador 1", "Cobrador 2", etc.
- El orden de los alias es consistente (basado en ID del cobrador)
- Solo se muestra el ID del cobrador para referencia técnica

### Datos Sensibles

Los siguientes datos están protegidos:
- ✅ Nombres de cobradores → Alias
- ✅ Información personal → No se muestra
- ℹ️ Métricas agregadas → Visibles (no identifican individualmente)

## 📊 Casos de Uso

### 1. Monitoreo de Rendimiento

**Objetivo**: Comparar el rendimiento entre cobradores sin revelar identidades

**Cómo usar**:
1. Ir a la pestaña "📈 Métricas"
2. Revisar la tabla de cobradores
3. Comparar tasas promedio y capital prestado
4. Identificar patrones por el % Neto

### 2. Análisis de Capital

**Objetivo**: Conocer cuánto capital está comprometido en créditos activos

**Cómo usar**:
1. Revisar la tarjeta "Capital Fantasma"
2. Comparar con el capital disponible
3. Tomar decisiones sobre nuevos préstamos

### 3. Evaluación de Tasas

**Objetivo**: Verificar que las tasas promedio están dentro del rango esperado

**Cómo usar**:
1. Revisar "Promedio Tasas Activas"
2. Comparar el % Neto de cada cobrador
3. Identificar desviaciones del promedio

## 🧪 Ejemplos de Datos

### Escenario 1: Dos Cobradores

```
Cobrador 1 (ID: 5):
- Créditos activos: 12
- % Promedio: 16.5%
- % Neto: 8.5%
- Capital total: $18,500,000

Cobrador 2 (ID: 7):
- Créditos activos: 8
- % Promedio: 14.0%
- % Neto: 6.0%
- Capital total: $12,000,000

Métricas Generales:
- Promedio tasas activas: 15.45%
- Capital fantasma: $30,500,000
- Cobradores activos: 2
```

## ⚙️ Configuración

### Modificar el Porcentaje Base

Si necesitas cambiar el 8% que se resta:

1. Abrir `PrestamosApi/Controllers/DashboardController.cs`
2. Buscar la línea:
   ```csharp
   PromedioTasaInteresNeto = Math.Round(g.Average(p => p.TasaInteres) - 8, 2)
   ```
3. Cambiar el `8` por el valor deseado
4. Actualizar la documentación en el componente React

### Modificar Alias

Para cambiar el formato de los alias ("Cobrador 1" → "Asesor A"):

1. Abrir `PrestamosApi/Controllers/DashboardController.cs`
2. Buscar:
   ```csharp
   Alias = $"Cobrador {index + 1}"
   ```
3. Modificar el formato según preferencia

## 🐛 Troubleshooting

### No se muestran datos

**Problema**: La página de métricas está vacía

**Posibles causas**:
1. No hay préstamos activos en el sistema
2. Los préstamos no tienen cobrador asignado
3. Error de conexión con el backend

**Solución**:
1. Verificar que existen préstamos con `EstadoPrestamo = 'Activo'`
2. Verificar que tienen `CobradorId` asignado
3. Revisar la consola del navegador para errores

### Error 500 en el endpoint

**Problema**: Error al cargar métricas

**Solución**:
1. Verificar que la migración se ejecutó correctamente
2. Verificar que la tabla `prestamos` tiene las columnas necesarias
3. Revisar logs del servidor

### Los alias no son consistentes

**Problema**: Los números de los cobradores cambian entre recargas

**Causa**: El ordenamiento no está basado en ID

**Solución**: Verificar el `OrderBy(g => g.Key.CobradorId)` en el endpoint

## 📝 Notas Importantes

1. **Capital Fantasma vs Capital Real**:
   - El capital fantasma NO considera los pagos
   - Para capital real, consultar el endpoint de métricas principal

2. **Solo Préstamos Activos**:
   - Los cálculos solo incluyen préstamos con estado "Activo"
   - Préstamos "Pagado" o "Vencido" no se consideran

3. **Privacidad**:
   - Los alias se asignan en el backend
   - No hay forma de identificar al cobrador desde el frontend sin acceso a la base de datos

4. **Actualización en Tiempo Real**:
   - Los datos se cargan al entrar a la pestaña
   - Para actualizar, cambiar de pestaña y volver

## 🔄 Futuras Mejoras

Posibles extensiones de esta funcionalidad:

1. **Filtros por Fecha**:
   - Métricas por rango de fechas
   - Comparativas mes a mes

2. **Gráficos**:
   - Gráfico de barras por cobrador
   - Evolución temporal de tasas

3. **Exportación**:
   - Exportar a Excel
   - Generar reportes PDF

4. **Alertas**:
   - Notificar cuando un cobrador excede ciertos límites
   - Alertas de capital fantasma alto

5. **Más Métricas**:
   - Tasa de mora por cobrador
   - Tiempo promedio de cobro
   - Eficiencia de recuperación

## 📞 Soporte

Para preguntas o problemas con esta funcionalidad, revisar:
- Este documento
- Código fuente en `PrestamosApi/Controllers/DashboardController.cs`
- Componente React en `src/components/MetricasCobradores.tsx`
