namespace Contabilidad.Api.Models;

public class Bitacora
{
    public int IdBitacora { get; set; }
    public DateTime FechaHora { get; set; }
    public int? IdUsuario { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string Modulo { get; set; } = "";
    public string Accion { get; set; } = "";
    public string Detalle { get; set; } = "";
}