using System.Globalization;
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
public class DocumentosController : ControllerBase
{
    private readonly ContabilidadDbContext _db;
    private readonly PartidaServicio _partidas;

    public DocumentosController(ContabilidadDbContext db, PartidaServicio partidas)
    {
        _db = db;
        _partidas = partidas;
    }

    public record LineaDto(string Descripcion, decimal Cantidad, decimal PrecioUnitario, int IdCuenta);

    public record DocumentoDto(
        string Tipo, DateOnly Fecha, int IdTercero, string Condicion, int? IdCuentaPago,
        string? Concepto, List<LineaDto> Lineas);

    public record AnularDto(string Motivo);

    private int MiId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<Cuenta?> CuentaPorParametro(string clave)
    {
        var codigo = await _db.Set<ParametroSistema>()
            .Where(p => p.Clave == clave)
            .Select(p => p.Valor)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        var cod = codigo.Trim();
        return await _db.Set<Cuenta>().FirstOrDefaultAsync(x => x.Codigo == cod);
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? tipo, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var q = _db.Set<DocumentoComercial>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tipo)) q = q.Where(d => d.Tipo == tipo);
        if (desde != null) q = q.Where(d => d.Fecha >= desde.Value);
        if (hasta != null) q = q.Where(d => d.Fecha <= hasta.Value);

        var lista = await (
            from d in q
            join t in _db.Set<Tercero>() on d.IdTercero equals t.IdTercero
            orderby d.Fecha descending, d.IdDocumento descending
            select new
            {
                d.IdDocumento,
                d.Tipo,
                d.Numero,
                d.Fecha,
                d.Condicion,
                d.Subtotal,
                d.Iva,
                d.Total,
                d.Estado,
                Tercero = t.Nombre,
                NumeroPartida = _db.Set<Partida>()
                    .Where(p => p.IdPartida == d.IdPartida)
                    .Select(p => p.Numero)
                    .FirstOrDefault()
            }).Take(300).ToListAsync();

        return Ok(lista);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var doc = await _db.Set<DocumentoComercial>().FindAsync(id);
        if (doc == null) return NotFound(new { mensaje = "Documento no encontrado" });

        var tercero = await _db.Set<Tercero>().FindAsync(doc.IdTercero);

        var lineas = await (
            from l in _db.Set<DocumentoLinea>()
            join c in _db.Set<Cuenta>() on l.IdCuenta equals c.IdCuenta
            where l.IdDocumento == id
            orderby l.IdLinea
            select new
            {
                l.IdLinea,
                l.Descripcion,
                l.Cantidad,
                l.PrecioUnitario,
                l.Subtotal,
                c.Codigo,
                CuentaNombre = c.Nombre
            }).ToListAsync();

        int? numeroPartida = null;
        if (doc.IdPartida != null)
        {
            numeroPartida = await _db.Set<Partida>()
                .Where(p => p.IdPartida == doc.IdPartida.Value)
                .Select(p => (int?)p.Numero)
                .FirstOrDefaultAsync();
        }

        return Ok(new
        {
            doc.IdDocumento,
            doc.Tipo,
            doc.Numero,
            doc.Fecha,
            doc.Condicion,
            doc.Concepto,
            doc.Subtotal,
            doc.Iva,
            doc.Total,
            doc.Estado,
            doc.MotivoAnulacion,
            Tercero = tercero?.Nombre,
            TerceroNit = tercero?.Nit,
            NumeroPartida = numeroPartida,
            Lineas = lineas
        });
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] DocumentoDto dto)
    {
        if (dto.Tipo != "Venta" && dto.Tipo != "Compra")
            return BadRequest(new { mensaje = "El tipo debe ser Venta o Compra" });
        if (dto.Condicion != "Contado" && dto.Condicion != "Credito")
            return BadRequest(new { mensaje = "La condicion debe ser Contado o Credito" });
        if (dto.Lineas == null || dto.Lineas.Count == 0)
            return BadRequest(new { mensaje = "Agregue al menos una linea" });

        var tipoTercero = dto.Tipo == "Venta" ? "Cliente" : "Proveedor";
        var tercero = await _db.Set<Tercero>().FindAsync(dto.IdTercero);
        if (tercero == null || !tercero.Activo || tercero.Tipo != tipoTercero)
            return BadRequest(new { mensaje = $"Seleccione un {tipoTercero.ToLower()} valido" });

        foreach (var linea in dto.Lineas)
        {
            if (string.IsNullOrWhiteSpace(linea.Descripcion) || linea.Cantidad <= 0 || linea.PrecioUnitario <= 0)
                return BadRequest(new { mensaje = "Cada linea necesita descripcion, cantidad y precio mayores que cero" });
        }

        var idsCuentas = dto.Lineas.Select(x => x.IdCuenta).Distinct().ToList();
        var cuentasLinea = await _db.Set<Cuenta>().Where(c => idsCuentas.Contains(c.IdCuenta)).ToListAsync();
        if (cuentasLinea.Count != idsCuentas.Count)
            return BadRequest(new { mensaje = "Alguna cuenta no existe" });

        foreach (var cl in cuentasLinea)
        {
            var valida = dto.Tipo == "Venta"
                ? cl.Tipo == "Ingreso"
                : cl.Tipo == "Gasto" || cl.Tipo == "Costo" ||
                  (cl.Tipo == "Activo" && (cl.Codigo.StartsWith("1.1.03") || cl.Codigo.StartsWith("1.2")));
            if (!valida)
                return BadRequest(new { mensaje = $"La cuenta {cl.Codigo} no es valida para una factura de {dto.Tipo.ToLower()}" });
        }

        Cuenta? cuentaPago = null;
        if (dto.Condicion == "Contado")
        {
            if (dto.IdCuentaPago == null)
                return BadRequest(new { mensaje = "Seleccione la cuenta de cobro o pago" });

            cuentaPago = await _db.Set<Cuenta>().FindAsync(dto.IdCuentaPago.Value);
            if (cuentaPago == null || cuentaPago.Tipo != "Activo" || !cuentaPago.Codigo.StartsWith("1.1.01."))
                return BadRequest(new { mensaje = "La cuenta de cobro o pago debe ser de Caja o Bancos" });
        }

        var pctTexto = await _db.Set<ParametroSistema>()
            .Where(p => p.Clave == "PorcentajeIVA")
            .Select(p => p.Valor)
            .FirstOrDefaultAsync();
        var pct = decimal.TryParse(pctTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out var porcentaje)
            ? porcentaje
            : 12m;

        var lineasCalc = dto.Lineas
            .Select(x => new
            {
                x.Descripcion,
                x.Cantidad,
                x.PrecioUnitario,
                x.IdCuenta,
                Subtotal = Math.Round(x.Cantidad * x.PrecioUnitario, 2)
            })
            .ToList();

        var subtotal = lineasCalc.Sum(x => x.Subtotal);
        var iva = Math.Round(subtotal * pct / 100m, 2);
        var total = subtotal + iva;

        var cuentaIva = await CuentaPorParametro(dto.Tipo == "Venta" ? "CuentaIvaDebito" : "CuentaIvaCredito");
        if (iva > 0 && cuentaIva == null)
            return BadRequest(new { mensaje = "Configure en Parametros el codigo de la cuenta de IVA" });

        Cuenta? cuentaContra;
        if (dto.Condicion == "Credito")
        {
            cuentaContra = await CuentaPorParametro(dto.Tipo == "Venta" ? "CuentaClientes" : "CuentaProveedores");
            if (cuentaContra == null)
                return BadRequest(new { mensaje = "Configure en Parametros el codigo de la cuenta de clientes o proveedores" });
        }
        else
        {
            cuentaContra = cuentaPago;
        }

        var porCuenta = lineasCalc
            .GroupBy(x => x.IdCuenta)
            .Select(g => (Id: g.Key, Monto: g.Sum(x => x.Subtotal)))
            .ToList();

        var asiento = new List<LineaAsiento>();
        if (dto.Tipo == "Venta")
        {
            asiento.Add(new LineaAsiento(cuentaContra!.IdCuenta, total, 0m));
            foreach (var (idCuenta, monto) in porCuenta)
                asiento.Add(new LineaAsiento(idCuenta, 0m, monto));
            if (iva > 0) asiento.Add(new LineaAsiento(cuentaIva!.IdCuenta, 0m, iva));
        }
        else
        {
            foreach (var (idCuenta, monto) in porCuenta)
                asiento.Add(new LineaAsiento(idCuenta, monto, 0m));
            if (iva > 0) asiento.Add(new LineaAsiento(cuentaIva!.IdCuenta, iva, 0m));
            asiento.Add(new LineaAsiento(cuentaContra!.IdCuenta, 0m, total));
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var ultimo = await _db.Set<DocumentoComercial>()
            .Where(d => d.Tipo == dto.Tipo)
            .MaxAsync(d => (int?)d.Numero) ?? 0;
        var numero = ultimo + 1;

        var titulo = dto.Tipo == "Venta" ? "Factura de venta" : "Factura de compra";
        var concepto = $"{titulo} No. {numero:D4} - {tercero.Nombre}";

        var (partida, error) = await _partidas.Crear(
            dto.Fecha, concepto, asiento, MiId(), dto.Tipo, null);
        if (partida == null)
            return BadRequest(new { mensaje = error });

        var doc = new DocumentoComercial
        {
            Tipo = dto.Tipo,
            Numero = numero,
            Fecha = dto.Fecha,
            IdTercero = dto.IdTercero,
            Condicion = dto.Condicion,
            IdCuentaPago = dto.Condicion == "Contado" ? dto.IdCuentaPago : null,
            Concepto = string.IsNullOrWhiteSpace(dto.Concepto) ? null : dto.Concepto.Trim(),
            Subtotal = subtotal,
            Iva = iva,
            Total = total,
            IdPartida = partida.IdPartida,
            Estado = "Registrado",
            IdUsuario = MiId()
        };
        _db.Set<DocumentoComercial>().Add(doc);
        await _db.SaveChangesAsync();

        foreach (var x in lineasCalc)
        {
            _db.Set<DocumentoLinea>().Add(new DocumentoLinea
            {
                IdDocumento = doc.IdDocumento,
                Descripcion = x.Descripcion.Trim(),
                Cantidad = x.Cantidad,
                PrecioUnitario = x.PrecioUnitario,
                Subtotal = x.Subtotal,
                IdCuenta = x.IdCuenta
            });
        }

        var origenPartida = await _db.Set<PartidaOrigen>().FindAsync(partida.IdPartida);
        if (origenPartida != null) origenPartida.IdOrigen = doc.IdDocumento;
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        return Ok(new { doc.IdDocumento, doc.Numero, NumeroPartida = partida.Numero, mensaje = "Factura registrada" });
    }

    [HttpPut("{id:int}/anular")]
    public async Task<IActionResult> Anular(int id, [FromBody] AnularDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Motivo))
            return BadRequest(new { mensaje = "El motivo de anulacion es obligatorio" });

        var doc = await _db.Set<DocumentoComercial>().FindAsync(id);
        if (doc == null) return NotFound(new { mensaje = "Documento no encontrado" });
        if (doc.Estado == "Anulado")
            return BadRequest(new { mensaje = "El documento ya esta anulado" });

        if (doc.IdPartida != null)
        {
            var partida = await _db.Set<Partida>().FindAsync(doc.IdPartida.Value);
            if (partida != null)
            {
                var periodo = await _db.Set<PeriodoContable>().FindAsync(partida.IdPeriodo);
                if (periodo == null || periodo.Estado != "Abierto")
                    return BadRequest(new { mensaje = "El periodo de la partida esta cerrado" });

                var texto = $"Anulacion de factura: {dto.Motivo.Trim()}";
                partida.Estado = "Anulada";
                partida.MotivoAnulacion = texto.Length > 250 ? texto[..250] : texto;
            }
        }

        doc.Estado = "Anulado";
        doc.MotivoAnulacion = dto.Motivo.Trim().Length > 250 ? dto.Motivo.Trim()[..250] : dto.Motivo.Trim();
        await _db.SaveChangesAsync();
        return Ok(new { doc.IdDocumento, doc.Estado });
    }
}