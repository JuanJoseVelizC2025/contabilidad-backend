using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PeriodosController : ControllerBase
{
    private readonly ContabilidadDbContext _db;

    public PeriodosController(ContabilidadDbContext db) => _db = db;

    public record NuevoPeriodoDto(int Anio, int Mes);
    public record EstadoPeriodoDto(string Estado);

    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(await _db.Set<PeriodoContable>()
            .OrderByDescending(p => p.Anio).ThenByDescending(p => p.Mes)
            .Select(p => new { p.IdPeriodo, p.Anio, p.Mes, p.FechaInicio, p.FechaFin, p.Estado })
            .ToListAsync());

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Post([FromBody] NuevoPeriodoDto dto)
    {
        if (dto.Mes < 1 || dto.Mes > 12 || dto.Anio < 2000 || dto.Anio > 2100)
            return BadRequest(new { mensaje = "Anio o mes invalido" });

        var existe = await _db.Set<PeriodoContable>()
            .AnyAsync(p => p.Anio == dto.Anio && p.Mes == dto.Mes);
        if (existe) return Conflict(new { mensaje = "Ese periodo ya existe" });

        var inicio = new DateOnly(dto.Anio, dto.Mes, 1);
        var p = new PeriodoContable
        {
            Anio = dto.Anio,
            Mes = dto.Mes,
            FechaInicio = inicio,
            FechaFin = inicio.AddMonths(1).AddDays(-1),
            Estado = "Abierto"
        };
        _db.Set<PeriodoContable>().Add(p);
        await _db.SaveChangesAsync();
        return Ok(new { p.IdPeriodo, p.Anio, p.Mes, p.FechaInicio, p.FechaFin, p.Estado });
    }

    [HttpPut("{id:int}/estado")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> PutEstado(int id, [FromBody] EstadoPeriodoDto dto)
    {
        if (dto.Estado != "Abierto" && dto.Estado != "Cerrado")
            return BadRequest(new { mensaje = "El estado debe ser Abierto o Cerrado" });

        var p = await _db.Set<PeriodoContable>().FindAsync(id);
        if (p == null) return NotFound(new { mensaje = "Periodo no encontrado" });

        p.Estado = dto.Estado;
        await _db.SaveChangesAsync();
        return Ok(new { p.IdPeriodo, p.Anio, p.Mes, p.FechaInicio, p.FechaFin, p.Estado });
    }
}