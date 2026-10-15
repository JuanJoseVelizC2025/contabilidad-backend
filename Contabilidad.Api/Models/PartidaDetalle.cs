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

    public virtual Cuentum IdCuentaNavigation { get; set; } = null!;

    public virtual Partidum IdPartidaNavigation { get; set; } = null!;
}
