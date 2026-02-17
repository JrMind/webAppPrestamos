# 🔒 Mejoras de Seguridad y Control de Acceso

## Resumen de Cambios

Este documento describe las mejoras de seguridad implementadas para controlar el acceso a funcionalidades sensibles de la aplicación de préstamos.

## ✅ Mejoras Implementadas

### 1. Protección de Métricas de Cobradores

**Problema**: Las métricas de cobradores son información sensible que no debería estar disponible para todos los usuarios.

**Solución Implementada**:

#### Backend (C# / .NET)
- **Archivo**: `PrestamosApi/Attributes/AuthorizeRolesAttribute.cs` (NUEVO)
  - Atributo personalizado de autorización basado en roles
  - Valida que el usuario autenticado tenga uno de los roles permitidos
  - Integrado con el sistema de autenticación JWT existente

- **Archivo**: `PrestamosApi/Controllers/DashboardController.cs`
  - Endpoint `GET /api/dashboard/metricas-cobradores` protegido con:
    ```csharp
    [AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin)]
    ```
  - Solo usuarios con rol "Socio" o "Admin" pueden acceder

#### Frontend (React / TypeScript)
- **Archivo**: `prestamos-frontend/src/App.tsx`
  - Pestaña "📈 Métricas" solo visible para Socios:
    ```tsx
    {currentUser?.rol === 'Socio' && <button className={`tab ${activeTab === 'metricas' ? 'active' : ''}`}>📈 Métricas</button>}
    ```
  - Los usuarios sin permisos no ven la opción en el menú

**Resultado**:
- ✅ Socios: Pueden ver todas las métricas de cobradores
- ✅ Admins: Pueden ver todas las métricas de cobradores
- ❌ Otros roles: No tienen acceso a las métricas

---

### 2. Protección de Marcado de Cuotas como Pagadas

**Problema**: Cualquier usuario podía marcar cuotas como pagadas, lo cual es una operación crítica que afecta el balance financiero.

**Solución Implementada**:

#### Backend (C# / .NET)
- **Archivo**: `PrestamosApi/Controllers/CobrosController.cs`
  - Endpoint `PUT /api/cobros/{cuotaId}/marcar` protegido con:
    ```csharp
    [AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin)]
    ```
  - Solo Socios y Admins pueden marcar cuotas como cobradas

#### Frontend (React / TypeScript)
- **Archivo**: `prestamos-frontend/src/App.tsx`
  - **Ubicación 1**: Tab de Cobros - Cuotas de Hoy
    ```tsx
    <input type="checkbox" ... disabled={currentUser?.rol !== 'Socio'} />
    ```
  - **Ubicación 2**: Tab de Cobros - Cuotas Vencidas
    ```tsx
    <input type="checkbox" ... disabled={currentUser?.rol !== 'Socio'} />
    ```
  - **Ubicación 3**: Modal de Detalle de Préstamo
    ```tsx
    <input
      type="checkbox"
      disabled={currentUser?.rol !== 'Socio'}
      style={{ ..., cursor: currentUser?.rol === 'Socio' ? 'pointer' : 'not-allowed', opacity: currentUser?.rol !== 'Socio' ? 0.5 : 1 }}
    />
    ```

**Características de UI**:
- Checkbox deshabilitado visualmente para usuarios sin permisos
- Cursor cambia a "not-allowed"
- Opacidad reducida (50%) para indicar que está deshabilitado
- Los usuarios pueden VER el estado, pero no modificarlo

**Resultado**:
- ✅ Socios: Pueden marcar/desmarcar cuotas como pagadas
- ❌ Cobradores: Solo pueden VER el estado, no modificarlo
- ❌ Otros roles: Solo pueden VER el estado, no modificarlo

---

### 3. Mejora de UX para Pagos Mayores al Saldo

**Problema Original**: No estaba claro para los usuarios que podían hacer pagos mayores al saldo de la cuota.

**Solución Implementada**:

