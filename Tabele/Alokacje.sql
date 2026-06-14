IF OBJECT_ID(N'SBD.Alokacje', N'U') IS NULL
BEGIN
    CREATE TABLE SBD.Alokacje
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SBD_Alokacje PRIMARY KEY,

        DokumentId INT NOT NULL,

        PozycjaId INT NOT NULL,

        DostawaId INT NULL,

		Kierunek NVARCHAR(50) NOT NULL DEFAULT(N'Przychód'),

        Ilosc DECIMAL(18,6) NOT NULL,

        DataUtworzenia DATETIME2(0) NOT NULL
            CONSTRAINT DF_SBD_Alokacje_DataUtworzenia DEFAULT SYSDATETIME(),

        CONSTRAINT FK_SBD_Alokacje_Dokumenty
            FOREIGN KEY (DokumentId)
            REFERENCES SBD.Dokumenty(Id),

        CONSTRAINT FK_SBD_Alokacje_Pozycje
            FOREIGN KEY (PozycjaId)
            REFERENCES SBD.Pozycje(Id),

        CONSTRAINT FK_SBD_Alokacje_Dostawy
            FOREIGN KEY (DostawaId)
            REFERENCES SBD.Dostawy(Id),

        CONSTRAINT CK_SBD_Alokacje_Ilosc
            CHECK (Ilosc > 0),

		CONSTRAINT CK_SBD_Dostawy_Kierunek
            CHECK (Kierunek IN (N'Przychód', N'Rozchód'))
    );
END



IF COL_LENGTH(N'SBD.Alokacje', N'Cecha') IS NULL
BEGIN
    ALTER TABLE SBD.Alokacje
    ADD Cecha NVARCHAR(200) NOT NULL DEFAULT(N'');
END
GO


IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_SBD_Dostawy_Kierunek'
      AND parent_object_id = OBJECT_ID(N'SBD.Alokacje')
)
BEGIN
    ALTER TABLE SBD.Alokacje
    DROP CONSTRAINT CK_SBD_Dostawy_Kierunek;
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c 
        ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'SBD.Alokacje')
      AND c.name = N'Kierunek'
)
BEGIN
    DECLARE @sql NVARCHAR(MAX);

    SELECT @sql = N'ALTER TABLE SBD.Alokacje DROP CONSTRAINT ' + QUOTENAME(dc.name)
    FROM sys.default_constraints dc
    JOIN sys.columns c 
        ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'SBD.Alokacje')
      AND c.name = N'Kierunek';

    EXEC sp_executesql @sql;
END
GO

ALTER TABLE SBD.Alokacje
ADD CONSTRAINT DF_SBD_Alokacje_Kierunek
DEFAULT(N'Szkic') FOR Kierunek;
GO

ALTER TABLE SBD.Alokacje
ADD CONSTRAINT CK_SBD_Alokacje_Kierunek
CHECK (Kierunek IN (N'Szkic', N'Przychód', N'Rozchód'));
GO


IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_SBD_Alokacje_Ilosc'
      AND parent_object_id = OBJECT_ID(N'SBD.Alokacje')
)
BEGIN
    ALTER TABLE SBD.Alokacje
    DROP CONSTRAINT CK_SBD_Alokacje_Ilosc;
END
GO

ALTER TABLE SBD.Alokacje
ADD CONSTRAINT CK_SBD_Alokacje_Ilosc
CHECK (Ilosc >= 0);
GO