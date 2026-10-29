SET XACT_ABORT ON;
BEGIN TRANSACTION;

DROP TABLE IF EXISTS #asiento, #linea, #mov, #doc;

DECLARE @u INT = (SELECT TOP 1 IdUsuario FROM Usuario WHERE NombreUsuario = 'admin');
DECLARE @p INT = (SELECT TOP 1 IdPeriodo FROM PeriodoContable
                  WHERE FechaInicio <= '2026-10-15' AND FechaFin >= '2026-10-15' AND Estado = 'Abierto');

IF @u IS NULL OR @p IS NULL
BEGIN
    RAISERROR('Falta el usuario admin o el periodo abierto de octubre 2026', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

-- 1) Limpiar datos transaccionales
DELETE FROM DocumentoLinea;
DELETE FROM DocumentoComercial;
DELETE FROM MovimientoBancario;
DELETE FROM CuentaBancaria;
DELETE FROM PartidaOrigen;
DELETE FROM PartidaDetalle;
DELETE FROM Partida;

-- 2) Partidas (encabezado)
CREATE TABLE #asiento (Numero INT PRIMARY KEY, Fecha DATE, Concepto NVARCHAR(250), Origen NVARCHAR(20));
INSERT INTO #asiento VALUES
(1,  '2026-10-01', N'Depósito bancario - Aporte inicial de capital', 'Banco'),
(2,  '2026-10-04', N'Retiro bancario - Fondo de caja chica', 'Banco'),
(3,  '2026-10-06', N'Factura de compra No. 0001 - Papelera Centroamericana, S.A.', 'Compra'),
(4,  '2026-10-07', N'Factura de venta No. 0001 - Consumidor final', 'Venta'),
(5,  '2026-10-08', N'Factura de compra No. 0002 - Tecnologia y Equipos, S.A.', 'Compra'),
(6,  '2026-10-09', N'Pago de sueldos del mes de octubre', 'Manual'),
(7,  '2026-10-10', N'Factura de compra No. 0003 - Inmobiliaria Los Pinos, S.A.', 'Compra'),
(8,  '2026-10-12', N'Factura de compra No. 0004 - Papelera Centroamericana, S.A.', 'Compra'),
(9,  '2026-10-14', N'Factura de venta No. 0002 - Distribuidora La Esperanza, S.A.', 'Venta'),
(10, '2026-10-15', N'Transferencia entre cuentas - Fondeo de cuenta BAC', 'Banco'),
(11, '2026-10-16', N'Pago de energía eléctrica', 'Manual'),
(12, '2026-10-18', N'Depósito bancario - Cobro factura de venta No. 0002', 'Banco'),
(13, '2026-10-20', N'Factura de venta No. 0003 - Servicios Santa Rosa, S.A.', 'Venta'),
(14, '2026-10-22', N'Factura de venta No. 0004 - Distribuidora La Esperanza, S.A.', 'Venta'),
(15, '2026-10-24', N'Cheque emitido - Pago factura de compra No. 0002', 'Banco'),
(16, '2026-10-25', N'Costo de ventas del mes de octubre', 'Manual'),
(17, '2026-10-26', N'Nota de débito - Comisión bancaria', 'Banco'),
(18, '2026-10-27', N'Nota de crédito - Intereses ganados', 'Banco');

-- 3) Partidas (detalle)
CREATE TABLE #linea (Numero INT, Codigo NVARCHAR(30), Debe DECIMAL(18,2), Haber DECIMAL(18,2));
INSERT INTO #linea VALUES
(1,  '1.1.01.02.01.01', 80000, 0), (1,  '3.1.01.01', 0, 80000),
(2,  '1.1.01.01.02', 1000, 0),     (2,  '1.1.01.02.01.01', 0, 1000),
(3,  '6.1.01.04.01', 750, 0),      (3,  '1.1.04.01.01', 90, 0),      (3,  '1.1.01.02.01.01', 0, 840),
(4,  '1.1.01.01.01', 3360, 0),     (4,  '4.1.01.01.01', 0, 3000),    (4,  '2.1.03.01.01', 0, 360),
(5,  '1.2.01.02.01', 9000, 0),     (5,  '1.1.04.01.01', 1080, 0),    (5,  '2.1.01.01.01.01', 0, 10080),
(6,  '6.1.01.01.01', 6000, 0),     (6,  '1.1.01.02.01.01', 0, 6000),
(7,  '6.1.01.03.01', 3500, 0),     (7,  '1.1.04.01.01', 420, 0),     (7,  '1.1.01.02.01.01', 0, 3920),
(8,  '1.1.03.01.01', 5000, 0),     (8,  '1.1.04.01.01', 600, 0),     (8,  '2.1.01.01.01.01', 0, 5600),
(9,  '1.1.02.01.01.01', 8960, 0),  (9,  '4.1.01.02.01', 0, 8000),    (9,  '2.1.03.01.01', 0, 960),
(10, '1.1.01.02.02.01', 15000, 0), (10, '1.1.01.02.01.01', 0, 15000),
(11, '6.1.01.02.01', 450, 0),      (11, '1.1.01.01.01', 0, 450),
(12, '1.1.01.02.01.01', 8960, 0),  (12, '1.1.02.01.01.01', 0, 8960),
(13, '1.1.02.01.01.01', 5600, 0),  (13, '4.1.01.01.02', 0, 5000),    (13, '2.1.03.01.01', 0, 600),
(14, '1.1.01.02.02.01', 2800, 0),  (14, '4.1.01.02.02', 0, 2500),    (14, '2.1.03.01.01', 0, 300),
(15, '2.1.01.01.01.01', 10080, 0), (15, '1.1.01.02.01.01', 0, 10080),
(16, '5.1.01.01', 3500, 0),        (16, '1.1.03.01.01', 0, 3500),
(17, '6.2.01.02', 75, 0),          (17, '1.1.01.02.01.01', 0, 75),
(18, '1.1.01.02.02.01', 40, 0),    (18, '4.2.01.01', 0, 40);

