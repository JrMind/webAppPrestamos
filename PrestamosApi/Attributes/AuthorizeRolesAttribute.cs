using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PrestamosApi.Models;
using PrestamosApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace PrestamosApi.Attributes;

/// <summary>
/// Atributo de autorización basado en roles.
/// Valida que el usuario autenticado tenga uno de los roles permitidos.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class AuthorizeRolesAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly RolUsuario[] _rolesPermitidos;

    public AuthorizeRolesAttribute(params RolUsuario[] rolesPermitidos)
    {
        _rolesPermitidos = rolesPermitidos;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Verificar si el usuario está autenticado
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "No autenticado" });
            return;
        }

        // Obtener el userId del claim
        var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Token inválido" });
            return;
        }

        // Obtener el DbContext del DI container
        var dbContext = context.HttpContext.RequestServices.GetService<PrestamosDbContext>();
        if (dbContext == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        // Obtener el usuario de la base de datos
        var usuario = await dbContext.Usuarios.FindAsync(userId);
        if (usuario == null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Usuario no encontrado" });
            return;
        }

        // Verificar si el usuario tiene rol asignado
        if (usuario.Rol == null)
        {
            context.Result = new ForbidResult();
            return;
        }

        // Verificar si el rol del usuario está en los roles permitidos
        if (!_rolesPermitidos.Contains(usuario.Rol.Value))
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}
