IF OBJECT_ID(N'SBD.Magazyny', N'U') IS NULL
BEGIN
    CREATE TABLE SBD.Magazyny
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SBD_Magazyny PRIMARY KEY,

        Kod NVARCHAR(50) NOT NULL,
        Nazwa NVARCHAR(200) NOT NULL,

        AdresId INT NOT NULL,

        Opis NVARCHAR(500) NULL,

        DataUtworzenia DATETIME2(0) NOT NULL
            CONSTRAINT DF_SBD_Magazyny_DataUtworzenia DEFAULT SYSDATETIME(),

        DataModyfikacji DATETIME2(0) NULL,

        CONSTRAINT FK_SBD_Magazyny_Adresy
            FOREIGN KEY (AdresId)
            REFERENCES SBD.Adresy(Id),

        CONSTRAINT UQ_SBD_Magazyny_Kod
            UNIQUE (Kod)
    );
END