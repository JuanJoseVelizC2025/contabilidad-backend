namespace Contabilidad.Api.Models;

public class Tercero
{
    public int IdTercero { get; set; }
    public string Tipo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Nit { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public bool Activo { get; set; }
}

public class DocumentoComercial
{
    public int IdDocumento { get; set; }
    public string Tipo { get; set; } = "";
    public int Numero { get; set; }
    public DateOnly Fecha { get; set; }
    public int IdTercero { get; set; }
    public string Condicion { get; set; } = "";
    public int? IdCuentaPago { get; set; }
    public string? Concepto { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
    public int? IdPartida { get; set; }
    public string Estado { get; set; } = "";
    public string? MotivoAnulacion { get; set; }
    public int IdUsuario { get; set; }
}

public class DocumentoLinea
{
    public int IdLinea { get; set; }
    public int IdDocumento { get; set; }
    public string Descripcion { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public int IdCuenta { get; set; }
}