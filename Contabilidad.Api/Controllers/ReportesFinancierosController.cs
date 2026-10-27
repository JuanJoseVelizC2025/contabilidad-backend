using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reportes")]
public class ReportesFinancierosController : ControllerBase
{
    private readonly ContabilidadDbContext _db;

    public ReportesFinancierosController(ContabilidadDbContext db) => _db = db;

    private record SumaCuenta(string Codigo, string Nombre, string Tipo, decimal Debe, decimal Haber);

    private async Task<List<SumaCuenta>> Sumas(DateOnly hasta)
    {
        var filas = await (
            from d in _db.Set<PartidaDetalle>()
            join p in _db.Set<Partida>() on d.IdPartida equals p.IdPartida
            join c in _db.Set<Cuenta>() on d.IdCuenta equals c.IdCuenta
            where p.Estado == "Registrada" && p.Fecha <= hasta
            select new
            {
                c.Codigo,
                c.Nombre,
                c.Tipo,
                Debe = (decimal?)d.Debe ?? 0m,
                Haber = (decimal?)d.Haber ?? 0m
            }).ToListAsync();

        return filas
            .GroupBy(f => new { f.Codigo, f.Nombre, f.Tipo })
            .Select(g => new SumaCuenta(g.Key.Codigo, g.Key.Nombre, g.Key.Tipo, g.Sum(x => x.Debe), g.Sum(x => x.Haber)))
            .OrderBy(s => s.Codigo)
            .ToList();
    }

    // ---------- BALANZA DE COMPROBACION ----------
    [HttpGet("balanza")]
    public async Task<IActionResult> Balanza([FromQuery] DateOnly? hasta)
    {
        if (hasta == null)
            return BadRequest(new { mensaje = "Indique la fecha de corte" });

        var sumas = await Sumas(hasta.Value);

        var cuentas = sumas.Select(s =>
        {
            var saldo = s.Debe - s.Haber;
            return new
            {
                s.Codigo,
                s.Nombre,
                s.Tipo,
                s.Debe,
                s.Haber,
                SaldoDeudor = saldo > 0 ? saldo : 0m,
                SaldoAcreedor = saldo < 0 ? -saldo : 0m
            };
        }).ToList();

        return Ok(new
        {
            hasta,
            cuentas,
            totalDebe = cuentas.Sum(c => c.Debe),
            totalHaber = cuentas.Sum(c => c.Haber),
            totalSaldoDeudor = cuentas.Sum(c => c.SaldoDeudor),
            totalSaldoAcreedor = cuentas.Sum(c => c.SaldoAcreedor)
        });
    }

    // ---------- BALANCE GENERAL ----------
    [HttpGet("balance-general")]
    public async Task<IActionResult> BalanceGeneral([FromQuery] DateOnly? hasta)
    {
        if (hasta == null)
            return BadRequest(new { mensaje = "Indique la fecha de corte" });

        var sumas = await Sumas(hasta.Value);

        List<object> Seccion(string tipo, bool deudora) => sumas
            .Where(s => s.Tipo == tipo)
            .Select(s => (object)new
            {
                s.Codigo,
                s.Nombre,
                Saldo = deudora ? s.Debe - s.Haber : s.Haber - s.Debe
            })
            .ToList();

        decimal Total(string tipo, bool deudora) => sumas
            .Where(s => s.Tipo == tipo)
            .Sum(s => deudora ? s.Debe - s.Haber : s.Haber - s.Debe);

        var activo = Total("Activo", true);
        var pasivo = Total("Pasivo", false);
        var capital = Total("Capital", false);
        var resultado = Total("Ingreso", false) - Total("Costo", true) - Total("Gasto", true);
        var pasivoCapital = pasivo + capital + resultado;

        return Ok(new
        {
            hasta,
            activo = new { cuentas = Seccion("Activo", true), total = activo },
            pasivo = new { cuentas = Seccion("Pasivo", false), total = pasivo },
            capital = new { cuentas = Seccion("Capital", false), total = capital },
            resultadoEjercicio = resultado,
            totalPasivoCapital = pasivoCapital,
            cuadra = activo == pasivoCapital
        });
    }
}