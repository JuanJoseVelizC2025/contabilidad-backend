using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly ContabilidadDbContext _db;

    public DashboardController(ContabilidadDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        var periodo = await _db.Set<PeriodoContable>()
            .Where(p => p.FechaInicio <= hoy && p.FechaFin >= hoy)
            .FirstOrDefaultAsync()
            ?? await _db.Set<PeriodoContable>()
                .OrderByDescending(p => p.Anio).ThenByDescending(p => p.Mes)
                .FirstOrDefaultAsync();

        decimal ingresos = 0, costos = 0, gastos = 0;
        var registradas = 0;
        var anuladas = 0;

        if (periodo != null)
        {
            var delPeriodo = await (
                from d in _db.Set<PartidaDetalle>()
                join p in _db.Set<Partida>() on d.IdPartida equals p.IdPartida
                join c in _db.Set<Cuenta>() on d.IdCuenta equals c.IdCuenta
                where p.Estado == "Registrada" && p.IdPeriodo == periodo.IdPeriodo
                      && (c.Tipo == "Ingreso" || c.Tipo == "Costo" || c.Tipo == "Gasto")
                select new
                {
                    c.Tipo,
                    Debe = (decimal?)d.Debe ?? 0m,
                    Haber = (decimal?)d.Haber ?? 0m
                }).ToListAsync();

            ingresos = delPeriodo.Where(l => l.Tipo == "Ingreso").Sum(l => l.Haber - l.Debe);
            costos = delPeriodo.Where(l => l.Tipo == "Costo").Sum(l => l.Debe - l.Haber);
            gastos = delPeriodo.Where(l => l.Tipo == "Gasto").Sum(l => l.Debe - l.Haber);

            registradas = await _db.Set<Partida>()
                .CountAsync(p => p.IdPeriodo == periodo.IdPeriodo && p.Estado == "Registrada");
            anuladas = await _db.Set<Partida>()
                .CountAsync(p => p.IdPeriodo == periodo.IdPeriodo && p.Estado == "Anulada");
        }

        var desde = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-5);

        var lineas = await (
            from d in _db.Set<PartidaDetalle>()
            join p in _db.Set<Partida>() on d.IdPartida equals p.IdPartida
            join c in _db.Set<Cuenta>() on d.IdCuenta equals c.IdCuenta
            where p.Estado == "Registrada" && p.Fecha >= desde
                  && (c.Tipo == "Ingreso" || c.Tipo == "Costo" || c.Tipo == "Gasto")
            select new
            {
                p.Fecha,
                c.Tipo,
                Debe = (decimal?)d.Debe ?? 0m,
                Haber = (decimal?)d.Haber ?? 0m
            }).ToListAsync();

        var meses = Enumerable.Range(0, 6).Select(i =>
        {
            var m = desde.AddMonths(i);
            var delMes = lineas.Where(l => l.Fecha.Year == m.Year && l.Fecha.Month == m.Month).ToList();
            return new
            {
                Anio = m.Year,
                Mes = m.Month,
                Ingresos = delMes.Where(l => l.Tipo == "Ingreso").Sum(l => l.Haber - l.Debe),
                Egresos = delMes.Where(l => l.Tipo != "Ingreso").Sum(l => l.Debe - l.Haber)
            };
        }).ToList();

        var ultimas = await _db.Set<Partida>()
            .OrderByDescending(p => p.Fecha).ThenByDescending(p => p.IdPartida)
            .Take(5)
            .Select(p => new
            {
                p.IdPartida,
                p.Numero,
                p.Fecha,
                p.Concepto,
                p.Estado,
                Total = _db.Set<PartidaDetalle>().Where(d => d.IdPartida == p.IdPartida).Sum(d => (decimal?)d.Debe) ?? 0m
            })
            .ToListAsync();

        var cuentasActivas = await _db.Set<Cuenta>().CountAsync(c => c.Activa == true);
        var usuariosActivos = await _db.Set<Usuario>().CountAsync(u => u.Activo == true);

        return Ok(new
        {
            periodo = periodo == null ? null : new { periodo.IdPeriodo, periodo.Anio, periodo.Mes, periodo.Estado },
            ingresos,
            costos,
            gastos,
            utilidadNeta = ingresos - costos - gastos,
            registradas,
            anuladas,
            cuentasActivas,
            usuariosActivos,
            ultimas,
            meses
        });
    }
}
