using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador,Contador")]
[Route("api/[controller]")]
public class TercerosController : ControllerBase
{
    private readonly ContabilidadDbContext _db;

    public TercerosController(ContabilidadDbContext db) => _db = db;

    public record TerceroDto(string Tipo, string Nombre, string? Nit, string? Direccion, string? Telefono, string? Correo);
    public record ActivoDto(bool Activo);

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? tipo)
    {
        var q = _db.Set<Tercero>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tipo)) q = q.Where(t => t.Tipo == tipo);
        return Ok(await q.OrderBy(t => t.Nombre).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] TerceroDto dto)
    {
        if (dto.Tipo != "Cliente" && dto.Tipo != "Proveedor")
            return BadRequest(new { mensaje = "El tipo debe ser Cliente o Proveedor" });
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { mensaje = "El nombre es obligatorio" });

        var nit = Limpio(dto.Nit);
        if (nit != null && nit.ToUpper() != "CF" &&
            await _db.Set<Tercero>().AnyAsync(t => t.Tipo == dto.Tipo && t.Nit == nit))
            return BadRequest(new { mensaje = "Ya existe un registro con ese NIT" });

        var nuevo = new Tercero
        {
            Tipo = dto.Tipo,
            Nombre = dto.Nombre.Trim(),
            Nit = nit,
            Direccion = Limpio(dto.Direccion),
            Telefono = Limpio(dto.Telefono),
            Correo = Limpio(dto.Correo),
            Activo = true
        };
        _db.Set<Tercero>().Add(nuevo);
        await _db.SaveChangesAsync();
        return Ok(nuevo);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Put(int id, [FromBody] TerceroDto dto)
    {
        var t = await _db.Set<Tercero>().FindAsync(id);
        if (t == null) return NotFound(new { mensaje = "Registro no encontrado" });
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { mensaje = "El nombre es obligatorio" });

        var nit = Limpio(dto.Nit);
        if (nit != null && nit.ToUpper() != "CF" &&
            await _db.Set<Tercero>().AnyAsync(x => x.IdTercero != id && x.Tipo == t.Tipo && x.Nit == nit))
            return BadRequest(new { mensaje = "Ya existe un registro con ese NIT" });

        t.Nombre = dto.Nombre.Trim();
        t.Nit = nit;
        t.Direccion = Limpio(dto.Direccion);
        t.Telefono = Limpio(dto.Telefono);
        t.Correo = Limpio(dto.Correo);
        await _db.SaveChangesAsync();
        return Ok(t);
    }

    [HttpPut("{id:int}/activo")]
    public async Task<IActionResult> PutActivo(int id, [FromBody] ActivoDto dto)
    {
        var t = await _db.Set<Tercero>().FindAsync(id);
        if (t == null) return NotFound(new { mensaje = "Registro no encontrado" });

        t.Activo = dto.Activo;
        await _db.SaveChangesAsync();
        return Ok(t);
    }
}