using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Services;

public interface IAuthService
{
    Task<(Usuario? usuario, string? token, string? error)> LoginAsync(string email, string password);
    Task<Usuario?> RegisterAsync(string nombre, string email, string password, string? telefono, RolUsuario? rol = null);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

public class AuthService : IAuthService
{
    private readonly PrestamosDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(PrestamosDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<(Usuario? usuario, string? token, string? error)> LoginAsync(string email, string password)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email && u.Activo);
        if (usuario == null || !VerifyPassword(password, usuario.PasswordHash))
        {
            return (null, null, "Email o contraseña incorrectos");
        }

        // Bloquear login si el usuario no tiene rol asignado
        if (!usuario.Rol.HasValue)
        {
            return (null, null, "Tu cuenta está pendiente de aprobación. Un administrador debe asignarte un rol.");
        }

        var token = GenerateJwtToken(usuario);
        return (usuario, token, null);
    }

    public async Task<Usuario?> RegisterAsync(string nombre, string email, string password, string? telefono, RolUsuario? rol = null)
    {
        if (await _context.Usuarios.AnyAsync(u => u.Email == email))
        {
            return null;
        }

        var usuario = new Usuario
        {
            Nombre = nombre,
            Email = email,
            PasswordHash = HashPassword(password),
            Telefono = telefono,
            Rol = rol, // null si es registro público
            Activo = true
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return usuario;
    }

    public string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }

    private string GenerateJwtToken(Usuario usuario)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.Name, usuario.Nombre),
            new Claim(ClaimTypes.Role, usuario.Rol?.ToString() ?? "Pendiente")
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpireMinutes"]!)),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

