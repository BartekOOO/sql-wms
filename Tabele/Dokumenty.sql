IF OBJECT_ID(N'SBD.Dokumenty', N'U') IS NULL
BEGIN
    CREATE TABLE SBD.Dokumenty
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SBD_Dokumenty PRIMARY KEY,

        Numer INT NOT NULL,

        TypDokumentu NVARCHAR(10) NOT NULL,

        DataDokumentu DATETIME2(0) NOT NULL
            CONSTRAINT DF_SBD_Dokumenty_DataDokumentu DEFAULT SYSDATETIME(),

        RokDokumentu AS YEAR(DataDokumentu) PERSISTED,
        MiesiacDokumentu AS MONTH(DataDokumentu) PERSISTED,

        Opis NVARCHAR(500) NULL,

        [Status] NVARCHAR(20) NOT NULL
            CONSTRAINT DF_SBD_Dokumenty_Status DEFAULT N'Szkic',

        MagazynZrodlowyId INT NULL,
        MagazynZrodlowyKod NVARCHAR(50) NULL,
        MagazynZrodlowyNazwa NVARCHAR(200) NULL,

        SektorZrodlowyId INT NULL,
        SektorZrodlowyKod NVARCHAR(50) NULL,
        SektorZrodlowyNazwa NVARCHAR(200) NULL,

        MagazynDocelowyId INT NULL,
        MagazynDocelowyKod NVARCHAR(50) NULL,
        MagazynDocelowyNazwa NVARCHAR(200) NULL,

        SektorDocelowyId INT NULL,
        SektorDocelowyKod NVARCHAR(50) NULL,
        SektorDocelowyNazwa NVARCHAR(200) NULL,

        Seria NVARCHAR(20) DEFAULT(N''),

        NumerDokumentu AS
        (
            TypDokumentu
            + N'-'
            + CONVERT(NVARCHAR(20), Numer)
            + N'/'
            + RIGHT(N'0' + CONVERT(NVARCHAR(2), DATEPART(MONTH, DataDokumentu)), 2)
            + N'/'
            + CONVERT(NVARCHAR(4), DATEPART(YEAR, DataDokumentu))
            + CASE
                WHEN LTRIM(RTRIM(Seria)) <> N''
                    THEN N'/' + Seria
                ELSE N''
              END
        ) PERSISTED,

        DataUtworzenia DATETIME2(0) NOT NULL
            CONSTRAINT DF_SBD_Dokumenty_DataUtworzenia DEFAULT SYSDATETIME(),

        DataModyfikacji DATETIME2(0) NULL,

        OperatorKod NVARCHAR(50) NULL,

        CONSTRAINT UQ_SBD_Dokumenty_Typ_Numer_Rok_Miesiac
            UNIQUE (TypDokumentu, Numer, RokDokumentu, MiesiacDokumentu, Seria),

        CONSTRAINT CK_SBD_Dokumenty_TypDokumentu
            CHECK (TypDokumentu IN (N'PM', N'WM', N'MM')),

        CONSTRAINT CK_SBD_Dokumenty_Status
            CHECK ([Status] IN (N'Szkic', N'Zatwierdzony', N'Anulowany')),

    );
END
GO


IF OBJECT_ID(N'SBD.Dokumenty', N'U') IS NOT NULL
AND OBJECT_ID(N'SBD.Magazyny', N'U') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_SBD_Dokumenty_Magazyny_Zrodlowy'
)
BEGIN
    ALTER TABLE SBD.Dokumenty
    ADD CONSTRAINT FK_SBD_Dokumenty_Magazyny_Zrodlowy
        FOREIGN KEY (MagazynZrodlowyId)
        REFERENCES SBD.Magazyny(Id);
END
GO


IF OBJECT_ID(N'SBD.Dokumenty', N'U') IS NOT NULL
AND OBJECT_ID(N'SBD.Sektory', N'U') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_SBD_Dokumenty_Sektory_Zrodlowy'
)
BEGIN
    ALTER TABLE SBD.Dokumenty
    ADD CONSTRAINT FK_SBD_Dokumenty_Sektory_Zrodlowy
        FOREIGN KEY (SektorZrodlowyId)
        REFERENCES SBD.Sektory(Id);
END
GO


IF OBJECT_ID(N'SBD.Dokumenty', N'U') IS NOT NULL
AND OBJECT_ID(N'SBD.Magazyny', N'U') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_SBD_Dokumenty_Magazyny_Docelowy'
)
BEGIN
    ALTER TABLE SBD.Dokumenty
    ADD CONSTRAINT FK_SBD_Dokumenty_Magazyny_Docelowy
        FOREIGN KEY (MagazynDocelowyId)
        REFERENCES SBD.Magazyny(Id);
END
GO


IF OBJECT_ID(N'SBD.Dokumenty', N'U') IS NOT NULL
AND OBJECT_ID(N'SBD.Sektory', N'U') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_SBD_Dokumenty_Sektory_Docelowy'
)
BEGIN
    ALTER TABLE SBD.Dokumenty
    ADD CONSTRAINT FK_SBD_Dokumenty_Sektory_Docelowy
        FOREIGN KEY (SektorDocelowyId)
        REFERENCES SBD.Sektory(Id);
END
GO

IF OBJECT_ID(N'SBD.Dokumenty', N'U') IS NOT NULL
AND COL_LENGTH(N'SBD.Dokumenty', N'NumerDokumentuSort') IS NULL
BEGIN
    ALTER TABLE SBD.Dokumenty
    ADD NumerDokumentuSort AS
    (
        TypDokumentu
        + N'-'
        + RIGHT(REPLICATE(N'0', 10) + CONVERT(NVARCHAR(10), Numer), 10)
        + N'/'
        + RIGHT(N'0' + CONVERT(NVARCHAR(2), DATEPART(MONTH, DataDokumentu)), 2)
        + N'/'
        + CONVERT(NVARCHAR(4), DATEPART(YEAR, DataDokumentu))
        + CASE
            WHEN LTRIM(RTRIM(Seria)) <> N''
                THEN N'/' + Seria
            ELSE N''
          END
    ) PERSISTED;
END
GO