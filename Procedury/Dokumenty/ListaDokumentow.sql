CREATE OR ALTER PROCEDURE SBD.ListaDokumentow
    @Strona INT = 1,
    @WielkoscStrony INT = 50,
    @KolumnySortowania NVARCHAR(MAX) = N'NumerSortowania ASC',

    @NumerDokumentu NVARCHAR(50) = NULL,
    @MagazynDocelowyKod NVARCHAR(100) = NULL,
    @MagazynZrodlowyKod NVARCHAR(100) = NULL,
    @SektorDocelowyKod NVARCHAR(100) = NULL,
    @SektorZrodlowyKod NVARCHAR(100) = NULL,
    @DataRealizacji DATETIME = NULL,
    @DataWystawienia DATETIME = NULL,
    @TypDokumentu NVARCHAR(10) = NULL,
    @Miesiac INT = NULL,
    @Rok INT = NULL,

    @TowarKod NVARCHAR(100) = NULL,
    @TowarNazwa NVARCHAR(200) = NULL,
    @Cecha NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Strona IS NULL OR @Strona < 1
        SET @Strona = 1;

    IF @WielkoscStrony IS NULL OR @WielkoscStrony < 1
        SET @WielkoscStrony = 50;

    DECLARE @Offset INT = (@Strona - 1) * @WielkoscStrony;

    DECLARE @Sql NVARCHAR(MAX) = N'';
    DECLARE @Apply NVARCHAR(MAX) = N'';
    DECLARE @Where NVARCHAR(MAX) = N' WHERE 1 = 1 ';
    DECLARE @OrderBy NVARCHAR(MAX) = ISNULL(NULLIF(LTRIM(RTRIM(@KolumnySortowania)), N''), N'NumerSortowania ASC');

    IF @NumerDokumentu IS NOT NULL
        SET @Where += N'
        AND CONVERT(NVARCHAR(50), d.NumerDokumentu) LIKE N''%'' + @NumerDokumentu + N''%'' ';

    IF @MagazynDocelowyKod IS NOT NULL
        SET @Where += N'
        AND d.MagazynDocelowyKod LIKE N''%'' + @MagazynDocelowyKod + N''%'' ';

    IF @MagazynZrodlowyKod IS NOT NULL
        SET @Where += N'
        AND d.MagazynZrodlowyKod LIKE N''%'' + @MagazynZrodlowyKod + N''%'' ';

    IF @SektorDocelowyKod IS NOT NULL
        SET @Where += N'
        AND d.SektorDocelowyKod LIKE N''%'' + @SektorDocelowyKod + N''%'' ';

    IF @SektorZrodlowyKod IS NOT NULL
        SET @Where += N'
        AND d.SektorZrodlowyKod LIKE N''%'' + @SektorZrodlowyKod + N''%'' ';

    IF @DataRealizacji IS NOT NULL
        SET @Where += N'
        AND CAST(d.DataRealizacji AS DATE) = CAST(@DataRealizacji AS DATE) ';

    IF @TypDokumentu IS NOT NULL
        SET @Where += N'
        AND d.TypDokumentu = @TypDokumentu ';

    IF @Miesiac IS NOT NULL
        SET @Where += N'
        AND MONTH(d.DataRealizacji) = @Miesiac ';

    IF @Rok IS NOT NULL
        SET @Where += N'
        AND YEAR(d.DataRealizacji) = @Rok ';


    IF @TowarKod IS NOT NULL OR @TowarNazwa IS NOT NULL
    BEGIN
        SET @Apply += N'
        OUTER APPLY
        (
            SELECT TOP 1
                    p.Id,
                    p.TowarKod,
                    p.TowarNazwa,
                    p.TowarId,
                    p.Ilosc,
                    p.IloscJednostkowa,
                    p.Jednostka
            FROM SBD.PozycjeView p
            WHERE p.IdDokumentu = d.Id
              AND (@TowarKod IS NULL OR p.TowarKod LIKE N''%'' + @TowarKod + N''%'')
              AND (@TowarNazwa IS NULL OR p.TowarNazwa LIKE N''%'' + @TowarNazwa + N''%'')
            ORDER BY p.Id
        ) poz ';

        SET @Where += N'
        AND poz.Id IS NOT NULL ';
    END;

    IF @Cecha IS NOT NULL
    BEGIN
        SET @Apply += N'
        OUTER APPLY
        (
            SELECT TOP 1
                    a.AlokacjaId,
                    a.AlokacjaCecha,
                    a.AlokacjaKierunek,
                    a.KodTowaru,
                    a.NazwaTowaru,
                    a.Ilosc,
                    a.IloscJednostkowa,
                    a.Jednostka
            FROM SBD.AlokacjeView a
            WHERE a.NumerDokumentu = d.NumerDokumentu
              AND a.AlokacjaCecha LIKE N''%'' + @Cecha + N''%''
            ORDER BY a.AlokacjaId
        ) alo ';

        SET @Where += N'
        AND alo.AlokacjaId IS NOT NULL ';
    END;

    SET @Sql = N'
    SELECT
            d.*
        ,   COUNT(1) OVER() AS LiczbaWszystkichRekordow
    FROM SBD.DokumentyView d
    ' + @Apply + N'
    ' + @Where + N'
    ORDER BY ' + @OrderBy + N'
    OFFSET @Offset ROWS
    FETCH NEXT @WielkoscStrony ROWS ONLY;
    ';

	PRINT @Sql;

    EXEC sp_executesql
        @Sql,
        N'
            @NumerDokumentu NVARCHAR(50),
            @MagazynDocelowyKod NVARCHAR(100),
            @MagazynZrodlowyKod NVARCHAR(100),
            @SektorDocelowyKod NVARCHAR(100),
            @SektorZrodlowyKod NVARCHAR(100),
            @DataRealizacji DATETIME,
            @DataWystawienia DATETIME,
            @TypDokumentu NVARCHAR(10),
            @Miesiac INT,
            @Rok INT,
            @TowarKod NVARCHAR(100),
            @TowarNazwa NVARCHAR(200),
            @Cecha NVARCHAR(100),
            @Offset INT,
            @WielkoscStrony INT
        ',
        @NumerDokumentu = @NumerDokumentu,
        @MagazynDocelowyKod = @MagazynDocelowyKod,
        @MagazynZrodlowyKod = @MagazynZrodlowyKod,
        @SektorDocelowyKod = @SektorDocelowyKod,
        @SektorZrodlowyKod = @SektorZrodlowyKod,
        @DataRealizacji = @DataRealizacji,
        @DataWystawienia = @DataWystawienia,
        @TypDokumentu = @TypDokumentu,
        @Miesiac = @Miesiac,
        @Rok = @Rok,
        @TowarKod = @TowarKod,
        @TowarNazwa = @TowarNazwa,
        @Cecha = @Cecha,
        @Offset = @Offset,
        @WielkoscStrony = @WielkoscStrony;
END
GO