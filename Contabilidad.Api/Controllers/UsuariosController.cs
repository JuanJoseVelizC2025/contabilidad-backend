using System.Security.Claims;
using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador")]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly ContabilidadDbContext _db;

    public UsuariosController(ContabilidadDbContext db) => _db = db;

    public record UsuarioDto(string NombreUsuario, string? Clave, string NombreCompleto, string? Correo, int IdRol);
    public record ActivoDto(bool Activo);

    private int MiId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(await (
            from u in _db.Set<Usuario>()
            join r in _db.Set<Rol>() on u.IdRol equals r.IdRol
            orderby u.NombreUsuario
            select new
            {
                u.IdUsuario,
                u.NombreUsuario,
                u.NombreCompleto,
                u.Correo,
                u.IdRol,
                Rol = r.Nombre,
                Activo = u.Activo == true
            }).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] UsuarioDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NombreUsuario) || string.IsNullOrWhiteSpace(dto.NombreCompleto))
            return BadRequest(new { mensaje = "Usuario y nombre completo son obligatorios" });

        if (string.IsNullOrWhiteSpace(dto.Clave) || dto.Clave.Length < 6)
            return BadRequest(new { mensaje = "La clave debe tener al menos 6 caracteres" });

        var nombre = dto.NombreUsuario.Trim();
        if (await _db.Set<Usuario>().AnyAsync(u => u.NombreUsuario == nombre))
            return Conflict(new { mensaje = "Ya existe un usuario con ese nombre" });

        if (!await _db.Set<Rol>().AnyAsync(r => r.IdRol == dto.IdRol))
            return BadRequest(new { mensaje = "El rol no existe" });

        var u = new Usuario
        {
            NombreUsuario = nombre,
            ClaveHash = BCrypt.Net.BCrypt.HashPassword(dto.Clave),
            NombreCompleto = dto.NombreCompleto.Trim(),
            Correo = string.IsNullOrWhiteSpace(dto.Correo) ? null : dto.Correo.Trim(),
            IdRol = dto.IdRol,
            Activo = true
        };
        _db.Set<Usuario>().Add(u);
        await _db.SaveChangesAsync();

        return Ok(new { u.IdUsuario, u.NombreUsuario });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Put(int id, [FromBody] UsuarioDto dto)
    {
        var u = await _db.Set<Usuario>().FindAsync(id);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado" });

        if (string.IsNullOrWhiteSpace(dto.NombreCompleto))
            return BadRequest(new { mensaje = "El nombre completo es obligatorio" });

        if (!await _db.Set<Rol>().AnyAsync(r => r.IdRol == dto.IdRol))
            return BadRequest(new { mensaje = "El rol no existe" });

        if (id == MiId() && dto.IdRol != u.IdRol)
            return BadRequest(new { mensaje = "No puede cambiar su propio rol" });

        if (!string.IsNullOrWhiteSpace(dto.Clave))
        {
            if (dto.Clave.Length < 6)
                return BadRequest(new { mensaje = "La clave debe tener al menos 6 caracteres" });
            u.ClaveHash = BCrypt.Net.BCrypt.HashPassword(dto.Clave);
        }

        u.NombreCompleto = dto.NombreCompleto.Trim();
        u.Correo = string.IsNullOrWhiteSpace(dto.Correo) ? null : dto.Correo.Trim();
        u.IdRol = dto.IdRol;
        await _db.SaveChangesAsync();

        return Ok(new { u.IdUsuario, u.NombreUsuario });
    }

    [HttpPut("{id:int}/activo")]
    public async Task<IActionResult> PutActivo(int id, [FromBody] ActivoDto dto)
    {
        if (id == MiId() && !dto.Activo)
            return BadRequest(new { mensaje = "No puede desactivar su propio usuario" });

        var u = await _db.Set<Usuario>().FindAsync(id);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado" });

        u.Activo = dto.Activo;
        await _db.SaveChangesAsync();

        return Ok(new { u.IdUsuario, Activo = dto.Activo });
    }
}