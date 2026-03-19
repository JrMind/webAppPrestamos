using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

/// <summary>
/// Base controller with helper methods for accessing current user context
/// </summary>
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Gets the current authenticated user's ID from JWT
    /// </summary>
    protected int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    /// <summary>
    /// Gets the current authenticated user's role from JWT
    /// </summary>
    protected RolUsuario? GetCurrentUserRole()
    {
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        return Enum.TryParse<RolUsuario>(roleClaim, true, out var role) ? role : null;
    }

    /// <summary>
    /// Checks if the current user is a Cobrador
    /// </summary>
    protected bool IsCobrador() => GetCurrentUserRole() == RolUsuario.Cobrador;

    /// <summary>
    /// Checks if the current user is a Socio (or Admin, who has all Socio permissions)
    /// </summary>
    protected bool IsSocio() => GetCurrentUserRole() == RolUsuario.Socio || GetCurrentUserRole() == RolUsuario.Admin;

    /// <summary>
    /// Checks if the current user is an Admin
    /// </summary>
    protected bool IsAdmin() => GetCurrentUserRole() == RolUsuario.Admin;

    /// <summary>
    /// Checks if the current user is an Administrador (vista acotada con permisos totales)
    /// </summary>
    protected bool IsAdministrador() => GetCurrentUserRole() == RolUsuario.Administrador;

    /// <summary>
    /// Returns the data start date for Administrador scope, or null if no scope applies
    /// </summary>
    protected DateTime? GetFechaInicioAcceso()
    {
        var claim = User.FindFirst("fecha_inicio_acceso")?.Value;
        return !string.IsNullOrEmpty(claim) && DateTime.TryParse(claim, out var date)
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : null;
    }

    /// <summary>
    /// Returns the list of allowed cobrador IDs for Administrador scope, or null if no scope applies
    /// </summary>
    protected List<int>? GetCobradorIdsPermitidos()
    {
        var claim = User.FindFirst("cobradores_ids")?.Value;
        if (string.IsNullOrEmpty(claim)) return null;
        return claim.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .ToList();
    }
}