INSERT INTO Partida (Numero, Fecha, Concepto, IdPeriodo, IdUsuario, Estado)
SELECT Numero, Fecha, Concepto, @p, @u, 'Registrada' FROM #asiento ORDER BY Numero;

INSERT INTO PartidaDetalle (IdPartida, IdCuenta, Debe, Haber)
SELECT p.IdPartida, c.IdCuenta, l.Debe, l.Haber
FROM #linea l
JOIN Partida p ON p.Numero = l.Numero AND p.IdPeriodo = @p
JOIN Cuenta c ON c.Codigo = l.Codigo;

IF (SELECT COUNT(*) FROM PartidaDetalle) <> (SELECT COUNT(*) FROM #linea)
BEGIN
    RAISERROR('Alguna cuenta del catalogo no existe: ejecute primero el script 04', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

INSERT INTO PartidaOrigen (IdPartida, Origen, IdOrigen)
SELECT p.IdPartida, a.Origen, NULL
FROM #asiento a JOIN Partida p ON p.Numero = a.Numero AND p.IdPeriodo = @p;

-- 4) Cuentas bancarias y movimientos
INSERT INTO CuentaBancaria (Banco, NumeroCuenta, TipoCuenta, IdCuenta, Activa)
SELECT 'Banco Industrial', '123-456789-0', 'Monetaria', IdCuenta, 1 FROM Cuenta WHERE Codigo = '1.1.01.02.01.01'
UNION ALL
SELECT 'Banco BAC Credomatic', '123-456710-0', 'Monetaria', IdCuenta, 1 FROM Cuenta WHERE Codigo = '1.1.01.02.02.01';

CREATE TABLE #mov (Numero INT, Banco NVARCHAR(80), Fecha DATE, Tipo NVARCHAR(20), Monto DECIMAL(18,2),
                   Referencia NVARCHAR(60), Concepto NVARCHAR(250), CodContra NVARCHAR(30),
                   BancoDestino NVARCHAR(80), Conciliado BIT);
INSERT INTO #mov VALUES
(1,  'Banco Industrial', '2026-10-01', 'Deposito', 80000, 'BOL-0001', N'Aporte inicial de capital', '3.1.01.01', NULL, 1),
(2,  'Banco Industrial', '2026-10-04', 'Retiro', 1000, 'RET-0001', N'Fondo de caja chica', '1.1.01.01.02', NULL, 1),
(10, 'Banco Industrial', '2026-10-15', 'Transferencia', 15000, 'TRF-0001', N'Fondeo de cuenta BAC', NULL, 'Banco BAC Credomatic', 1),
(12, 'Banco Industrial', '2026-10-18', 'Deposito', 8960, 'BOL-0002', N'Cobro factura de venta No. 0002', '1.1.02.01.01.01', NULL, 0),
(15, 'Banco Industrial', '2026-10-24', 'Cheque', 10080, 'CHQ-0001', N'Pago factura de compra No. 0002', '2.1.01.01.01.01', NULL, 0),
(17, 'Banco Industrial', '2026-10-26', 'NotaDebito', 75, 'ND-0001', N'Comisión bancaria', '6.2.01.02', NULL, 0),
(18, 'Banco BAC Credomatic', '2026-10-27', 'NotaCredito', 40, 'NC-0001', N'Intereses ganados', '4.2.01.01', NULL, 0);

INSERT INTO MovimientoBancario (IdCuentaBancaria, Fecha, Tipo, Monto, Referencia, Concepto,
                                IdCuentaContrapartida, IdCuentaBancariaDestino, IdPartida, Estado, Conciliado, IdUsuario)
SELECT cb.IdCuentaBancaria, m.Fecha, m.Tipo, m.Monto, m.Referencia, m.Concepto,
       (SELECT IdCuenta FROM Cuenta WHERE Codigo = m.CodContra),
       (SELECT IdCuentaBancaria FROM CuentaBancaria WHERE Banco = m.BancoDestino),
       p.IdPartida, 'Registrado', m.Conciliado, @u
