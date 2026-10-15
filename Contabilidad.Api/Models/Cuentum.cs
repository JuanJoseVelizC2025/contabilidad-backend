using System;
using System.Collections.Generic;

namespace Contabilidad.Api.Models;

public partial class Cuentum
{
    public int IdCuenta { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string Tipo { get; set; } = null!;

    public string Naturaleza { get; set; } = null!;

    public int Nivel { get; set; }

    public int? IdCuentaPadre { get; set; }

    public bool AceptaMovimientos { get; set; }

    public bool Activa { get; set; }

    public virtual Cuentum? IdCuentaPadreNavigation { get; set; }

    public virtual ICollection<Cuentum> InverseIdCuentaPadreNavigation { get; set; } = new List<Cuentum>();

    public virtual ICollection<PartidaDetalle> PartidaDetalles { get; set; } = new List<PartidaDetalle>();
}
