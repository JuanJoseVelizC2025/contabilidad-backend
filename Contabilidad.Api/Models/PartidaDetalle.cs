using System;
using System.Collections.Generic;

namespace Contabilidad.Api.Models;

public partial class PartidaDetalle
{
    public int IdDetalle { get; set; }

    public int IdPartida { get; set; }

    public int IdCuenta { get; set; }

    public decimal Debe { get; set; }

    public decimal Haber { get; set; }

    public virtual Cuenta IdCuentaNavigation { get; set; } = null!;

    public virtual Partida IdPartidaNavigation { get; set; } = null!;
}
