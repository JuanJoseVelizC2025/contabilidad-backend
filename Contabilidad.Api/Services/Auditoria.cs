using System.Security.Claims;
using Contabilidad.Api.Data;
using Contabilidad.Api.Models;

namespace Contabilidad.Api.Services;

public class Auditoria
{
    private readonly ContabilidadDbContext _db;
    private readonly IHttpContextAccessor _http;

    public Auditoria(ContabilidadDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task Registrar(string modulo, string accion, string detalle,
        string? usuario = null, int? idUsuario = null)
    {
        try
        {
            var user = _http.HttpContext?.User;
            var nombre = usuario ?? user?.FindFirstValue(ClaimTypes.Name) ?? "anonimo";
            if (idUsuario == null && int.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                idUsuario = id;

            _db.Set<Bitacora>().Add(new Bitacora
            {
                FechaHora = DateTime.UtcNow,
                IdUsuario = idUsuario,
                NombreUsuario = nombre.Length > 50 ? nombre[..50] : nombre,
                Modulo = modulo,
                Accion = accion,
                Detalle = detalle.Length > 500 ? detalle[..500] : detalle
            });
            await _db.SaveChangesAsync();
        }
        catch
        {
            // La auditoria nunca debe romper la operacion principal
        }
    }
}