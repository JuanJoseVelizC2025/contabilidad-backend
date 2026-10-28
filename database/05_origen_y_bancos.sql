CREATE TABLE PartidaOrigen (
    IdPartida INT NOT NULL PRIMARY KEY REFERENCES Partida(IdPartida),
    Origen NVARCHAR(20) NOT NULL,
    IdOrigen INT NULL
);

CREATE TABLE CuentaBancaria (
    IdCuentaBancaria INT IDENTITY(1,1) PRIMARY KEY,
    Banco NVARCHAR(80) NOT NULL,
    NumeroCuenta NVARCHAR(40) NOT NULL,
    TipoCuenta NVARCHAR(20) NOT NULL CHECK (TipoCuenta IN ('Monetaria','Ahorro')),
    IdCuenta INT NOT NULL UNIQUE REFERENCES Cuenta(IdCuenta),
    Activa BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_CuentaBancaria_Numero UNIQUE (Banco, NumeroCuenta)
);

CREATE TABLE MovimientoBancario (
    IdMovimiento INT IDENTITY(1,1) PRIMARY KEY,
    IdCuentaBancaria INT NOT NULL REFERENCES CuentaBancaria(IdCuentaBancaria),
    Fecha DATE NOT NULL,
    Tipo NVARCHAR(20) NOT NULL CHECK (Tipo IN ('Deposito','Retiro','Cheque','NotaDebito','NotaCredito','Transferencia')),
    Monto DECIMAL(18,2) NOT NULL CHECK (Monto > 0),
    Referencia NVARCHAR(60) NULL,
    Concepto NVARCHAR(250) NOT NULL,
    IdCuentaContrapartida INT NULL REFERENCES Cuenta(IdCuenta),
    IdCuentaBancariaDestino INT NULL REFERENCES CuentaBancaria(IdCuentaBancaria),
    IdPartida INT NULL REFERENCES Partida(IdPartida),
    Estado NVARCHAR(12) NOT NULL DEFAULT 'Registrado' CHECK (Estado IN ('Registrado','Anulado')),
    Conciliado BIT NOT NULL DEFAULT 0,
    IdUsuario INT NOT NULL REFERENCES Usuario(IdUsuario)
);