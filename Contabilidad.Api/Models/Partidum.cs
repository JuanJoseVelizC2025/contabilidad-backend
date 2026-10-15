using System;
using System.Collections.Generic;

namespace Contabilidad.Api.Models;

public partial class Partidum
{
    public int IdPartida { get; set; }

    public int Numero { get; set; }

    public DateOnly Fecha { get; set; }

    public string Concepto { get; set; } = null!;

    public int IdPeriodo { get; set; }

    public int IdUsuario { get; set; }

    public string Estado { get; set; } = null!;

    public string? MotivoAnulacion { get; set; }

    public virtual PeriodoContable IdPeriodoNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;

    public virtual ICollection<PartidaDetalle> PartidaDetalles { get; set; } = new List<PartidaDetalle>();
}
