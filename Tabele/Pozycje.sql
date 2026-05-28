IF OBJECT_ID(N'SBD.Pozycje', N'U') IS NULL
BEGIN
    CREATE TABLE SBD.Pozycje
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SBD_Pozycje PRIMARY KEY,

        DokumentId INT NOT NULL,

        TowarId INT NOT NULL,
        TowarKod NVARCHAR(50) NULL,
        TowarNazwa NVARCHAR(200) NULL,

        JednostkaId INT NOT NULL,
        JednostkaKod NVARCHAR(20) NULL,
        JednostkaPrzelicznik DECIMAL(18,6) NULL,

        Ilosc DECIMAL(18,6) NOT NULL,

        DataUtworzenia DATETIME2(0) NOT NULL
            CONSTRAINT DF_SBD_Pozycje_DataUtworzenia DEFAULT SYSDATETIME(),

        DataModyfikacji DATETIME2(0) NULL,

        CONSTRAINT FK_SBD_Pozycje_Dokumenty
            FOREIGN KEY (DokumentId)
            REFERENCES SBD.Dokumenty(Id),

        CONSTRAINT FK_SBD_Pozycje_Towary
            FOREIGN KEY (TowarId)
            REFERENCES SBD.Towary(Id),

        CONSTRAINT FK_SBD_Pozycje_Jednostki
            FOREIGN KEY (JednostkaId)
            REFERENCES SBD.Jednostki(Id),

        CONSTRAINT CK_SBD_Pozycje_Ilosc
            CHECK (Ilosc > 0),


        CONSTRAINT CK_SBD_Pozycje_JednostkaPrzelicznik
            CHECK (JednostkaPrzelicznik IS NULL OR JednostkaPrzelicznik > 0)
    );
END