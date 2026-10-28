using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Services;

public record LineaAsiento(int IdCuenta, decimal Debe, decimal Haber);

public class PartidaServicio
{
    private readonly ContabilidadDbContext _db;

    public PartidaServicio(ContabilidadDbContext db) => _db = db;

    public async Task<(Partida? Partida, string? Error)> Crear(
        DateOnly fecha, string concepto, List<LineaAsiento> lineas, int idUsuario,
        string origen, int? idOrigen)
    {
        if (string.IsNullOrWhiteSpace(concepto)) return (null, "El concepto es obligatorio");
        if (lineas.Count < 2) return (null, "La partida debe tener al menos dos lineas");

        lineas = lineas
            .Select(l => new LineaAsiento(l.IdCuenta, Math.Round(l.Debe, 2), Math.Round(l.Haber, 2)))
            .ToList();

        foreach (var l in lineas)
        {
            if (l.Debe < 0 || l.Haber < 0 || (l.Debe > 0) == (l.Haber > 0))
                return (null, "Cada linea debe tener un valor mayor que cero en Debe o en Haber, no en ambos");
        }

        var totalDebe = lineas.Sum(l => l.Debe);
        var totalHaber = lineas.Sum(l => l.Haber);
        if (totalDebe != totalHaber)
            return (null, $"La partida no cuadra: Debe {totalDebe:N2} y Haber {totalHaber:N2}");

        var periodo = await _db.Set<PeriodoContable>()
            .FirstOrDefaultAsync(p => p.FechaInicio <= fecha && p.FechaFin >= fecha);
        if (periodo == null) return (null, "No existe un periodo contable para esa fecha");
        if (periodo.Estado != "Abierto") return (null, "El periodo contable esta cerrado");

        var ids = lineas.Select(l => l.IdCuenta).Distinct().ToList();
        var cuentas = await _db.Set<Cuenta>().Where(c => ids.Contains(c.IdCuenta)).ToListAsync();
        if (cuentas.Count != ids.Count) return (null, "Alguna cuenta no existe");

        var invalida = cuentas.FirstOrDefault(c => c.Activa != true || c.AceptaMovimientos != true);
        if (invalida != null)
            return (null, $"La cuenta {invalida.Codigo} esta inactiva o no acepta movimientos");

        var ultimo = await _db.Set<Partida>()
            .Where(p => p.IdPeriodo == periodo.IdPeriodo)
            .MaxAsync(p => (int?)p.Numero) ?? 0;

        var partida = new Partida
        {
            Numero = ultimo + 1,
            Fecha = fecha,
            Concepto = concepto.Trim(),
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
        _db.Set<PartidaOrigen>().Add(new PartidaOrigen
        {
            IdPartida = partida.IdPartida,
            Origen = origen,
            IdOrigen = idOrigen
        });
        await _db.SaveChangesAsync();

        return (partida, null);
    }
}