CREATE OR ALTER VIEW SBD.DokumentyView
AS

	SELECT 
		--Dane podstawowe
			d.Id AS Id
		,	d.NumerDokumentu AS NumerDokumentu
		,	d.TypDokumentu AS TypDokumentu
		,	d.[Status] AS StatusDokumentu
		,	d.DataDokumentu AS DataRealizacji
		,	d.OperatorKod AS OtworzonyPrzez
		,	CASE WHEN d.DataModyfikacji IS NULL THEN N'Nigdy' ELSE CONVERT(NVARCHAR(19), d.DataModyfikacji, 120) END AS DataModyfikacji
		,	d.Seria AS SeriaDokumentu
		,	d.Opis AS OpisDokumentu

		--Magazyn i sektor docelowy
		,	CASE WHEN d.TypDokumentu = N'WM' THEN N'Nie dotyczy' ELSE ISNULL(d.MagazynDocelowyKod, N'Nie ustawiono') END AS MagazynDocelowyKod
		,	CASE WHEN d.TypDokumentu = N'WM' THEN N'Nie dotyczy' ELSE ISNULL(d.MagazynDocelowyNazwa, N'Nie ustawiono') END AS MagazynDocelowyNazwa
		,	CASE WHEN d.TypDokumentu = N'WM' THEN N'Nie dotyczy'WHEN d.SektorDocelowyKod IS NULL THEN N'Dowolny' ELSE ISNULL(d.SektorDocelowyKod, N'Nie ustawiono') END AS SektorDocelowyKod
		,	CASE WHEN d.TypDokumentu = N'WM' THEN N'Nie dotyczy'WHEN d.SektorDocelowyNazwa IS NULL THEN N'Dowolny' ELSE ISNULL(d.SektorDocelowyNazwa, N'Nie ustawiono') END AS SektorDocelowyNazwa

		--Magazyn i sektor Ÿród³owy
		,	CASE WHEN d.TypDokumentu = N'PM' THEN N'Nie dotyczy' ELSE ISNULL(d.MagazynZrodlowyKod, N'Nie ustawiono') END AS MagazynZrodlowyKod
		,	CASE WHEN d.TypDokumentu = N'PM' THEN N'Nie dotyczy' ELSE ISNULL(d.MagazynZrodlowyNazwa, N'Nie ustawiono') END AS MagazynZrodlowyNazwa
		,	CASE WHEN d.TypDokumentu = N'PM' THEN N'Nie dotyczy'WHEN d.SektorZrodlowyKod IS NULL THEN N'Dowolny' ELSE ISNULL(d.SektorZrodlowyKod, N'Nie ustawiono') END AS SektorZrodlowyKod
		,	CASE WHEN d.TypDokumentu = N'PM' THEN N'Nie dotyczy' WHEN d.SektorZrodlowyNazwa IS NULL THEN N'Dowolny' ELSE ISNULL(d.SektorZrodlowyNazwa, N'Nie ustawiono') END AS SektorZrodlowyNazwa

		--Techniczne
		,	d.NumerDokumentuSort AS NumerSortowania
	FROM SBD.Dokumenty d

GO

SELECT * FROM SBD.DokumentyView