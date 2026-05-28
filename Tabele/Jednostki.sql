IF OBJECT_ID(N'SBD.Jednostki', N'U') IS NULL
BEGIN
    CREATE TABLE SBD.Jednostki
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SBD_Jednostki PRIMARY KEY,

        TowarId INT NOT NULL,

        Kod NVARCHAR(20) NOT NULL,
        Nazwa NVARCHAR(100) NOT NULL,

        Przelicznik DECIMAL(18,6) NOT NULL,

        DataUtworzenia DATETIME2(0) NOT NULL
            CONSTRAINT DF_SBD_Jednostki_DataUtworzenia DEFAULT SYSDATETIME(),

        DataModyfikacji DATETIME2(0) NULL,

        CONSTRAINT FK_SBD_Jednostki_Towary
            FOREIGN KEY (TowarId)
            REFERENCES SBD.Towary(Id),

        CONSTRAINT CK_SBD_Jednostki_Przelicznik
            CHECK (Przelicznik > 0),

        CONSTRAINT UQ_SBD_Jednostki_TowarId_Kod
            UNIQUE (TowarId, Kod)
    );
END
