namespace Contabilidad.Api.Models;

public class PartidaOrigen
{
    public int IdPartida { get; set; }
    public string Origen { get; set; } = "";
    public int? IdOrigen { get; set; }
}

public class CuentaBancaria
{
    public int IdCuentaBancaria { get; set; }
    public string Banco { get; set; } = "";
    public string NumeroCuenta { get; set; } = "";
    public string TipoCuenta { get; set; } = "";
    public int IdCuenta { get; set; }
    public bool Activa { get; set; }
}

public class MovimientoBancario
{
    public int IdMovimiento { get; set; }
    public int IdCuentaBancaria { get; set; }
    public DateOnly Fecha { get; set; }
    public string Tipo { get; set; } = "";
    public decimal Monto { get; set; }
    public string? Referencia { get; set; }
    public string Concepto { get; set; } = "";
    public int? IdCuentaContrapartida { get; set; }
    public int? IdCuentaBancariaDestino { get; set; }
    public int? IdPartida { get; set; }
    public string Estado { get; set; } = "";
    public bool Conciliado { get; set; }
    public int IdUsuario { get; set; }
}