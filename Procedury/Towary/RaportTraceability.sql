CREATE OR ALTER PROCEDURE SBD.RaportTraceability
    @DokumentNumer NVARCHAR(100),
    @TowarKod NVARCHAR(100) = NULL,
    @Cecha NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH DokumentStartowy AS
    (
        SELECT 
            d.Id AS DokumentId,
            d.NumerDokumentu,
            d.TypDokumentu,
            d.Status
        FROM SBD.Dokumenty d
        WHERE d.NumerDokumentu = @DokumentNumer
    ),
	DostawyStartowe AS
	(
	    SELECT
	        0 AS Poziom,
	        CAST(CONCAT(ds.NumerDokumentu, N' -> DOSTAWA ', dos.Id) AS NVARCHAR(MAX)) AS Sciezka,
	
	        ds.DokumentId AS DokumentStartowyId,
	        CAST(ds.NumerDokumentu AS NVARCHAR(100)) AS DokumentStartowyNumer,
	
	        dos.Id AS DostawaId,
	        dos.TowarId,
	        CAST(dos.TowarKod AS NVARCHAR(100)) AS TowarKod,
	        CAST(dos.TowarNazwa AS NVARCHAR(300)) AS TowarNazwa,
	        dos.MagazynId,
	        dos.SektorId,
	        dos.Ilosc AS AktualnaIloscDostawy,
	        CAST(dos.Cecha AS NVARCHAR(200)) AS Cecha,
	        dos.ZakladajacaPozycjaId,
	        dos.ZakladajacaAlokacjaId,
	        dos.ZrodlowaAlokacjaId,
	
	        CAST(NULL AS INT) AS PrzezDokumentId,
	        CAST(NULL AS NVARCHAR(100)) AS PrzezDokumentNumer,
	        CAST(NULL AS NVARCHAR(50)) AS PrzezTypDokumentu,
	        CAST(NULL AS INT) AS RozchodAlokacjaId,
	        CAST(NULL AS DECIMAL(18,6)) AS IloscRozchodu
	    FROM DokumentStartowy ds
	    JOIN SBD.Alokacje a
	        ON a.DokumentId = ds.DokumentId
	       AND a.Kierunek = N'Przychód'
	    JOIN SBD.Dostawy dos
	        ON dos.Id = a.DostawaId
	    WHERE (@TowarKod IS NULL OR dos.TowarKod = @TowarKod)
	      AND (@Cecha IS NULL OR dos.Cecha = @Cecha)
	),
	Trace AS
	(
	    SELECT *
	    FROM DostawyStartowe
	
	    UNION ALL
	
	    SELECT
	        t.Poziom + 1 AS Poziom,
	        CAST(CONCAT(t.Sciezka, N' -> ', d.NumerDokumentu, N' -> DOSTAWA ', nd.Id) AS NVARCHAR(MAX)) AS Sciezka,
	
	        t.DokumentStartowyId,
	        t.DokumentStartowyNumer,
	
	        nd.Id AS DostawaId,
	        nd.TowarId,
	        CAST(nd.TowarKod AS NVARCHAR(100)) AS TowarKod,
	        CAST(nd.TowarNazwa AS NVARCHAR(300)) AS TowarNazwa,
	        nd.MagazynId,
	        nd.SektorId,
	        nd.Ilosc AS AktualnaIloscDostawy,
	        CAST(nd.Cecha AS NVARCHAR(200)) AS Cecha,
	        nd.ZakladajacaPozycjaId,
	        nd.ZakladajacaAlokacjaId,
	        nd.ZrodlowaAlokacjaId,
	
	        d.Id AS PrzezDokumentId,
	        CAST(d.NumerDokumentu AS NVARCHAR(100)) AS PrzezDokumentNumer,
	        CAST(d.TypDokumentu AS NVARCHAR(50)) AS PrzezTypDokumentu,
	        ar.Id AS RozchodAlokacjaId,
	        ar.Ilosc AS IloscRozchodu
	    FROM Trace t
	    JOIN SBD.Alokacje ar
	        ON ar.DostawaId = t.DostawaId
	       AND ar.Kierunek = N'Rozchód'
	    JOIN SBD.Dokumenty d
	        ON d.Id = ar.DokumentId
	    JOIN SBD.Dostawy nd
	        ON nd.ZrodlowaAlokacjaId = ar.Id
	    WHERE d.Status <> N'Anulowany'
	),
    RozchodyBezNowejDostawy AS
    (
        SELECT
            t.Poziom + 1 AS Poziom,
            CAST(CONCAT(t.Sciezka, N' -> ', d.NumerDokumentu, N' -> WYDANIE') AS NVARCHAR(MAX)) AS Sciezka,

            t.DokumentStartowyId,
            t.DokumentStartowyNumer,

            CAST(NULL AS INT) AS DostawaId,
            t.TowarId,
            t.TowarKod,
            t.TowarNazwa,
            CAST(NULL AS INT) AS MagazynId,
            CAST(NULL AS INT) AS SektorId,
            CAST(0 AS DECIMAL(18,6)) AS AktualnaIloscDostawy,
            t.Cecha,
            CAST(NULL AS INT) AS ZakladajacaPozycjaId,
            CAST(NULL AS INT) AS ZakladajacaAlokacjaId,
            ar.Id AS ZrodlowaAlokacjaId,

            d.Id AS PrzezDokumentId,
            d.NumerDokumentu AS PrzezDokumentNumer,
            d.TypDokumentu AS PrzezTypDokumentu,
            ar.Id AS RozchodAlokacjaId,
            ar.Ilosc AS IloscRozchodu
        FROM Trace t
        JOIN SBD.Alokacje ar
            ON ar.DostawaId = t.DostawaId
           AND ar.Kierunek = N'Rozchód'
        JOIN SBD.Dokumenty d
            ON d.Id = ar.DokumentId
        WHERE d.Status <> N'Anulowany'
          AND NOT EXISTS
          (
              SELECT 1
              FROM SBD.Dostawy nd
              WHERE nd.ZrodlowaAlokacjaId = ar.Id
          )
    ),
    Wynik AS
    (
        SELECT *
        FROM Trace

        UNION ALL

        SELECT *
        FROM RozchodyBezNowejDostawy
    )
    SELECT
        w.Poziom,
        w.Sciezka,

        w.DokumentStartowyNumer,

        w.PrzezDokumentNumer,
        w.PrzezTypDokumentu,

        w.DostawaId,
        w.TowarKod,
        w.TowarNazwa,
        w.Cecha,

        w.MagazynId,
        m.Kod AS MagazynKod,
        m.Nazwa AS MagazynNazwa,

        w.SektorId,
        s.Kod AS SektorKod,
        s.Nazwa AS SektorNazwa,

        w.AktualnaIloscDostawy,
        w.IloscRozchodu,

        w.ZakladajacaPozycjaId,
        w.ZakladajacaAlokacjaId,
        w.ZrodlowaAlokacjaId,
        w.RozchodAlokacjaId
    FROM Wynik w
    LEFT JOIN SBD.Magazyny m
        ON m.Id = w.MagazynId
    LEFT JOIN SBD.Sektory s
        ON s.Id = w.SektorId
    ORDER BY
        w.TowarKod,
        w.Cecha,
        w.Poziom,
        w.DostawaId,
        w.RozchodAlokacjaId
    OPTION (MAXRECURSION 100);
END;
GO


EXEC SBD.RaportTraceability 'PM-1/06/2026/TEST'