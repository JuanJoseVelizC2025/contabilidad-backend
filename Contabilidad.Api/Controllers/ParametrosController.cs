using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ParametrosController : ControllerBase
{
    private readonly ContabilidadDbContext _db;

    public ParametrosController(ContabilidadDbContext db) => _db = db;

    public record ParametroDto(string Valor);

    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(await _db.Set<ParametroSistema>()
            .OrderBy(p => p.IdParametro)
            .Select(p => new { p.IdParametro, p.Clave, p.Valor, p.Descripcion })
            .ToListAsync());

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Put(int id, [FromBody] ParametroDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Valor))
            return BadRequest(new { mensaje = "El valor es obligatorio" });

        var p = await _db.Set<ParametroSistema>().FindAsync(id);
        if (p == null) return NotFound(new { mensaje = "Parametro no encontrado" });

        p.Valor = dto.Valor.Trim();
        await _db.SaveChangesAsync();
        return Ok(new { p.IdParametro, p.Clave, p.Valor, p.Descripcion });
    }
}