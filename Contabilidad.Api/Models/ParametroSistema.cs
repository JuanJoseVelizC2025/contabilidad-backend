using System;
using System.Collections.Generic;

namespace Contabilidad.Api.Models;

public partial class ParametroSistema
{
    public int IdParametro { get; set; }

    public string Clave { get; set; } = null!;

    public string Valor { get; set; } = null!;

    public string? Descripcion { get; set; }
}
