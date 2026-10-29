namespace Contabilidad.Api.Models;

public class ActivoFijo
{
    public int IdActivo { get; set; }
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaAdquisicion { get; set; }
    public decimal Costo { get; set; }
    public decimal ValorResidual { get; set; }
    public int VidaUtilMeses { get; set; }
    public int IdCuentaActivo { get; set; }
    public int IdCuentaDepreciacion { get; set; }
    public int IdCuentaGasto { get; set; }
    public string Estado { get; set; } = "";
    public DateOnly? FechaBaja { get; set; }
    public int? IdPartidaBaja { get; set; }
    public int IdUsuario { get; set; }
}

public class DepreciacionActivo
{
    public int IdDepreciacion { get; set; }
    public int IdActivo { get; set; }
    public int IdPeriodo { get; set; }
    public decimal Monto { get; set; }
    public int IdPartida { get; set; }
}