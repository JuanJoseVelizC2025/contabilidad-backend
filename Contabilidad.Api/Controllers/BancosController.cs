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
public class BancosController : ControllerBase
{
    private readonly ContabilidadDbContext _db;
    private readonly PartidaServicio _partidas;

    public BancosController(ContabilidadDbContext db, PartidaServicio partidas)
    {
        _db = db;
        _partidas = partidas;
    }

    public record CuentaBancariaDto(string Banco, string NumeroCuenta, string TipoCuenta, int IdCuenta);

    public record MovimientoDto(
        int IdCuentaBancaria, DateOnly Fecha, string Tipo, decimal Monto, string? Referencia,
        string Concepto, int? IdCuentaContrapartida, int? IdCuentaBancariaDestino);

    public record AnularDto(string Motivo);
    public record ConciliarDto(bool Conciliado);

    private static readonly string[] Entradas = { "Deposito", "NotaCredito" };
    private static readonly string[] Salidas = { "Retiro", "Cheque", "NotaDebito" };

    private int MiId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string Etiqueta(string tipo) => tipo switch
    {
        "Deposito" => "Depósito bancario",
        "Retiro" => "Retiro bancario",
        "Cheque" => "Cheque emitido",
        "NotaDebito" => "Nota de débito",
        "NotaCredito" => "Nota de crédito",
        _ => "Transferencia entre cuentas"
    };

    // ---------- CUENTAS BANCARIAS ----------
    [HttpGet("cuentas")]
    public async Task<IActionResult> Cuentas()
    {
        var cuentas = await (
            from b in _db.Set<CuentaBancaria>()
            join c in _db.Set<Cuenta>() on b.IdCuenta equals c.IdCuenta
            orderby b.Banco, b.NumeroCuenta
            select new
            {
                b.IdCuentaBancaria,
                b.Banco,
                b.NumeroCuenta,
                b.TipoCuenta,
                b.IdCuenta,
                c.Codigo,
                CuentaNombre = c.Nombre,
                b.Activa
            }).ToListAsync();

        var ids = cuentas.Select(x => x.IdCuenta).ToList();
        var sumas = await (
            from d in _db.Set<PartidaDetalle>()
            join p in _db.Set<Partida>() on d.IdPartida equals p.IdPartida
            where p.Estado == "Registrada" && ids.Contains(d.IdCuenta)
            select new
            {
                d.IdCuenta,
                Debe = (decimal?)d.Debe ?? 0m,
                Haber = (decimal?)d.Haber ?? 0m
            }).ToListAsync();

        return Ok(cuentas.Select(x => new
        {
            x.IdCuentaBancaria,
            x.Banco,
            x.NumeroCuenta,
            x.TipoCuenta,
            x.IdCuenta,
            x.Codigo,
            x.CuentaNombre,
            x.Activa,
            Saldo = sumas.Where(s => s.IdCuenta == x.IdCuenta).Sum(s => s.Debe - s.Haber)
        }));
    }

