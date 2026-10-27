using System.Security.Claims;
using System.Text;
using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Contabilidad.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ContabilidadDbContext _db;
    private readonly IConfiguration _config;
    private readonly Auditoria _auditoria;

    public AuthController(ContabilidadDbContext db, IConfiguration config, Auditoria auditoria)
    {
        _db = db;
        _config = config;
        _auditoria = auditoria;
    }

    public record LoginRequest(string NombreUsuario, string Clave);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Set<Usuario>()
            .FirstOrDefaultAsync(u => u.NombreUsuario == req.NombreUsuario);

        if (user == null || user.Activo != true || !BCrypt.Net.BCrypt.Verify(req.Clave, user.ClaveHash))
        {
            await _auditoria.Registrar("Seguridad", "Inicio de sesión fallido",
                $"Intento con el usuario {req.NombreUsuario}", req.NombreUsuario);
            return Unauthorized(new { mensaje = "Usuario o clave incorrectos" });
        }

        var rol = await _db.Set<Rol>()
            .Where(r => r.IdRol == user.IdRol)
            .Select(r => r.Nombre)
            .FirstAsync();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var expira = DateTime.UtcNow.AddHours(8);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.IdUsuario.ToString()),
                new Claim(ClaimTypes.Name, user.NombreUsuario),
                new Claim(ClaimTypes.Role, rol)
            }),
            Expires = expira,
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        await _auditoria.Registrar("Seguridad", "Inicio de sesión", $"Rol {rol}",
            user.NombreUsuario, user.IdUsuario);

        return Ok(new
        {
            token,
            expira,
            usuario = new
            {
                id = user.IdUsuario,
                nombreUsuario = user.NombreUsuario,
                nombreCompleto = user.NombreCompleto,
                rol
            }
        });
    }
}