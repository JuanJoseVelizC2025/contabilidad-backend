using System;
using System.Collections.Generic;

namespace Contabilidad.Api.Models;

public partial class Cuenta
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

    public virtual Cuenta? IdCuentaPadreNavigation { get; set; }

    public virtual ICollection<Cuenta> InverseIdCuentaPadreNavigation { get; set; } = new List<Cuenta>();

    public virtual ICollection<PartidaDetalle> PartidaDetalle { get; set; } = new List<PartidaDetalle>();
}
