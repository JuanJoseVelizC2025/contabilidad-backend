CREATE TABLE ActivoFijo (
    IdActivo INT IDENTITY(1,1) PRIMARY KEY,
    Codigo NVARCHAR(20) NOT NULL UNIQUE,
    Descripcion NVARCHAR(200) NOT NULL,
    FechaAdquisicion DATE NOT NULL,
    Costo DECIMAL(18,2) NOT NULL CHECK (Costo > 0),
    ValorResidual DECIMAL(18,2) NOT NULL DEFAULT 0 CHECK (ValorResidual >= 0),
    VidaUtilMeses INT NOT NULL CHECK (VidaUtilMeses > 0),
    IdCuentaActivo INT NOT NULL REFERENCES Cuenta(IdCuenta),
    IdCuentaDepreciacion INT NOT NULL REFERENCES Cuenta(IdCuenta),
    IdCuentaGasto INT NOT NULL REFERENCES Cuenta(IdCuenta),
    Estado NVARCHAR(10) NOT NULL DEFAULT 'Activo' CHECK (Estado IN ('Activo','Baja')),
    FechaBaja DATE NULL,
    IdPartidaBaja INT NULL REFERENCES Partida(IdPartida),
    IdUsuario INT NOT NULL REFERENCES Usuario(IdUsuario)
);

CREATE TABLE DepreciacionActivo (
    IdDepreciacion INT IDENTITY(1,1) PRIMARY KEY,
    IdActivo INT NOT NULL REFERENCES ActivoFijo(IdActivo),
    IdPeriodo INT NOT NULL REFERENCES PeriodoContable(IdPeriodo),
    Monto DECIMAL(18,2) NOT NULL CHECK (Monto > 0),
    IdPartida INT NOT NULL REFERENCES Partida(IdPartida)
);