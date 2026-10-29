CREATE TABLE Tercero (
    IdTercero INT IDENTITY(1,1) PRIMARY KEY,
    Tipo NVARCHAR(10) NOT NULL CHECK (Tipo IN ('Cliente','Proveedor')),
    Nombre NVARCHAR(150) NOT NULL,
    Nit NVARCHAR(20) NULL,
    Direccion NVARCHAR(200) NULL,
    Telefono NVARCHAR(30) NULL,
    Correo NVARCHAR(150) NULL,
    Activo BIT NOT NULL DEFAULT 1
);

CREATE TABLE DocumentoComercial (
    IdDocumento INT IDENTITY(1,1) PRIMARY KEY,
    Tipo NVARCHAR(10) NOT NULL CHECK (Tipo IN ('Venta','Compra')),
    Numero INT NOT NULL,
    Fecha DATE NOT NULL,
    IdTercero INT NOT NULL REFERENCES Tercero(IdTercero),
    Condicion NVARCHAR(10) NOT NULL CHECK (Condicion IN ('Contado','Credito')),
    IdCuentaPago INT NULL REFERENCES Cuenta(IdCuenta),
    Concepto NVARCHAR(250) NULL,
    Subtotal DECIMAL(18,2) NOT NULL,
    Iva DECIMAL(18,2) NOT NULL,
    Total DECIMAL(18,2) NOT NULL,
    IdPartida INT NULL REFERENCES Partida(IdPartida),
    Estado NVARCHAR(12) NOT NULL DEFAULT 'Registrado' CHECK (Estado IN ('Registrado','Anulado')),
    MotivoAnulacion NVARCHAR(250) NULL,
    IdUsuario INT NOT NULL REFERENCES Usuario(IdUsuario),
    CONSTRAINT UQ_Documento_Numero UNIQUE (Tipo, Numero)
);

CREATE TABLE DocumentoLinea (
    IdLinea INT IDENTITY(1,1) PRIMARY KEY,
    IdDocumento INT NOT NULL REFERENCES DocumentoComercial(IdDocumento),
    Descripcion NVARCHAR(200) NOT NULL,
    Cantidad DECIMAL(18,2) NOT NULL CHECK (Cantidad > 0),
    PrecioUnitario DECIMAL(18,2) NOT NULL CHECK (PrecioUnitario > 0),
    Subtotal DECIMAL(18,2) NOT NULL,
    IdCuenta INT NOT NULL REFERENCES Cuenta(IdCuenta)
);

INSERT INTO ParametroSistema (Clave, Valor, Descripcion)
SELECT v.Clave, v.Valor, v.Descripcion
FROM (VALUES
    ('PorcentajeIVA', '12', 'Porcentaje de IVA (Guatemala 12%)'),
    ('CuentaIvaDebito', '2.1.03.01.01', 'Codigo de la cuenta de IVA debito fiscal (ventas)'),
    ('CuentaIvaCredito', '1.1.04.01.01', 'Codigo de la cuenta de IVA credito fiscal (compras)'),
    ('CuentaClientes', '1.1.02.01.01.01', 'Codigo de la cuenta de clientes (ventas al credito)'),
    ('CuentaProveedores', '2.1.01.01.01.01', 'Codigo de la cuenta de proveedores (compras al credito)')
) AS v(Clave, Valor, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM ParametroSistema p WHERE p.Clave = v.Clave);

INSERT INTO Tercero (Tipo, Nombre, Nit, Direccion, Telefono, Correo) VALUES
('Cliente', 'Consumidor final', 'CF', 'Ciudad', NULL, NULL),
('Cliente', 'Distribuidora La Esperanza, S.A.', '1234567-8', 'Zona 4, Guatemala', '2222-1111', 'compras@laesperanza.com'),
('Cliente', 'Servicios Santa Rosa, S.A.', '7654321-0', 'Cuilapa, Santa Rosa', '7888-2222', 'info@serviciossr.com'),
('Proveedor', 'Papelera Centroamericana, S.A.', '2345678-9', 'Zona 12, Guatemala', '2333-4444', 'ventas@papelera.com'),
('Proveedor', 'Tecnologia y Equipos, S.A.', '3456789-0', 'Zona 10, Guatemala', '2444-5555', 'ventas@tecnoequipos.com'),
('Proveedor', 'Inmobiliaria Los Pinos, S.A.', '4567890-1', 'Cuilapa, Santa Rosa', '7888-3333', 'cobros@lospinos.com');