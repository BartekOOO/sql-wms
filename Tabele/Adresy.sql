 IF OBJECT_ID(N'SBD.Adresy', N'U') IS NULL
BEGIN
	CREATE TABLE SBD.Adresy
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SBD_Adresy PRIMARY KEY,

        Kraj NVARCHAR(100) NULL,
        KodKraju NVARCHAR(10) NULL,

        Wojewodztwo NVARCHAR(100) NULL,
        Powiat NVARCHAR(100) NULL,
        Gmina NVARCHAR(100) NULL,

        Miejscowosc NVARCHAR(150) NOT NULL,
        KodPocztowy NVARCHAR(20) NULL,
        Poczta NVARCHAR(150) NULL,

        Ulica NVARCHAR(150) NULL,
        NumerDomu NVARCHAR(30) NULL,
        NumerLokalu NVARCHAR(30) NULL,

        AdresPelny AS
        (
            LTRIM(RTRIM(
                ISNULL(Ulica + N' ', N'') +
                ISNULL(NumerDomu, N'') +
                CASE 
                    WHEN NumerLokalu IS NOT NULL AND NumerLokalu <> N'' 
                    THEN N'/' + NumerLokalu 
                    ELSE N'' 
                END +
                CASE 
                    WHEN KodPocztowy IS NOT NULL OR Miejscowosc IS NOT NULL
                    THEN N', ' + ISNULL(KodPocztowy + N' ', N'') + ISNULL(Miejscowosc, N'')
                    ELSE N''
                END +
                CASE 
                    WHEN Kraj IS NOT NULL AND Kraj <> N'' 
                    THEN N', ' + Kraj 
                    ELSE N'' 
                END
            ))
        ),

        DataUtworzenia DATETIME2(0) NOT NULL
            CONSTRAINT DF_SBD_Adresy_DataUtworzenia DEFAULT SYSDATETIME(),

        DataModyfikacji DATETIME2(0) NULL
    );
END