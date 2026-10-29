CREATE TABLE CodigoVerificacion (
    IdCodigo INT IDENTITY(1,1) PRIMARY KEY,
    IdUsuario INT NOT NULL REFERENCES Usuario(IdUsuario),
    Proposito NVARCHAR(20) NOT NULL CHECK (Proposito IN ('Recuperacion','InicioSesion')),
    CodigoHash NVARCHAR(100) NOT NULL,
    Expira DATETIME2 NOT NULL,
    Usado BIT NOT NULL DEFAULT 0,
    Intentos INT NOT NULL DEFAULT 0,
    Creado DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Codigo_Usuario ON CodigoVerificacion(IdUsuario, Proposito, Creado DESC);

IF NOT EXISTS (SELECT 1 FROM ParametroSistema WHERE Clave = 'DobleAutenticacion')
    INSERT INTO ParametroSistema (Clave, Valor, Descripcion)
    VALUES ('DobleAutenticacion', 'No', 'Exigir codigo por correo al iniciar sesion (Si o No)');