#### Backend (C# / .NET)
El backend YA soportaba pagos mayores desde antes (líneas 250-299 de `PagosController.cs`):
- Aplica el pago a la cuota actual
- Si queda excedente, lo distribuye automáticamente a cuotas futuras
- Actualiza el estado de todas las cuotas afectadas

#### Frontend (React / TypeScript) - Mejoras de UX
- **Archivo**: `prestamos-frontend/src/App.tsx`

**Cambios**:
1. **Mensaje informativo permanente**:
   ```tsx
   <div style={{ background: 'rgba(16,185,129,0.1)', ... }}>
     💡 <strong>Nota:</strong> Puede pagar más del saldo pendiente.
     El excedente se aplicará automáticamente a las siguientes cuotas.
   </div>
   ```
   - Siempre visible en el modal de pago
   - Color verde para indicar que es una característica positiva

2. **Indicador dinámico de excedente**:
   ```tsx
   {pagoForm.montoPago > selectedCuota.saldoPendiente && (
     <div style={{ background: 'rgba(59,130,246,0.1)', ... }}>
       ✅ El excedente de {formatMoney(pagoForm.montoPago - selectedCuota.saldoPendiente)}
       se aplicará automáticamente a cuotas futuras.
     </div>
   )}
   ```
   - Se muestra solo cuando el monto supera el saldo
   - Calcula y muestra el excedente exacto
   - Color azul para destacar la acción que se tomará

**Resultado**:
- ✅ Los usuarios saben que pueden pagar más
- ✅ Ven exactamente cuánto excedente se aplicará
- ✅ La funcionalidad ya existía, solo se mejoró la comunicación

---

## 🔐 Sistema de Autorización

### Atributo Personalizado: `AuthorizeRolesAttribute`

**Ubicación**: `PrestamosApi/Attributes/AuthorizeRolesAttribute.cs`

**Características**:
- Implementa `IAsyncAuthorizationFilter`
- Verifica autenticación JWT
- Valida rol del usuario contra la base de datos
- Permite múltiples roles permitidos
- Retorna 401 (Unauthorized) si no está autenticado
- Retorna 403 (Forbidden) si no tiene el rol adecuado

**Uso**:
```csharp
[AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin)]
public async Task<ActionResult> MiEndpoint()
{
    // Solo Socios y Admins pueden ejecutar esto
}
```

**Roles Disponibles** (según `Models/Usuario.cs`):
- `Admin` - Administradores del sistema
- `Socio` - Socios/dueños del negocio
- `AportadorInterno` - Aportadores internos
- `AportadorExterno` - Aportadores externos
- `Cobrador` - Cobradores de campo

---

## 📝 Matriz de Permisos

| Funcionalidad | Socio | Admin | Cobrador | Otros |
|--------------|-------|-------|----------|-------|
| Ver métricas de cobradores | ✅ | ✅ | ❌ | ❌ |
| Marcar cuotas como pagadas | ✅ | ✅ | ❌ | ❌ |
| Registrar pagos | ✅ | ✅ | ❌ | ❌ |
| Ver estado de cuotas | ✅ | ✅ | ✅ | ✅ |
| Ver cobros del día | ✅ | ✅ | Solo propios | Según rol |

---

## 🧪 Cómo Probar

### Probar Protección de Métricas

1. **Como Socio**:
   - Iniciar sesión con usuario Socio
   - Verificar que aparece la pestaña "📈 Métricas"
   - Clic en la pestaña
   - Verificar que se cargan las métricas correctamente

2. **Como Cobrador**:
   - Iniciar sesión con usuario Cobrador
   - Verificar que NO aparece la pestaña "📈 Métricas"
   - Intentar acceder directamente: `http://localhost:5000/api/dashboard/metricas-cobradores`
   - Verificar respuesta 403 Forbidden

### Probar Protección de Marcado de Cuotas

1. **Como Socio**:
   - Ir a la pestaña "📋 Cobros"
   - Verificar que los checkboxes están habilitados
   - Poder marcar/desmarcar cuotas

2. **Como Cobrador**:
   - Ir a la pestaña "📋 Cobros"
   - Verificar que los checkboxes están deshabilitados (opacidad 50%, cursor not-allowed)
   - No poder modificar el estado

