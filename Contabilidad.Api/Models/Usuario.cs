using System;
using System.Collections.Generic;

namespace Contabilidad.Api.Models;

public partial class Usuario
{
    public int IdUsuario { get; set; }

    public string NombreUsuario { get; set; } = null!;

    public string ClaveHash { get; set; } = null!;

    public string NombreCompleto { get; set; } = null!;

    public string? Correo { get; set; }

    public int IdRol { get; set; }

    public bool Activo { get; set; }

    public virtual Rol IdRolNavigation { get; set; } = null!;

    public virtual ICollection<Partida> Partida { get; set; } = new List<Partida>();
}
