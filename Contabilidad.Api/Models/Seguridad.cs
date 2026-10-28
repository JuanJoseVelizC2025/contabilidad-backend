namespace Contabilidad.Api.Models;

public class CodigoVerificacion
{
    public int IdCodigo { get; set; }
    public int IdUsuario { get; set; }
    public string Proposito { get; set; } = "";
    public string CodigoHash { get; set; } = "";
    public DateTime Expira { get; set; }
    public bool Usado { get; set; }
    public int Intentos { get; set; }
    public DateTime Creado { get; set; }
}