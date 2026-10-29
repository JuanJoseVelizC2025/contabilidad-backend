using System.Security.Claims;
using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Contabilidad.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador,Contador")]
[Route("api/[controller]")]
public class ActivosController : ControllerBase
{
    private readonly ContabilidadDbContext _db;
    private readonly PartidaServicio _partidas;

    public ActivosController(ContabilidadDbContext db, PartidaServicio partidas)
    {
        _db = db;
        _partidas = partidas;
    }

    public record ActivoDto(
        string Descripcion, DateOnly FechaAdquisicion, decimal Costo,
        decimal ValorResidual, int VidaUtilMeses, int IdCuentaActivo);

    public record DepreciarDto(int IdPeriodo);
    public record BajaDto(DateOnly Fecha, string Motivo);

    private int MiId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Cuenta del activo -> (cuenta de depreciacion acumulada, cuenta de gasto)
    private static readonly Dictionary<string, (string Dep, string Gasto)> Mapa = new()
    {
        ["1.2.01.01"] = ("1.2.02.01", "6.1.01.05.01"),
        ["1.2.01.02"] = ("1.2.02.02", "6.1.01.05.02"),
        ["1.2.01.03"] = ("1.2.02.03", "6.1.01.05.03"),
        ["1.2.01.04.02"] = ("1.2.02.04", "6.1.01.05.04")
    };

    private static (string Dep, string Gasto)? BuscarMapa(string codigo)
    {
        foreach (var par in Mapa)
        {
            if (codigo == par.Key || codigo.StartsWith(par.Key + "."))
                return par.Value;
        }
        return null;
    }

    private async Task<Dictionary<int, decimal>> Acumuladas()
    {
        var filas = await (
            from d in _db.Set<DepreciacionActivo>()
            join p in _db.Set<Partida>() on d.IdPartida equals p.IdPartida
            where p.Estado == "Registrada"
            group d by d.IdActivo into g
            select new { IdActivo = g.Key, Total = g.Sum(x => x.Monto) }).ToListAsync();

        return filas.ToDictionary(x => x.IdActivo, x => x.Total);
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var activos = await (
            from a in _db.Set<ActivoFijo>()
            join c in _db.Set<Cuenta>() on a.IdCuentaActivo equals c.IdCuenta
            orderby a.Codigo
            select new
            {
                a.IdActivo,
                a.Codigo,
                a.Descripcion,
                a.FechaAdquisicion,
                a.Costo,
                a.ValorResidual,
                a.VidaUtilMeses,
                a.Estado,
                a.FechaBaja,
                CuentaCodigo = c.Codigo,
                CuentaNombre = c.Nombre
            }).ToListAsync();

        var acum = await Acumuladas();

        return Ok(activos.Select(x =>
        {
            var dep = acum.GetValueOrDefault(x.IdActivo);
            return new
            {
                x.IdActivo,
                x.Codigo,
                x.Descripcion,
                x.FechaAdquisicion,
                x.Costo,
                x.ValorResidual,
                x.VidaUtilMeses,
                x.Estado,
                x.FechaBaja,
                x.CuentaCodigo,
                x.CuentaNombre,
                Mensual = Math.Round((x.Costo - x.ValorResidual) / x.VidaUtilMeses, 2),
                DepreciacionAcumulada = dep,
                ValorEnLibros = x.Estado == "Baja" ? 0m : x.Costo - dep
            };
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ActivoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Descripcion))
            return BadRequest(new { mensaje = "La descripcion es obligatoria" });
        if (dto.Costo <= 0)
            return BadRequest(new { mensaje = "El costo debe ser mayor que cero" });
        if (dto.ValorResidual < 0 || dto.ValorResidual >= dto.Costo)
            return BadRequest(new { mensaje = "El valor residual debe ser menor que el costo" });
        if (dto.VidaUtilMeses <= 0 || dto.VidaUtilMeses > 600)
            return BadRequest(new { mensaje = "La vida util debe estar entre 1 y 600 meses" });

        var cuenta = await _db.Set<Cuenta>().FindAsync(dto.IdCuentaActivo);
        if (cuenta == null || cuenta.Activa != true || cuenta.AceptaMovimientos != true || cuenta.Tipo != "Activo")
            return BadRequest(new { mensaje = "Seleccione una cuenta de activo que acepte movimientos" });

        var mapa = BuscarMapa(cuenta.Codigo);
        if (mapa == null)
            return BadRequest(new { mensaje = "Esa cuenta no es depreciable (por ejemplo, los terrenos)" });

        var codDep = mapa.Value.Dep;
        var codGasto = mapa.Value.Gasto;
        var cuentaDep = await _db.Set<Cuenta>().FirstOrDefaultAsync(x => x.Codigo == codDep);
        var cuentaGasto = await _db.Set<Cuenta>().FirstOrDefaultAsync(x => x.Codigo == codGasto);
        if (cuentaDep == null || cuentaGasto == null)
            return BadRequest(new { mensaje = "Faltan las cuentas de depreciacion en la nomenclatura" });

        var numero = await _db.Set<ActivoFijo>().CountAsync() + 1;
        var nuevo = new ActivoFijo
        {
            Codigo = $"AF-{numero:D4}",
            Descripcion = dto.Descripcion.Trim(),
            FechaAdquisicion = dto.FechaAdquisicion,
            Costo = dto.Costo,
            ValorResidual = dto.ValorResidual,
            VidaUtilMeses = dto.VidaUtilMeses,
            IdCuentaActivo = cuenta.IdCuenta,
            IdCuentaDepreciacion = cuentaDep.IdCuenta,
            IdCuentaGasto = cuentaGasto.IdCuenta,
            Estado = "Activo",
            IdUsuario = MiId()
        };
        _db.Set<ActivoFijo>().Add(nuevo);
        await _db.SaveChangesAsync();

        return Ok(new { nuevo.IdActivo, nuevo.Codigo });
    }

    [HttpPost("depreciar")]
    public async Task<IActionResult> Depreciar([FromBody] DepreciarDto dto)
    {
        var periodo = await _db.Set<PeriodoContable>().FindAsync(dto.IdPeriodo);
        if (periodo == null)
            return NotFound(new { mensaje = "Periodo no encontrado" });
        if (periodo.Estado != "Abierto")
            return BadRequest(new { mensaje = "El periodo contable esta cerrado" });

        var vigentes = await _db.Set<ActivoFijo>()
            .Where(x => x.Estado == "Activo" && x.FechaAdquisicion <= periodo.FechaFin)
            .ToListAsync();

        var acum = await Acumuladas();

        var yaDepreciados = await (
            from d in _db.Set<DepreciacionActivo>()
            join p in _db.Set<Partida>() on d.IdPartida equals p.IdPartida
            where d.IdPeriodo == dto.IdPeriodo && p.Estado == "Registrada"
            select d.IdActivo).ToListAsync();

        var items = new List<(ActivoFijo Activo, decimal Monto)>();
        foreach (var activo in vigentes)
        {
            if (yaDepreciados.Contains(activo.IdActivo)) continue;

            var depreciable = activo.Costo - activo.ValorResidual;
            var pendiente = depreciable - acum.GetValueOrDefault(activo.IdActivo);
            if (pendiente <= 0) continue;

            var monto = Math.Min(Math.Round(depreciable / activo.VidaUtilMeses, 2), pendiente);
            if (monto > 0) items.Add((activo, monto));
        }

        if (items.Count == 0)
            return BadRequest(new { mensaje = "No hay activos pendientes de depreciar en este periodo" });

        var asiento = new List<LineaAsiento>();
        foreach (var g in items.GroupBy(x => x.Activo.IdCuentaGasto))
            asiento.Add(new LineaAsiento(g.Key, g.Sum(x => x.Monto), 0m));
        foreach (var g in items.GroupBy(x => x.Activo.IdCuentaDepreciacion))
            asiento.Add(new LineaAsiento(g.Key, 0m, g.Sum(x => x.Monto)));

        await using var tx = await _db.Database.BeginTransactionAsync();

        var concepto = $"Depreciación de activos fijos - {periodo.Mes:D2}/{periodo.Anio}";
        var (partida, error) = await _partidas.Crear(periodo.FechaFin, concepto, asiento, MiId(), "Depreciacion", null);
        if (partida == null)
            return BadRequest(new { mensaje = error });

        foreach (var (activo, monto) in items)
        {
            _db.Set<DepreciacionActivo>().Add(new DepreciacionActivo
            {
                IdActivo = activo.IdActivo,
                IdPeriodo = dto.IdPeriodo,
                Monto = monto,
                IdPartida = partida.IdPartida
            });
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            NumeroPartida = partida.Numero,
            Activos = items.Count,
            Total = items.Sum(x => x.Monto),
            mensaje = "Depreciacion registrada"
        });
    }

