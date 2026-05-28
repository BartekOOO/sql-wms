IF OBJECT_ID(N'SBD.Towary', N'U') IS NULL
BEGIN
    CREATE TABLE SBD.Towary
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SBD_Towary PRIMARY KEY,

        Kod NVARCHAR(50) NOT NULL,
        Nazwa NVARCHAR(200) NOT NULL,

        Opis NVARCHAR(500) NULL,

        KodKreskowy NVARCHAR(100) NULL,

        DataUtworzenia DATETIME2(0) NOT NULL
            CONSTRAINT DF_SBD_Towary_DataUtworzenia DEFAULT SYSDATETIME(),

        DataModyfikacji DATETIME2(0) NULL,

        CONSTRAINT UQ_SBD_Towary_Kod
            UNIQUE (Kod)
    );
END