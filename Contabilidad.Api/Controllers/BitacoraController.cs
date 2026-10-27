using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador")]
[Route("api/[controller]")]
public class BitacoraController : ControllerBase
{
    private readonly ContabilidadDbContext _db;

    public BitacoraController(ContabilidadDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta,
        [FromQuery] string? usuario, [FromQuery] string? modulo)
    {
        var q = _db.Set<Bitacora>().AsQueryable();

        if (desde != null)
        {
            var inicio = desde.Value.ToDateTime(TimeOnly.MinValue).AddHours(6);
            q = q.Where(b => b.FechaHora >= inicio);
        }
        if (hasta != null)
        {
            var fin = hasta.Value.ToDateTime(TimeOnly.MinValue).AddDays(1).AddHours(6);
            q = q.Where(b => b.FechaHora < fin);
        }
        if (!string.IsNullOrWhiteSpace(usuario))
        {
            var u = usuario.Trim();
            q = q.Where(b => b.NombreUsuario.Contains(u));
        }
        if (!string.IsNullOrWhiteSpace(modulo))
            q = q.Where(b => b.Modulo == modulo);

        var filas = await q
            .OrderByDescending(b => b.FechaHora).ThenByDescending(b => b.IdBitacora)
            .Take(300)
            .ToListAsync();

        return Ok(filas.Select(b => new
        {
            b.IdBitacora,
            FechaHora = DateTime.SpecifyKind(b.FechaHora, DateTimeKind.Utc),
            b.NombreUsuario,
            b.Modulo,
            b.Accion,
            b.Detalle
        }));
    }
}