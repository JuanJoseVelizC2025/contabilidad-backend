using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class IvaController : ControllerBase
{
    private readonly ContabilidadDbContext _db;

    public IvaController(ContabilidadDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta)
    {
        if (hasta < desde)
            return BadRequest(new { mensaje = "La fecha final no puede ser menor que la inicial" });

        var docs = await (
            from d in _db.Set<DocumentoComercial>()
            join t in _db.Set<Tercero>() on d.IdTercero equals t.IdTercero
            where d.Estado == "Registrado" && d.Fecha >= desde && d.Fecha <= hasta
            orderby d.Fecha, d.Numero
            select new
            {
                d.Tipo,
                d.Numero,
                d.Fecha,
                d.Condicion,
                Tercero = t.Nombre,
                t.Nit,
                d.Subtotal,
                d.Iva,
                d.Total
            }).ToListAsync();

        var ventas = docs.Where(x => x.Tipo == "Venta").ToList();
        var compras = docs.Where(x => x.Tipo == "Compra").ToList();
        var ivaDebito = ventas.Sum(x => x.Iva);
        var ivaCredito = compras.Sum(x => x.Iva);

        return Ok(new
        {
            desde,
            hasta,
            ventas = new
            {
                documentos = ventas,
                subtotal = ventas.Sum(x => x.Subtotal),
                iva = ivaDebito,
                total = ventas.Sum(x => x.Total)
            },
            compras = new
            {
                documentos = compras,
                subtotal = compras.Sum(x => x.Subtotal),
                iva = ivaCredito,
                total = compras.Sum(x => x.Total)
            },
            ivaDebito,
            ivaCredito,
            ivaPorPagar = ivaDebito - ivaCredito
        });
    }
}