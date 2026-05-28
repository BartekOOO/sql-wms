IF OBJECT_ID(N'SBD.Sektory', N'U') IS NULL
BEGIN
    CREATE TABLE SBD.Sektory
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SBD_Sektory PRIMARY KEY,

        MagazynId INT NOT NULL,

        Kod NVARCHAR(50) NOT NULL,
        Nazwa NVARCHAR(200) NOT NULL,

        Opis NVARCHAR(500) NULL,

        DataUtworzenia DATETIME2(0) NOT NULL
            CONSTRAINT DF_SBD_Sektory_DataUtworzenia DEFAULT SYSDATETIME(),

        DataModyfikacji DATETIME2(0) NULL,

        CONSTRAINT FK_SBD_Sektory_Magazyny
            FOREIGN KEY (MagazynId)
            REFERENCES SBD.Magazyny(Id),

        CONSTRAINT UQ_SBD_Sektory_MagazynId_Kod
            UNIQUE (Kod)
    );
END