    [HttpPost("cuentas")]
    public async Task<IActionResult> CrearCuenta([FromBody] CuentaBancariaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Banco) || string.IsNullOrWhiteSpace(dto.NumeroCuenta))
            return BadRequest(new { mensaje = "Banco y numero de cuenta son obligatorios" });

        if (dto.TipoCuenta != "Monetaria" && dto.TipoCuenta != "Ahorro")
            return BadRequest(new { mensaje = "El tipo de cuenta debe ser Monetaria o Ahorro" });

        var cuenta = await _db.Set<Cuenta>().FindAsync(dto.IdCuenta);
        if (cuenta == null || cuenta.Activa != true || cuenta.AceptaMovimientos != true || cuenta.Tipo != "Activo")
            return BadRequest(new { mensaje = "Seleccione una cuenta contable activa de tipo Activo que acepte movimientos" });

        if (!cuenta.Codigo.StartsWith("1.1.01.02."))
            return BadRequest(new { mensaje = "La cuenta contable debe pertenecer al rubro Bancos (1.1.01.02)" });

        if (await _db.Set<CuentaBancaria>().AnyAsync(x => x.IdCuenta == dto.IdCuenta))
            return BadRequest(new { mensaje = "Esa cuenta contable ya esta asignada a otra cuenta bancaria" });

        var banco = dto.Banco.Trim();
        var numero = dto.NumeroCuenta.Trim();
        if (await _db.Set<CuentaBancaria>().AnyAsync(x => x.Banco == banco && x.NumeroCuenta == numero))
            return BadRequest(new { mensaje = "Ya existe esa cuenta bancaria" });

        var nueva = new CuentaBancaria
        {
            Banco = banco,
            NumeroCuenta = numero,
            TipoCuenta = dto.TipoCuenta,
            IdCuenta = dto.IdCuenta,
            Activa = true
        };
        _db.Set<CuentaBancaria>().Add(nueva);
        await _db.SaveChangesAsync();

        return Ok(new { nueva.IdCuentaBancaria, nueva.Banco, nueva.NumeroCuenta });
    }

    // ---------- MOVIMIENTOS ----------
    [HttpGet("movimientos")]
    public async Task<IActionResult> Movimientos(
        [FromQuery] int? idCuentaBancaria, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var q = _db.Set<MovimientoBancario>().AsQueryable();
        if (idCuentaBancaria != null) q = q.Where(m => m.IdCuentaBancaria == idCuentaBancaria.Value);
        if (desde != null) q = q.Where(m => m.Fecha >= desde.Value);
        if (hasta != null) q = q.Where(m => m.Fecha <= hasta.Value);

        var lista = await (
            from m in q
            join b in _db.Set<CuentaBancaria>() on m.IdCuentaBancaria equals b.IdCuentaBancaria
            orderby m.Fecha descending, m.IdMovimiento descending
            select new
            {
                m.IdMovimiento,
                m.Fecha,
                m.Tipo,
                m.Monto,
                m.Referencia,
                m.Concepto,
                m.Estado,
                m.Conciliado,
                m.IdPartida,
                b.Banco,
                b.NumeroCuenta,
                NumeroPartida = _db.Set<Partida>()
                    .Where(p => p.IdPartida == m.IdPartida)
                    .Select(p => p.Numero)
                    .FirstOrDefault()
            }).Take(200).ToListAsync();

        return Ok(lista);
    }

    [HttpPost("movimientos")]
    public async Task<IActionResult> CrearMovimiento([FromBody] MovimientoDto dto)
    {
        var tipos = Entradas.Concat(Salidas).Append("Transferencia");
        if (!tipos.Contains(dto.Tipo))
            return BadRequest(new { mensaje = "Tipo de movimiento invalido" });
        if (dto.Monto <= 0)
            return BadRequest(new { mensaje = "El monto debe ser mayor que cero" });
        if (string.IsNullOrWhiteSpace(dto.Concepto))
            return BadRequest(new { mensaje = "El concepto es obligatorio" });

        var origen = await _db.Set<CuentaBancaria>().FindAsync(dto.IdCuentaBancaria);
        if (origen == null || !origen.Activa)
            return BadRequest(new { mensaje = "La cuenta bancaria no es valida" });

        var monto = Math.Round(dto.Monto, 2);
        LineaAsiento[] lineas;
        int? contrapartida = null;
        int? destinoId = null;

        if (dto.Tipo == "Transferencia")
        {
            if (dto.IdCuentaBancariaDestino == null || dto.IdCuentaBancariaDestino == dto.IdCuentaBancaria)
                return BadRequest(new { mensaje = "Seleccione una cuenta bancaria de destino distinta" });

            var destino = await _db.Set<CuentaBancaria>().FindAsync(dto.IdCuentaBancariaDestino.Value);
            if (destino == null || !destino.Activa)
                return BadRequest(new { mensaje = "La cuenta de destino no es valida" });

            destinoId = destino.IdCuentaBancaria;
            lineas = new[]
            {
                new LineaAsiento(destino.IdCuenta, monto, 0m),
                new LineaAsiento(origen.IdCuenta, 0m, monto)
            };
        }
        else
        {
            if (dto.IdCuentaContrapartida == null)
                return BadRequest(new { mensaje = "Seleccione la cuenta contable de contrapartida" });
            if (dto.IdCuentaContrapartida == origen.IdCuenta)
                return BadRequest(new { mensaje = "La contrapartida no puede ser la misma cuenta del banco" });

            contrapartida = dto.IdCuentaContrapartida;
            lineas = Entradas.Contains(dto.Tipo)
                ? new[]
                {
                    new LineaAsiento(origen.IdCuenta, monto, 0m),
                    new LineaAsiento(contrapartida.Value, 0m, monto)
                }
                : new[]
                {
                    new LineaAsiento(contrapartida.Value, monto, 0m),
                    new LineaAsiento(origen.IdCuenta, 0m, monto)
                };
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var concepto = $"{Etiqueta(dto.Tipo)} - {dto.Concepto.Trim()}";
        var (partida, error) = await _partidas.Crear(dto.Fecha, concepto, lineas.ToList(), MiId(), "Banco", null);
        if (partida == null)
            return BadRequest(new { mensaje = error });

        var mov = new MovimientoBancario
        {
            IdCuentaBancaria = dto.IdCuentaBancaria,
            Fecha = dto.Fecha,
            Tipo = dto.Tipo,
            Monto = monto,
            Referencia = string.IsNullOrWhiteSpace(dto.Referencia) ? null : dto.Referencia.Trim(),
            Concepto = dto.Concepto.Trim(),
            IdCuentaContrapartida = contrapartida,
            IdCuentaBancariaDestino = destinoId,
            IdPartida = partida.IdPartida,
            Estado = "Registrado",
            Conciliado = false,
            IdUsuario = MiId()
        };
        _db.Set<MovimientoBancario>().Add(mov);
        await _db.SaveChangesAsync();

        var origenPartida = await _db.Set<PartidaOrigen>().FindAsync(partida.IdPartida);
        if (origenPartida != null)
        {
            origenPartida.IdOrigen = mov.IdMovimiento;
            await _db.SaveChangesAsync();
        }

        await tx.CommitAsync();

        return Ok(new { mov.IdMovimiento, NumeroPartida = partida.Numero, mensaje = "Movimiento registrado" });
    }

    [HttpPut("movimientos/{id:int}/anular")]
    public async Task<IActionResult> AnularMovimiento(int id, [FromBody] AnularDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Motivo))
            return BadRequest(new { mensaje = "El motivo de anulacion es obligatorio" });

        var mov = await _db.Set<MovimientoBancario>().FindAsync(id);
        if (mov == null) return NotFound(new { mensaje = "Movimiento no encontrado" });
        if (mov.Estado == "Anulado")
            return BadRequest(new { mensaje = "El movimiento ya esta anulado" });

        if (mov.IdPartida != null)
        {
            var partida = await _db.Set<Partida>().FindAsync(mov.IdPartida.Value);
            if (partida != null)
            {
                var periodo = await _db.Set<PeriodoContable>().FindAsync(partida.IdPeriodo);
                if (periodo == null || periodo.Estado != "Abierto")
                    return BadRequest(new { mensaje = "El periodo de la partida esta cerrado" });

                var texto = $"Anulacion de movimiento bancario: {dto.Motivo.Trim()}";
                partida.Estado = "Anulada";
                partida.MotivoAnulacion = texto.Length > 250 ? texto[..250] : texto;
            }
        }

        mov.Estado = "Anulado";
        await _db.SaveChangesAsync();
        return Ok(new { mov.IdMovimiento, mov.Estado });
    }

    [HttpPut("movimientos/{id:int}/conciliar")]
    public async Task<IActionResult> Conciliar(int id, [FromBody] ConciliarDto dto)
    {
        var mov = await _db.Set<MovimientoBancario>().FindAsync(id);
        if (mov == null) return NotFound(new { mensaje = "Movimiento no encontrado" });

        mov.Conciliado = dto.Conciliado;
        await _db.SaveChangesAsync();
        return Ok(new { mov.IdMovimiento, mov.Conciliado });
    }
}