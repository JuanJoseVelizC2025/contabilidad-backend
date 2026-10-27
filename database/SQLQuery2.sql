-- Sistema de Contabilidad
USE ContabilidadDB;
GO

-- Roles
INSERT INTO Rol (Nombre, Descripcion) VALUES
('Administrador', 'Gestiona usuarios, parametros y periodos'),
('Contador', 'Registra partidas y consulta reportes'),
('Consulta', 'Solo consulta reportes');

-- Parametros del sistema
INSERT INTO ParametroSistema (Clave, Valor, Descripcion) VALUES
('NombreEmpresa', 'Empresa Demo, S.A.', 'Nombre de la empresa'),
('NIT', '1234567-8', 'NIT de la empresa'),
('Direccion', 'Cuilapa, Santa Rosa', 'Direccion de la empresa'),
('Moneda', 'Quetzal', 'Moneda del sistema'),
('SimboloMoneda', 'Q', 'Simbolo de la moneda');

-- Periodos contables
INSERT INTO PeriodoContable (Anio, Mes, FechaInicio, FechaFin, Estado) VALUES
(2026, 10, '2026-10-01', '2026-10-31', 'Abierto'),
(2026, 11, '2026-11-01', '2026-11-30', 'Abierto'),
(2026, 12, '2026-12-01', '2026-12-31', 'Abierto');

-- Nomenclatura contable base
DECLARE @c TABLE (
    Codigo NVARCHAR(20), Nombre NVARCHAR(150), Tipo NVARCHAR(10),
    Naturaleza NVARCHAR(10), Nivel INT, PadreCodigo NVARCHAR(20), Acepta BIT
);

INSERT INTO @c VALUES
('1',      'ACTIVO',                  'Activo',  'Deudora',   1, NULL,    0),
('1.1',    'ACTIVO CORRIENTE',        'Activo',  'Deudora',   2, '1',     0),
('1.1.01', 'Caja',                    'Activo',  'Deudora',   3, '1.1',   1),
('1.1.02', 'Bancos',                  'Activo',  'Deudora',   3, '1.1',   1),
('1.1.03', 'Clientes',                'Activo',  'Deudora',   3, '1.1',   1),
('1.1.04', 'Inventario de mercaderias','Activo', 'Deudora',   3, '1.1',   1),
('1.1.05', 'IVA credito fiscal',      'Activo',  'Deudora',   3, '1.1',   1),
('1.2',    'ACTIVO NO CORRIENTE',     'Activo',  'Deudora',   2, '1',     0),
('1.2.01', 'Mobiliario y equipo',     'Activo',  'Deudora',   3, '1.2',   1),
('2',      'PASIVO',                  'Pasivo',  'Acreedora', 1, NULL,    0),
('2.1',    'PASIVO CORRIENTE',        'Pasivo',  'Acreedora', 2, '2',     0),
('2.1.01', 'Proveedores',             'Pasivo',  'Acreedora', 3, '2.1',   1),
('2.1.02', 'IVA debito fiscal',       'Pasivo',  'Acreedora', 3, '2.1',   1),
('2.1.03', 'Prestamos bancarios por pagar','Pasivo','Acreedora',3,'2.1',  1),
('3',      'CAPITAL',                 'Capital', 'Acreedora', 1, NULL,    0),
('3.1',    'CAPITAL CONTABLE',        'Capital', 'Acreedora', 2, '3',     0),
('3.1.01', 'Capital social',          'Capital', 'Acreedora', 3, '3.1',   1),
('3.1.02', 'Resultados acumulados',   'Capital', 'Acreedora', 3, '3.1',   1),
('4',      'INGRESOS',                'Ingreso', 'Acreedora', 1, NULL,    0),
('4.1',    'INGRESOS OPERATIVOS',     'Ingreso', 'Acreedora', 2, '4',     0),
('4.1.01', 'Ventas',                  'Ingreso', 'Acreedora', 3, '4.1',   1),
('4.1.02', 'Otros ingresos',          'Ingreso', 'Acreedora', 3, '4.1',   1),
('5',      'COSTOS',                  'Costo',   'Deudora',   1, NULL,    0),
('5.1',    'COSTO DE VENTAS',         'Costo',   'Deudora',   2, '5',     0),
('5.1.01', 'Costo de ventas',         'Costo',   'Deudora',   3, '5.1',   1),
('6',      'GASTOS',                  'Gasto',   'Deudora',   1, NULL,    0),
('6.1',    'GASTOS DE OPERACION',     'Gasto',   'Deudora',   2, '6',     0),
('6.1.01', 'Sueldos y salarios',      'Gasto',   'Deudora',   3, '6.1',   1),
('6.1.02', 'Alquileres',              'Gasto',   'Deudora',   3, '6.1',   1),
('6.1.03', 'Servicios basicos',       'Gasto',   'Deudora',   3, '6.1',   1),
('6.1.04', 'Papeleria y utiles',      'Gasto',   'Deudora',   3, '6.1',   1);

INSERT INTO Cuenta (Codigo, Nombre, Tipo, Naturaleza, Nivel, AceptaMovimientos)
SELECT Codigo, Nombre, Tipo, Naturaleza, Nivel, Acepta FROM @c;

UPDATE c
SET c.IdCuentaPadre = p.IdCuenta
FROM Cuenta c
JOIN @c x ON x.Codigo = c.Codigo
JOIN Cuenta p ON p.Codigo = x.PadreCodigo;
GO

-- Verificacion
SELECT 'Roles' AS Tabla, COUNT(*) AS Filas FROM Rol
UNION ALL SELECT 'Parametros', COUNT(*) FROM ParametroSistema
UNION ALL SELECT 'Periodos', COUNT(*) FROM PeriodoContable
UNION ALL SELECT 'Cuentas', COUNT(*) FROM Cuenta;