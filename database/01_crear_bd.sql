-- Sistema de Contabilidad | Desarrollo Web UMG
IF DB_ID('ContabilidadDB') IS NULL
    CREATE DATABASE ContabilidadDB;
GO
USE ContabilidadDB;
GO

CREATE TABLE Rol (
    IdRol INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(50) NOT NULL UNIQUE,
    Descripcion NVARCHAR(200) NULL
);

CREATE TABLE Usuario (
    IdUsuario INT IDENTITY(1,1) PRIMARY KEY,
    NombreUsuario NVARCHAR(50) NOT NULL UNIQUE,
    ClaveHash NVARCHAR(200) NOT NULL,
    NombreCompleto NVARCHAR(150) NOT NULL,
    Correo NVARCHAR(150) NULL,
    IdRol INT NOT NULL REFERENCES Rol(IdRol),
    Activo BIT NOT NULL DEFAULT 1
);

CREATE TABLE ParametroSistema (
    IdParametro INT IDENTITY(1,1) PRIMARY KEY,
    Clave NVARCHAR(50) NOT NULL UNIQUE,
    Valor NVARCHAR(200) NOT NULL,
    Descripcion NVARCHAR(200) NULL
);

CREATE TABLE PeriodoContable (
    IdPeriodo INT IDENTITY(1,1) PRIMARY KEY,
    Anio INT NOT NULL,
    Mes INT NOT NULL CHECK (Mes BETWEEN 1 AND 12),
    FechaInicio DATE NOT NULL,
    FechaFin DATE NOT NULL,
    Estado NVARCHAR(10) NOT NULL DEFAULT 'Abierto' CHECK (Estado IN ('Abierto','Cerrado')),
    CONSTRAINT UQ_Periodo UNIQUE (Anio, Mes)
);

CREATE TABLE Cuenta (
    IdCuenta INT IDENTITY(1,1) PRIMARY KEY,
    Codigo NVARCHAR(20) NOT NULL UNIQUE,
    Nombre NVARCHAR(150) NOT NULL,
    Tipo NVARCHAR(10) NOT NULL CHECK (Tipo IN ('Activo','Pasivo','Capital','Ingreso','Costo','Gasto')),
    Naturaleza NVARCHAR(10) NOT NULL CHECK (Naturaleza IN ('Deudora','Acreedora')),
    Nivel INT NOT NULL,
    IdCuentaPadre INT NULL REFERENCES Cuenta(IdCuenta),
    AceptaMovimientos BIT NOT NULL DEFAULT 0,
    Activa BIT NOT NULL DEFAULT 1
);

CREATE TABLE Partida (
    IdPartida INT IDENTITY(1,1) PRIMARY KEY,
    Numero INT NOT NULL,
    Fecha DATE NOT NULL,
    Concepto NVARCHAR(250) NOT NULL,
    IdPeriodo INT NOT NULL REFERENCES PeriodoContable(IdPeriodo),
    IdUsuario INT NOT NULL REFERENCES Usuario(IdUsuario),
    Estado NVARCHAR(12) NOT NULL DEFAULT 'Registrada' CHECK (Estado IN ('Registrada','Anulada')),
    MotivoAnulacion NVARCHAR(250) NULL,
    CONSTRAINT UQ_Partida_Numero UNIQUE (IdPeriodo, Numero)
);

CREATE TABLE PartidaDetalle (
    IdDetalle INT IDENTITY(1,1) PRIMARY KEY,
    IdPartida INT NOT NULL REFERENCES Partida(IdPartida),
    IdCuenta INT NOT NULL REFERENCES Cuenta(IdCuenta),
    Debe DECIMAL(18,2) NOT NULL DEFAULT 0 CHECK (Debe >= 0),
    Haber DECIMAL(18,2) NOT NULL DEFAULT 0 CHECK (Haber >= 0),
    CONSTRAINT CK_Debe_O_Haber CHECK ((Debe > 0 AND Haber = 0) OR (Haber > 0 AND Debe = 0))
);
GO