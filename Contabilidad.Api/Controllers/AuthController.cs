using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
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
    private readonly CorreoServicio _correo;

    public AuthController(ContabilidadDbContext db, IConfiguration config, Auditoria auditoria, CorreoServicio correo)
    {
        _db = db;
        _config = config;
        _auditoria = auditoria;
        _correo = correo;
    }

    public record LoginRequest(string NombreUsuario, string Clave);
    public record VerificarRequest(string NombreUsuario, string Codigo);
    public record OlvideRequest(string Usuario);
    public record RestablecerRequest(string Usuario, string Codigo, string NuevaClave);

    // ---------- Utilidades ----------
    private static string Hash(int idUsuario, string proposito, string codigo)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{idUsuario}:{proposito}:{codigo}"));
        return Convert.ToHexString(bytes);
    }

    private static string NuevoCodigo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string Enmascarar(string correo)
    {
        var i = correo.IndexOf('@');
        if (i < 0) return "***";
        return i <= 2 ? "***" + correo[i..] : correo[..2] + "***" + correo[i..];
    }

    private static string Plantilla(string nombre, string titulo, string codigo) =>
        "<div style=\"font-family:Arial,sans-serif;max-width:480px\">" +
        $"<h2>{titulo}</h2>" +
        $"<p>Hola {WebUtility.HtmlEncode(nombre)},</p>" +
        "<p>Su código de verificación es:</p>" +
        $"<p style=\"font-size:28px;letter-spacing:6px;font-weight:bold\">{codigo}</p>" +
        "<p>Vence en 10 minutos. Si usted no lo solicitó, ignore este mensaje.</p>" +
        "<p style=\"color:#6a6d70;font-size:12px\">Contabilidad ERP</p></div>";

    private async Task<string> CrearCodigo(int idUsuario, string proposito)
    {
        var anteriores = await _db.Set<CodigoVerificacion>()
            .Where(c => c.IdUsuario == idUsuario && c.Proposito == proposito && !c.Usado)
            .ToListAsync();
        foreach (var a in anteriores) a.Usado = true;

        var codigo = NuevoCodigo();
        _db.Set<CodigoVerificacion>().Add(new CodigoVerificacion
        {
            IdUsuario = idUsuario,
            Proposito = proposito,
            CodigoHash = Hash(idUsuario, proposito, codigo),
            Expira = DateTime.UtcNow.AddMinutes(10),
            Usado = false,
            Intentos = 0,
            Creado = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return codigo;
    }

    private async Task<bool> ValidarCodigo(int idUsuario, string proposito, string codigo)
    {
        var registro = await _db.Set<CodigoVerificacion>()
            .Where(c => c.IdUsuario == idUsuario && c.Proposito == proposito && !c.Usado)
            .OrderByDescending(c => c.IdCodigo)
            .FirstOrDefaultAsync();

        if (registro == null || registro.Expira < DateTime.UtcNow || registro.Intentos >= 5)
            return false;

        if (registro.CodigoHash != Hash(idUsuario, proposito, (codigo ?? "").Trim()))
        {
            registro.Intentos++;
            await _db.SaveChangesAsync();
            return false;
        }

        registro.Usado = true;
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<IActionResult> Emitir(Usuario user)
    {
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

    // ---------- Login (con doble autenticacion opcional) ----------
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

        var doble = await _db.Set<ParametroSistema>()
            .Where(p => p.Clave == "DobleAutenticacion")
            .Select(p => p.Valor)
            .FirstOrDefaultAsync();

        if (string.Equals(doble, "Si", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(user.Correo))
                return BadRequest(new { mensaje = "La doble autenticación está activa y su usuario no tiene correo registrado" });

            var codigo = await CrearCodigo(user.IdUsuario, "InicioSesion");
            var enviado = await _correo.Enviar(user.Correo, user.NombreCompleto,
                "Código de verificación - Contabilidad ERP",
                Plantilla(user.NombreCompleto, "Inicio de sesión", codigo));

            await _auditoria.Registrar("Seguridad", "Código de acceso enviado",
                enviado ? "Código enviado por correo" : "No se pudo enviar el correo",
                user.NombreUsuario, user.IdUsuario);

            if (!enviado)
                return StatusCode(502, new { mensaje = "No se pudo enviar el código por correo. Intente de nuevo." });

            return Ok(new { requiereCodigo = true, correo = Enmascarar(user.Correo), nombreUsuario = user.NombreUsuario });
        }

        return await Emitir(user);
    }

    [HttpPost("verificar-codigo")]
    public async Task<IActionResult> Verificar([FromBody] VerificarRequest req)
    {
        var user = await _db.Set<Usuario>()
            .FirstOrDefaultAsync(u => u.NombreUsuario == req.NombreUsuario);

        if (user == null || user.Activo != true || !await ValidarCodigo(user.IdUsuario, "InicioSesion", req.Codigo))
        {
            await _auditoria.Registrar("Seguridad", "Inicio de sesión fallido",
                $"Código de verificación inválido para {req.NombreUsuario}", req.NombreUsuario);
            return BadRequest(new { mensaje = "Código incorrecto o vencido" });
        }

        return await Emitir(user);
    }

    // ---------- Recuperacion de contrasena ----------
    [HttpPost("olvide-clave")]
    public async Task<IActionResult> Olvide([FromBody] OlvideRequest req)
    {
        var texto = (req.Usuario ?? "").Trim();
        var user = await _db.Set<Usuario>()
            .FirstOrDefaultAsync(u => (u.NombreUsuario == texto || u.Correo == texto) && u.Activo == true);

        if (user != null && !string.IsNullOrWhiteSpace(user.Correo))
        {
            var codigo = await CrearCodigo(user.IdUsuario, "Recuperacion");
            var enviado = await _correo.Enviar(user.Correo, user.NombreCompleto,
                "Recuperación de contraseña - Contabilidad ERP",
                Plantilla(user.NombreCompleto, "Recuperación de contraseña", codigo));

            await _auditoria.Registrar("Seguridad", "Recuperación de clave solicitada",
                enviado ? $"Código enviado a {Enmascarar(user.Correo)}" : "No se pudo enviar el correo",
                user.NombreUsuario, user.IdUsuario);
        }

        return Ok(new { mensaje = "Si el usuario existe y tiene correo, se envió un código de 6 dígitos" });
    }

    [HttpPost("restablecer-clave")]
    public async Task<IActionResult> Restablecer([FromBody] RestablecerRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NuevaClave) || req.NuevaClave.Length < 6)
            return BadRequest(new { mensaje = "La nueva clave debe tener al menos 6 caracteres" });

        var texto = (req.Usuario ?? "").Trim();
        var user = await _db.Set<Usuario>()
            .FirstOrDefaultAsync(u => (u.NombreUsuario == texto || u.Correo == texto) && u.Activo == true);

        if (user == null || !await ValidarCodigo(user.IdUsuario, "Recuperacion", req.Codigo))
            return BadRequest(new { mensaje = "Código incorrecto o vencido" });

        user.ClaveHash = BCrypt.Net.BCrypt.HashPassword(req.NuevaClave);
        await _db.SaveChangesAsync();

        await _auditoria.Registrar("Seguridad", "Clave restablecida", "Contraseña cambiada con código por correo",
            user.NombreUsuario, user.IdUsuario);

        return Ok(new { mensaje = "Contraseña actualizada" });
    }
}