    [HttpPost("{id:int}/baja")]
    public async Task<IActionResult> Baja(int id, [FromBody] BajaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Motivo))
            return BadRequest(new { mensaje = "El motivo de la baja es obligatorio" });

        var activo = await _db.Set<ActivoFijo>().FindAsync(id);
        if (activo == null) return NotFound(new { mensaje = "Activo no encontrado" });
        if (activo.Estado == "Baja") return BadRequest(new { mensaje = "El activo ya esta dado de baja" });
        if (dto.Fecha < activo.FechaAdquisicion)
            return BadRequest(new { mensaje = "La fecha de baja no puede ser anterior a la adquisicion" });

        var acum = (await Acumuladas()).GetValueOrDefault(id);
        var valorLibros = activo.Costo - acum;

        var asiento = new List<LineaAsiento>();
        if (acum > 0)
            asiento.Add(new LineaAsiento(activo.IdCuentaDepreciacion, acum, 0m));

        if (valorLibros > 0)
        {
            var perdida = await _db.Set<Cuenta>().FirstOrDefaultAsync(x => x.Codigo == "6.2.02.01");
            if (perdida == null)
                return BadRequest(new { mensaje = "Falta la cuenta 6.2.02.01 (perdida en venta de activos fijos)" });
            asiento.Add(new LineaAsiento(perdida.IdCuenta, valorLibros, 0m));
        }

        asiento.Add(new LineaAsiento(activo.IdCuentaActivo, 0m, activo.Costo));

        await using var tx = await _db.Database.BeginTransactionAsync();

        var concepto = $"Baja de activo fijo {activo.Codigo} - {activo.Descripcion}";
        if (concepto.Length > 240) concepto = concepto[..240];

        var (partida, error) = await _partidas.Crear(dto.Fecha, concepto, asiento, MiId(), "BajaActivo", activo.IdActivo);
        if (partida == null)
            return BadRequest(new { mensaje = error });

        activo.Estado = "Baja";
        activo.FechaBaja = dto.Fecha;
        activo.IdPartidaBaja = partida.IdPartida;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { NumeroPartida = partida.Numero, mensaje = "Baja registrada" });
    }
}