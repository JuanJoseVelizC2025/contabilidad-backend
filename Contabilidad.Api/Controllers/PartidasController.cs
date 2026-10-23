using System.Security.Claims;
using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PartidasController : ControllerBase
{
    private readonly ContabilidadDbContext _db;

    public PartidasController(ContabilidadDbContext db) => _db = db;

    public record LineaDto(int IdCuenta, decimal Debe, decimal Haber);
    public record PartidaDto(DateOnly Fecha, string Concepto, List<LineaDto> Detalle);
    public record AnularDto(string Motivo);

    [HttpPost]
    [Authorize(Roles = "Administrador,Contador")]
    public async Task<IActionResult> Post([FromBody] PartidaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Concepto))
            return BadRequest(new { mensaje = "El concepto es obligatorio" });

        if (dto.Detalle == null || dto.Detalle.Count < 2)
            return BadRequest(new { mensaje = "La partida debe tener al menos dos lineas" });

        var lineas = dto.Detalle
            .Select(l => new LineaDto(l.IdCuenta, Math.Round(l.Debe, 2), Math.Round(l.Haber, 2)))
            .ToList();

        foreach (var l in lineas)
        {
            if (l.Debe < 0 || l.Haber < 0 || (l.Debe > 0) == (l.Haber > 0))
                return BadRequest(new { mensaje = "Cada linea debe tener un valor mayor que cero en Debe o en Haber, no en ambos" });
        }

        var totalDebe = lineas.Sum(l => l.Debe);
        var totalHaber = lineas.Sum(l => l.Haber);
        if (totalDebe != totalHaber)
            return BadRequest(new { mensaje = $"La partida no cuadra: Debe {totalDebe:N2} y Haber {totalHaber:N2}" });

        var periodo = await _db.Set<PeriodoContable>()
            .FirstOrDefaultAsync(p => p.FechaInicio <= dto.Fecha && p.FechaFin >= dto.Fecha);
        if (periodo == null)
            return BadRequest(new { mensaje = "No existe un periodo contable para esa fecha" });
        if (periodo.Estado != "Abierto")
            return BadRequest(new { mensaje = "El periodo contable esta cerrado" });

        var ids = lineas.Select(l => l.IdCuenta).Distinct().ToList();
        var cuentas = await _db.Set<Cuenta>().Where(c => ids.Contains(c.IdCuenta)).ToListAsync();
        if (cuentas.Count != ids.Count)
            return BadRequest(new { mensaje = "Alguna cuenta no existe" });

        var invalida = cuentas.FirstOrDefault(c => c.Activa != true || c.AceptaMovimientos != true);
        if (invalida != null)
            return BadRequest(new { mensaje = $"La cuenta {invalida.Codigo} esta inactiva o no acepta movimientos" });

        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        await using var tx = await _db.Database.BeginTransactionAsync();

        var ultimo = await _db.Set<Partida>()
            .Where(p => p.IdPeriodo == periodo.IdPeriodo)
            .MaxAsync(p => (int?)p.Numero) ?? 0;

        var partida = new Partida
        {
            Numero = ultimo + 1,
            Fecha = dto.Fecha,
            Concepto = dto.Concepto.Trim(),
            IdPeriodo = periodo.IdPeriodo,
            IdUsuario = idUsuario,
            Estado = "Registrada"
        };
        _db.Set<Partida>().Add(partida);
        await _db.SaveChangesAsync();

        foreach (var l in lineas)
        {
            _db.Set<PartidaDetalle>().Add(new PartidaDetalle
            {
                IdPartida = partida.IdPartida,
                IdCuenta = l.IdCuenta,
                Debe = l.Debe,
                Haber = l.Haber
            });
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { partida.IdPartida, partida.Numero, mensaje = "Partida registrada" });
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta,
        [FromQuery] int? numero, [FromQuery] string? concepto, [FromQuery] string? estado)
    {
        var q = _db.Set<Partida>().AsQueryable();
        if (desde != null) q = q.Where(p => p.Fecha >= desde.Value);
        if (hasta != null) q = q.Where(p => p.Fecha <= hasta.Value);
        if (numero != null) q = q.Where(p => p.Numero == numero.Value);
        if (!string.IsNullOrWhiteSpace(concepto))
        {
            var texto = concepto.Trim();
            q = q.Where(p => p.Concepto.Contains(texto));
        }
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(p => p.Estado == estado);

        var lista = await q
            .OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Numero)
            .Select(p => new
            {
                p.IdPartida,
                p.Numero,
                p.Fecha,
                p.Concepto,
                p.Estado,
                p.IdPeriodo,
                Anio = _db.Set<PeriodoContable>().Where(x => x.IdPeriodo == p.IdPeriodo).Select(x => x.Anio).FirstOrDefault(),
                Mes = _db.Set<PeriodoContable>().Where(x => x.IdPeriodo == p.IdPeriodo).Select(x => x.Mes).FirstOrDefault(),
                Total = _db.Set<PartidaDetalle>().Where(d => d.IdPartida == p.IdPartida).Sum(d => (decimal?)d.Debe) ?? 0m
            })
            .ToListAsync();

        return Ok(lista);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var p = await _db.Set<Partida>().FirstOrDefaultAsync(x => x.IdPartida == id);
        if (p == null) return NotFound(new { mensaje = "Partida no encontrada" });

        var detalle = await (
            from d in _db.Set<PartidaDetalle>()
            join c in _db.Set<Cuenta>() on d.IdCuenta equals c.IdCuenta
            where d.IdPartida == id
            orderby d.IdDetalle
            select new
            {
                d.IdDetalle,
                d.IdCuenta,
                c.Codigo,
                CuentaNombre = c.Nombre,
                Debe = (decimal?)d.Debe ?? 0m,
                Haber = (decimal?)d.Haber ?? 0m
            }).ToListAsync();

        return Ok(new
        {
            p.IdPartida,
            p.Numero,
            p.Fecha,
            p.Concepto,
            p.Estado,
            p.MotivoAnulacion,
            p.IdPeriodo,
            p.IdUsuario,
            TotalDebe = detalle.Sum(x => x.Debe),
            TotalHaber = detalle.Sum(x => x.Haber),
            Detalle = detalle
        });
    }

    [HttpPut("{id:int}/anular")]
    [Authorize(Roles = "Administrador,Contador")]
    public async Task<IActionResult> Anular(int id, [FromBody] AnularDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Motivo))
            return BadRequest(new { mensaje = "El motivo de anulacion es obligatorio" });

        var p = await _db.Set<Partida>().FindAsync(id);
        if (p == null) return NotFound(new { mensaje = "Partida no encontrada" });
        if (p.Estado == "Anulada")
            return BadRequest(new { mensaje = "La partida ya esta anulada" });

        var periodo = await _db.Set<PeriodoContable>().FindAsync(p.IdPeriodo);
        if (periodo == null || periodo.Estado != "Abierto")
            return BadRequest(new { mensaje = "El periodo de la partida esta cerrado" });

        p.Estado = "Anulada";
        p.MotivoAnulacion = dto.Motivo.Trim();
        await _db.SaveChangesAsync();

        return Ok(new { p.IdPartida, p.Numero, p.Estado, p.MotivoAnulacion });
    }
}