FROM #mov m
JOIN CuentaBancaria cb ON cb.Banco = m.Banco
JOIN Partida p ON p.Numero = m.Numero AND p.IdPeriodo = @p;

UPDATE o SET o.IdOrigen = m.IdMovimiento
FROM PartidaOrigen o JOIN MovimientoBancario m ON m.IdPartida = o.IdPartida;

-- 5) Facturas de venta y de compra (una linea por factura)
CREATE TABLE #doc (Tipo NVARCHAR(10), Numero INT, Fecha DATE, Tercero NVARCHAR(150), Condicion NVARCHAR(10),
                   CodPago NVARCHAR(30), Subtotal DECIMAL(18,2), Iva DECIMAL(18,2), Total DECIMAL(18,2),
                   NumPartida INT, Descripcion NVARCHAR(200), Cantidad DECIMAL(18,2), Precio DECIMAL(18,2),
                   CodCuenta NVARCHAR(30));
INSERT INTO #doc VALUES
('Compra', 1, '2026-10-06', 'Papelera Centroamericana, S.A.', 'Contado', '1.1.01.02.01.01', 750, 90, 840, 3, N'Resmas de papel y tintas', 5, 150, '6.1.01.04.01'),
('Compra', 2, '2026-10-08', 'Tecnologia y Equipos, S.A.', 'Credito', NULL, 9000, 1080, 10080, 5, N'Computadora de escritorio', 2, 4500, '1.2.01.02.01'),
('Compra', 3, '2026-10-10', 'Inmobiliaria Los Pinos, S.A.', 'Contado', '1.1.01.02.01.01', 3500, 420, 3920, 7, N'Alquiler de local, octubre', 1, 3500, '6.1.01.03.01'),
('Compra', 4, '2026-10-12', 'Papelera Centroamericana, S.A.', 'Credito', NULL, 5000, 600, 5600, 8, N'Mercadería para reventa', 40, 125, '1.1.03.01.01'),
('Venta',  1, '2026-10-07', 'Consumidor final', 'Contado', '1.1.01.01.01', 3000, 360, 3360, 4, N'Venta de mercadería', 10, 300, '4.1.01.01.01'),
('Venta',  2, '2026-10-14', 'Distribuidora La Esperanza, S.A.', 'Credito', NULL, 8000, 960, 8960, 9, N'Servicio de consultoría', 1, 8000, '4.1.01.02.01'),
('Venta',  3, '2026-10-20', 'Servicios Santa Rosa, S.A.', 'Credito', NULL, 5000, 600, 5600, 13, N'Venta de mercadería', 20, 250, '4.1.01.01.02'),
('Venta',  4, '2026-10-22', 'Distribuidora La Esperanza, S.A.', 'Contado', '1.1.01.02.02.01', 2500, 300, 2800, 14, N'Soporte técnico', 1, 2500, '4.1.01.02.02');

INSERT INTO DocumentoComercial (Tipo, Numero, Fecha, IdTercero, Condicion, IdCuentaPago, Concepto,
                                Subtotal, Iva, Total, IdPartida, Estado, IdUsuario)
SELECT d.Tipo, d.Numero, d.Fecha,
       (SELECT TOP 1 IdTercero FROM Tercero
         WHERE Nombre = d.Tercero AND Tipo = CASE d.Tipo WHEN 'Venta' THEN 'Cliente' ELSE 'Proveedor' END),
       d.Condicion,
       (SELECT IdCuenta FROM Cuenta WHERE Codigo = d.CodPago),
       NULL, d.Subtotal, d.Iva, d.Total, p.IdPartida, 'Registrado', @u
FROM #doc d JOIN Partida p ON p.Numero = d.NumPartida AND p.IdPeriodo = @p;

INSERT INTO DocumentoLinea (IdDocumento, Descripcion, Cantidad, PrecioUnitario, Subtotal, IdCuenta)
SELECT dc.IdDocumento, d.Descripcion, d.Cantidad, d.Precio, d.Cantidad * d.Precio,
       (SELECT IdCuenta FROM Cuenta WHERE Codigo = d.CodCuenta)
FROM #doc d JOIN DocumentoComercial dc ON dc.Tipo = d.Tipo AND dc.Numero = d.Numero;

UPDATE o SET o.IdOrigen = dc.IdDocumento
FROM PartidaOrigen o JOIN DocumentoComercial dc ON dc.IdPartida = o.IdPartida;

COMMIT TRANSACTION;

-- 6) Verificacion
SELECT (SELECT COUNT(*) FROM Partida) AS Partidas,
       (SELECT COUNT(*) FROM PartidaDetalle) AS Detalles,
       (SELECT COUNT(*) FROM MovimientoBancario) AS Movimientos,
       (SELECT COUNT(*) FROM DocumentoComercial) AS Facturas;
SELECT SUM(Debe) AS TotalDebe, SUM(Haber) AS TotalHaber FROM PartidaDetalle;
SELECT Origen, COUNT(*) AS Cantidad FROM PartidaOrigen GROUP BY Origen;