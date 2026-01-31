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
    /// Checks if the current user is a Socio
    /// </summary>
    protected bool IsSocio() => GetCurrentUserRole() == RolUsuario.Socio;

    /// <summary>
    /// Checks if the current user is an Admin
    /// </summary>
    protected bool IsAdmin() => GetCurrentUserRole() == RolUsuario.Admin;
}
