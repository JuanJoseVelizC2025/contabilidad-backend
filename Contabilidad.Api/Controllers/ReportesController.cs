using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ReportesController : ControllerBase
{
    private readonly ContabilidadDbContext _db;

    public ReportesController(ContabilidadDbContext db) => _db = db;

    // ---------- LIBRO DIARIO ----------
    [HttpGet("diario")]
    public async Task<IActionResult> Diario([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        if (desde == null || hasta == null || desde > hasta)
            return BadRequest(new { mensaje = "Indique un rango de fechas valido (desde y hasta)" });

        var lineas = await (
            from d in _db.Set<PartidaDetalle>()
            join p in _db.Set<Partida>() on d.IdPartida equals p.IdPartida
            join c in _db.Set<Cuenta>() on d.IdCuenta equals c.IdCuenta
            where p.Estado == "Registrada" && p.Fecha >= desde.Value && p.Fecha <= hasta.Value
            orderby p.Fecha, p.Numero, d.IdDetalle
            select new
            {
                p.IdPartida,
                p.Numero,
                p.Fecha,
                p.Concepto,
                c.Codigo,
                Cuenta = c.Nombre,
                Debe = (decimal?)d.Debe ?? 0m,
                Haber = (decimal?)d.Haber ?? 0m
            }).ToListAsync();

        var partidas = lineas
            .GroupBy(l => new { l.IdPartida, l.Numero, l.Fecha, l.Concepto })
            .Select(g => new
            {
                g.Key.Numero,
                g.Key.Fecha,
                g.Key.Concepto,
                Lineas = g.Select(m => new { m.Codigo, m.Cuenta, m.Debe, m.Haber }).ToList(),
                TotalDebe = g.Sum(m => m.Debe),
                TotalHaber = g.Sum(m => m.Haber)
            }).ToList();

        return Ok(new
        {
            desde,
            hasta,
            partidas,
            TotalDebe = lineas.Sum(l => l.Debe),
            TotalHaber = lineas.Sum(l => l.Haber)
        });
    }

    // ---------- LIBRO MAYOR GENERAL ----------
    [HttpGet("mayor")]
    public async Task<IActionResult> Mayor(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, [FromQuery] int? idCuenta)
    {
        if (desde == null || hasta == null || desde > hasta)
            return BadRequest(new { mensaje = "Indique un rango de fechas valido (desde y hasta)" });

        var consulta =
            from d in _db.Set<PartidaDetalle>()
            join p in _db.Set<Partida>() on d.IdPartida equals p.IdPartida
            join c in _db.Set<Cuenta>() on d.IdCuenta equals c.IdCuenta
            where p.Estado == "Registrada" && p.Fecha <= hasta.Value
            select new
            {
                c.IdCuenta,
                c.Codigo,
                Cuenta = c.Nombre,
                c.Naturaleza,
                p.Numero,
                p.Fecha,
                p.Concepto,
                d.IdDetalle,
                Debe = (decimal?)d.Debe ?? 0m,
                Haber = (decimal?)d.Haber ?? 0m
            };

        if (idCuenta != null) consulta = consulta.Where(x => x.IdCuenta == idCuenta.Value);

        var filas = await consulta
            .OrderBy(x => x.Codigo).ThenBy(x => x.Fecha).ThenBy(x => x.Numero).ThenBy(x => x.IdDetalle)
            .ToListAsync();

        var cuentas = filas
            .GroupBy(r => new { r.IdCuenta, r.Codigo, r.Cuenta, r.Naturaleza })
            .Select(g =>
            {
                var deudora = g.Key.Naturaleza == "Deudora";
                decimal Neto(decimal debe, decimal haber) => deudora ? debe - haber : haber - debe;

                var saldoInicial = g.Where(r => r.Fecha < desde.Value).Sum(r => Neto(r.Debe, r.Haber));
                var saldo = saldoInicial;
                var movimientos = new List<object>();
                var enRango = g.Where(r => r.Fecha >= desde.Value).ToList();

                foreach (var f in enRango)
                {
                    saldo += Neto(f.Debe, f.Haber);
                    movimientos.Add(new { f.Fecha, f.Numero, f.Concepto, f.Debe, f.Haber, Saldo = saldo });
                }

                return new
                {
                    g.Key.Codigo,
                    g.Key.Cuenta,
                    g.Key.Naturaleza,
                    SaldoInicial = saldoInicial,
                    TotalDebe = enRango.Sum(r => r.Debe),
                    TotalHaber = enRango.Sum(r => r.Haber),
                    SaldoFinal = saldo,
                    Movimientos = movimientos
                };
            })
            .Where(c => c.Movimientos.Count > 0 || c.SaldoInicial != 0)
            .ToList();

        return Ok(new { desde, hasta, cuentas });
    }

    // ---------- ESTADO DE RESULTADOS ----------
    [HttpGet("estado-resultados")]
    public async Task<IActionResult> EstadoResultados(
        [FromQuery] int? idPeriodo, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        DateOnly ini, fin;
        if (idPeriodo != null)
        {
            var periodo = await _db.Set<PeriodoContable>().FindAsync(idPeriodo.Value);
            if (periodo == null) return NotFound(new { mensaje = "Periodo no encontrado" });
            ini = periodo.FechaInicio;
            fin = periodo.FechaFin;
        }
        else
        {
            if (desde == null || hasta == null || desde > hasta)
                return BadRequest(new { mensaje = "Indique un periodo o un rango de fechas valido" });
            ini = desde.Value;
            fin = hasta.Value;
        }

        var filas = await (
            from d in _db.Set<PartidaDetalle>()
            join p in _db.Set<Partida>() on d.IdPartida equals p.IdPartida
            join c in _db.Set<Cuenta>() on d.IdCuenta equals c.IdCuenta
            where p.Estado == "Registrada" && p.Fecha >= ini && p.Fecha <= fin
                  && (c.Tipo == "Ingreso" || c.Tipo == "Costo" || c.Tipo == "Gasto")
            select new
            {
                c.Codigo,
                Cuenta = c.Nombre,
                c.Tipo,
                Debe = (decimal?)d.Debe ?? 0m,
                Haber = (decimal?)d.Haber ?? 0m
            }).ToListAsync();

        List<object> Detalle(string tipo) => filas
            .Where(r => r.Tipo == tipo)
            .GroupBy(r => new { r.Codigo, r.Cuenta })
            .Select(g => new
            {
                g.Key.Codigo,
                g.Key.Cuenta,
                Saldo = tipo == "Ingreso" ? g.Sum(r => r.Haber - r.Debe) : g.Sum(r => r.Debe - r.Haber)
            })
            .OrderBy(x => x.Codigo)
            .Cast<object>()
            .ToList();

        var ingresos = filas.Where(r => r.Tipo == "Ingreso").Sum(r => r.Haber - r.Debe);
        var costos = filas.Where(r => r.Tipo == "Costo").Sum(r => r.Debe - r.Haber);
        var gastos = filas.Where(r => r.Tipo == "Gasto").Sum(r => r.Debe - r.Haber);
        var utilidadBruta = ingresos - costos;
        var utilidadNeta = utilidadBruta - gastos;

        return Ok(new
        {
            desde = ini,
            hasta = fin,
            ingresos = new { cuentas = Detalle("Ingreso"), total = ingresos },
            costos = new { cuentas = Detalle("Costo"), total = costos },
            utilidadBruta,
            gastos = new { cuentas = Detalle("Gasto"), total = gastos },
            utilidadNeta
        });
    }
}