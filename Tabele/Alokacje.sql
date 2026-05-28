IF OBJECT_ID(N'SBD.Alokacje', N'U') IS NULL
BEGIN
    CREATE TABLE SBD.Alokacje
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SBD_Alokacje PRIMARY KEY,

        DokumentId INT NOT NULL,

        PozycjaId INT NOT NULL,

        DostawaId INT NULL,

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
            CHECK (Ilosc > 0)
    );
END



IF COL_LENGTH(N'SBD.Alokacje', N'Cecha') IS NULL
BEGIN
    ALTER TABLE SBD.Alokacje
    ADD Cecha NVARCHAR(200) NOT NULL DEFAULT(N'');
END
GO
