IF OBJECT_ID(N'SBD.Dostawy', N'U') IS NULL
BEGIN
    CREATE TABLE SBD.Dostawy
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SBD_Dostawy PRIMARY KEY,

        TowarId INT NOT NULL,
        TowarKod NVARCHAR(50) NULL,
        TowarNazwa NVARCHAR(200) NULL,

        MagazynId INT NOT NULL,

        SektorId INT NULL,

        ZakladajacaPozycjaId INT NOT NULL,

        Ilosc DECIMAL(18,6) NOT NULL,

        DataUtworzenia DATETIME2(0) NOT NULL
            CONSTRAINT DF_SBD_Dostawy_DataUtworzenia DEFAULT SYSDATETIME(),

        DataModyfikacji DATETIME2(0) NULL,

        CONSTRAINT FK_SBD_Dostawy_Towary
            FOREIGN KEY (TowarId)
            REFERENCES SBD.Towary(Id),

        CONSTRAINT FK_SBD_Dostawy_Magazyny
            FOREIGN KEY (MagazynId)
            REFERENCES SBD.Magazyny(Id),

        CONSTRAINT FK_SBD_Dostawy_Sektory
            FOREIGN KEY (SektorId)
            REFERENCES SBD.Sektory(Id),

        CONSTRAINT FK_SBD_Dostawy_Pozycje_Zakladajaca
            FOREIGN KEY (ZakladajacaPozycjaId)
            REFERENCES SBD.Pozycje(Id),

        CONSTRAINT CK_SBD_Dostawy_Ilosc
            CHECK (Ilosc >= 0),

    );
END


IF COL_LENGTH(N'SBD.Dostawy', N'Cecha') IS NULL
BEGIN
    ALTER TABLE SBD.Dostawy
    ADD Cecha NVARCHAR(200) NOT NULL DEFAULT(N'');
END
GO


IF OBJECT_ID(N'SBD.Dostawy', N'U') IS NOT NULL
AND COL_LENGTH(N'SBD.Dostawy', N'ZakladajacaAlokacjaId') IS NULL
BEGIN
    ALTER TABLE SBD.Dostawy
    ADD ZakladajacaAlokacjaId INT NULL;
END
GO

IF OBJECT_ID(N'SBD.Dostawy', N'U') IS NOT NULL
AND OBJECT_ID(N'SBD.Alokacje', N'U') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_SBD_Dostawy_Alokacje_Zakladajaca'
)
BEGIN
    ALTER TABLE SBD.Dostawy
    ADD CONSTRAINT FK_SBD_Dostawy_Alokacje_Zakladajaca
        FOREIGN KEY (ZakladajacaAlokacjaId)
        REFERENCES SBD.Alokacje(Id);
END
GO


IF OBJECT_ID(N'SBD.Dostawy', N'U') IS NOT NULL
AND COL_LENGTH(N'SBD.Dostawy', N'ZrodlowaAlokacjaId') IS NULL
BEGIN
    ALTER TABLE SBD.Dostawy
    ADD ZrodlowaAlokacjaId INT NULL;
END
GO