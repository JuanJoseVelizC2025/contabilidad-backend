using System;
using System.Collections.Generic;

namespace Contabilidad.Api.Models;

public partial class PeriodoContable
{
    public int IdPeriodo { get; set; }

    public int Anio { get; set; }

    public int Mes { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly FechaFin { get; set; }

    public string Estado { get; set; } = null!;

    public virtual ICollection<Partida> Partida { get; set; } = new List<Partida>();
}
