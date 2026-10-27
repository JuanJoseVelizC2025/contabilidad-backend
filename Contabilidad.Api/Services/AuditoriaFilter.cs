using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Contabilidad.Api.Services;

public class AuditoriaFilter : IAsyncActionFilter
{
    private readonly Auditoria _auditoria;

    public AuditoriaFilter(Auditoria auditoria) => _auditoria = auditoria;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var resultado = await next();

        var metodo = context.HttpContext.Request.Method;
        if (metodo == "GET" || metodo == "OPTIONS" || resultado.Exception != null) return;

        var controlador = context.RouteData.Values["controller"]?.ToString() ?? "";
        if (controlador == "Auth") return;

        var codigo = (resultado.Result as IStatusCodeActionResult)?.StatusCode ?? 200;
        if (codigo < 200 || codigo >= 300) return;

        var accion = context.RouteData.Values["action"]?.ToString() ?? "";
        var id = context.RouteData.Values["id"]?.ToString();
        var valor = (resultado.Result as ObjectResult)?.Value;

        var (titulo, detalle) = Describir(controlador, accion, id, valor);
        await _auditoria.Registrar(controlador, titulo, detalle);
    }

    private static string? Propiedad(object? valor, string nombre) =>
        valor?.GetType().GetProperty(nombre)?.GetValue(valor)?.ToString();

    private static (string Titulo, string Detalle) Describir(string c, string a, string? id, object? v)
    {
        string P(string n) => Propiedad(v, n) ?? "";

        return (c, a) switch
        {
            ("Partidas", "Post") => ("Registrar partida", $"Partida No. {P("Numero")}"),
            ("Partidas", "Anular") => ("Anular partida", $"Partida No. {P("Numero")}"),
            ("Cuentas", "Post") => ("Crear cuenta", $"Cuenta {P("Codigo")}"),
            ("Cuentas", "Put") => ("Modificar cuenta", $"Cuenta {P("Codigo")}"),
            ("Cuentas", "PutActiva") => (P("Activa") == "True" ? "Activar cuenta" : "Desactivar cuenta", $"Cuenta {P("Codigo")}"),
            ("Usuarios", "Post") => ("Crear usuario", $"Usuario {P("NombreUsuario")}"),
            ("Usuarios", "Put") => ("Modificar usuario", $"Usuario {P("NombreUsuario")}"),
            ("Usuarios", "PutActivo") => (P("Activo") == "True" ? "Activar usuario" : "Desactivar usuario", $"Usuario id {id}"),
            ("Periodos", "Post") => ("Crear período", $"Período {P("Mes")}/{P("Anio")}"),
            ("Periodos", "PutEstado") => ("Cambiar estado del período", $"Período {P("Mes")}/{P("Anio")}: {P("Estado")}"),
            ("Parametros", "Put") => ("Modificar parámetro", $"Parámetro {P("Clave")}"),
            _ => ($"{c} {a}", $"Operación sobre id {id}")
        };
    }
}