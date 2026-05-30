CREATE OR ALTER VIEW SBD.DokumentyView
AS

	SELECT 
		--Dane podstawowe
			d.Id AS Id
		,	d.Numer AS NumerDokumentu
		,	d.TypDokumentu AS TypDokumentu
		,	d.[Status] AS StatusDokumentu
		,	d.DataDokumentu AS DataRealizacji
		,	d.OperatorKod AS OtworzonyPrzez
		,	CASE WHEN d.DataModyfikacji IS NULL THEN N'Nigdy' ELSE d.DataUtworzenia END AS DataModyfikacji
		,	d.Seria AS SeriaDokumentu
		,	d.Opis AS OpisDokumentu

		--Magazyn i sektor docelowy
		,	CASE WHEN d.TypDokumentu = N'WM' THEN N'Nie dotyczy' ELSE d.MagazynDocelowyKod END AS MagazynDocelowyKod
		,	CASE WHEN d.TypDokumentu = N'WM' THEN N'Nie dotyczy' ELSE MagazynDocelowyNazwa END AS MagazynDocelowyNazwa
		,	CASE WHEN d.SektorDocelowyKod IS NULL THEN N'Dowolny' ELSE d.SektorDocelowyKod END AS SektorDocelowyKod
		,	CASE WHEN d.SektorDocelowyNazwa IS NULL THEN N'Dowolny' ELSE d.SektorDocelowyNazwa END AS SektorDocelowyNazwa

		--Magazyn i sektor Ÿród³owy
		,	CASE WHEN d.TypDokumentu = N'PM' THEN N'Nie dotyczy' ELSE d.MagazynZrodlowyKod END AS MagazynZrodlowyKod
		,	CASE WHEN d.TypDokumentu = N'PM' THEN N'Nie dotyczy' ELSE d.MagazynZrodlowyNazwa END AS MagazynZrodlowyNazwa
		,	CASE WHEN d.SektorZrodlowyKod IS NULL THEN N'Dowolny' ELSE d.SektorZrodlowyKod END AS SektorZrodlowyKod
		,	CASE WHEN d.SektorZrodlowyNazwa IS NULL THEN N'Dowolny' ELSE d.SektorZrodlowyNazwa END AS SektorZrodlowyNazwa
	FROM SBD.Dokumenty d

GO

SELECT * FROM SBD.DokumentyView