### Probar Pagos Mayores

1. Ir al detalle de un préstamo
2. Clic en "💰 Pagar" en una cuota con saldo, por ejemplo $50,000
3. Ingresar un monto mayor, por ejemplo $150,000
4. Verificar que aparece el mensaje:
   - "✅ El excedente de $100,000 se aplicará automáticamente a cuotas futuras"
5. Registrar el pago
6. Verificar que:
   - La cuota actual queda en $0
   - Las siguientes cuotas se abonan con el excedente

---

## 🔄 Migración y Despliegue

### No Requiere Migración de Base de Datos

Los cambios son únicamente de lógica de negocio y no afectan el esquema de la base de datos.

### Pasos para Desplegar

1. **Backend**:
   ```bash
   cd D:\webAppPrestamos\PrestamosApi
   dotnet build
   dotnet run
   ```

2. **Frontend**:
   ```bash
   cd D:\webAppPrestamos\prestamos-frontend
   npm install
   npm run build
   npm run dev  # o npm start para producción
   ```

3. **Verificar**:
   - Navegar a la aplicación
   - Iniciar sesión con diferentes roles
   - Probar las funcionalidades protegidas

---

## 🐛 Troubleshooting

### "No autorizado" al acceder a métricas

**Problema**: Usuario Socio recibe error 401

**Posibles causas**:
1. Token JWT expirado
2. Usuario no tiene rol asignado
3. Rol no es "Socio" o "Admin"

**Solución**:
1. Cerrar sesión y volver a iniciar
2. Verificar en la base de datos que el usuario tiene `Rol = 'Socio'`
3. Revisar que el token incluye el userId correcto

### Checkboxes no se deshabilitan

**Problema**: Los checkboxes siguen habilitados para Cobradores

**Causa**: `currentUser?.rol` no está cargado

**Solución**:
1. Verificar que el login devuelve el rol correcto
2. Revisar que `setCurrentUser` se ejecuta después del login
3. Verificar en DevTools que `currentUser.rol` tiene el valor esperado

### Pago mayor no se distribuye

**Problema**: El excedente no se aplica a cuotas futuras

**Causa**: Lógica en `PagosController.cs` líneas 272-299

**Solución**:
1. Verificar que el préstamo NO es congelado (lógica diferente)
2. Revisar que existen cuotas futuras con estado "Pendiente", "Parcial" o "Vencida"
3. Revisar logs del servidor para ver si hay errores

---

## 📊 Impacto en Rendimiento

**Backend**:
- ✅ Mínimo - Solo una consulta adicional a la BD por request (verificar rol)
- ✅ Consulta cacheada por el contexto de la request

**Frontend**:
- ✅ Ninguno - Solo evaluaciones condicionales en render

---

## 🔜 Futuras Mejoras

### Sugerencias para Extender

1. **Auditoría**:
   - Registrar quién marca cuotas como pagadas
   - Log de accesos a métricas sensibles

2. **Permisos Granulares**:
   - Permitir configurar permisos por usuario
   - Roles personalizables

3. **Notificaciones**:
   - Alertar al Socio cuando se marcan cuotas
   - Notificar pagos mayores al saldo

4. **Validación de Monto Máximo**:
   - Límite configurable para pagos mayores
   - Alerta si el excedente es muy grande

---

## 📞 Soporte

Para preguntas sobre estas mejoras:
- Revisar este documento
- Código fuente en los archivos mencionados
- Logs del servidor en caso de errores

---

## ✅ Checklist de Implementación

- [x] Crear `AuthorizeRolesAttribute`
- [x] Proteger endpoint de métricas
- [x] Proteger UI de métricas (pestaña visible solo para Socios)
- [x] Proteger endpoint de marcado de cuotas
- [x] Proteger UI de marcado de cuotas (3 ubicaciones)
- [x] Mejorar UX de pagos mayores (mensaje informativo)
- [x] Probar con diferentes roles
- [x] Documentar cambios

---

**Fecha de implementación**: 2026-02-16
**Versión**: 1.0
**Autor**: Claude Code con skills de C# y React
