using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CuentasController : ControllerBase
{
    private readonly ContabilidadDbContext _db;

    public CuentasController(ContabilidadDbContext db) => _db = db;

    public record CuentaDto(string Codigo, string Nombre, string Tipo, int? IdCuentaPadre, bool AceptaMovimientos);
    public record ActivaDto(bool Activa);

    private static readonly string[] Tipos = { "Activo", "Pasivo", "Capital", "Ingreso", "Costo", "Gasto" };

    private static string NaturalezaDe(string tipo) =>
        tipo is "Activo" or "Costo" or "Gasto" ? "Deudora" : "Acreedora";

    private static object Proyectar(Cuenta c) => new
    {
        c.IdCuenta, c.Codigo, c.Nombre, c.Tipo, c.Naturaleza, c.Nivel, c.IdCuentaPadre,
        AceptaMovimientos = c.AceptaMovimientos == true,
        Activa = c.Activa == true
    };

    private async Task<string?> Aplicar(Cuenta c, CuentaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo) || string.IsNullOrWhiteSpace(dto.Nombre))
            return "Codigo y nombre son obligatorios";

        var codigo = dto.Codigo.Trim();
        var existe = await _db.Set<Cuenta>().AnyAsync(x => x.Codigo == codigo && x.IdCuenta != c.IdCuenta);
        if (existe) return "Ya existe una cuenta con ese codigo";

        var tipo = dto.Tipo;
        var nivel = 1;
        if (dto.IdCuentaPadre != null)
        {
            if (c.IdCuenta != 0 && dto.IdCuentaPadre == c.IdCuenta)
                return "Una cuenta no puede ser su propia cuenta padre";

            var padre = await _db.Set<Cuenta>().FindAsync(dto.IdCuentaPadre.Value);
            if (padre == null) return "La cuenta padre no existe";
            if (padre.AceptaMovimientos == true)
                return "La cuenta padre acepta movimientos y no puede tener cuentas hijas";

            tipo = padre.Tipo;
            nivel = padre.Nivel + 1;
        }

        if (!Tipos.Contains(tipo)) return "Tipo de cuenta invalido";

        c.Codigo = codigo;
        c.Nombre = dto.Nombre.Trim();
        c.Tipo = tipo;
        c.Naturaleza = NaturalezaDe(tipo);
        c.Nivel = nivel;
        c.IdCuentaPadre = dto.IdCuentaPadre;
        c.AceptaMovimientos = dto.AceptaMovimientos;
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? buscar)
    {
        var q = _db.Set<Cuenta>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = buscar.Trim();
            q = q.Where(c => c.Codigo.Contains(b) || c.Nombre.Contains(b));
        }

        return Ok(await q.OrderBy(c => c.Codigo)
            .Select(c => new
            {
                c.IdCuenta, c.Codigo, c.Nombre, c.Tipo, c.Naturaleza, c.Nivel, c.IdCuentaPadre,
                AceptaMovimientos = c.AceptaMovimientos == true,
                Activa = c.Activa == true
            })
            .ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _db.Set<Cuenta>().FindAsync(id);
        return c == null ? NotFound(new { mensaje = "Cuenta no encontrada" }) : Ok(Proyectar(c));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Contador")]
    public async Task<IActionResult> Post([FromBody] CuentaDto dto)
    {
        var c = new Cuenta { Activa = true };
        var error = await Aplicar(c, dto);
        if (error != null) return BadRequest(new { mensaje = error });

        _db.Set<Cuenta>().Add(c);
        await _db.SaveChangesAsync();
        return Ok(Proyectar(c));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador,Contador")]
    public async Task<IActionResult> Put(int id, [FromBody] CuentaDto dto)
    {
        var c = await _db.Set<Cuenta>().FindAsync(id);
        if (c == null) return NotFound(new { mensaje = "Cuenta no encontrada" });

        var tieneMovimientos = await _db.Set<PartidaDetalle>().AnyAsync(d => d.IdCuenta == id);
        if (tieneMovimientos && dto.AceptaMovimientos != (c.AceptaMovimientos == true))
            return BadRequest(new { mensaje = "La cuenta ya tiene movimientos; no se puede cambiar si acepta movimientos" });

        var error = await Aplicar(c, dto);
        if (error != null) return BadRequest(new { mensaje = error });

        await _db.SaveChangesAsync();
        return Ok(Proyectar(c));
    }

    [HttpPut("{id:int}/activa")]
    [Authorize(Roles = "Administrador,Contador")]
    public async Task<IActionResult> PutActiva(int id, [FromBody] ActivaDto dto)
    {
        var c = await _db.Set<Cuenta>().FindAsync(id);
        if (c == null) return NotFound(new { mensaje = "Cuenta no encontrada" });

        if (!dto.Activa)
        {
            var hijasActivas = await _db.Set<Cuenta>()
                .AnyAsync(x => x.IdCuentaPadre == id && x.Activa == true);
            if (hijasActivas)
                return BadRequest(new { mensaje = "Primero desactive las cuentas hijas" });
        }

        c.Activa = dto.Activa;
        await _db.SaveChangesAsync();
        return Ok(Proyectar(c));
    }
}