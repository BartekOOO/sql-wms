CREATE OR ALTER VIEW SBD.TowaryView
AS
	WITH StanyMagazynowe AS (
		SELECT 
			  TowarId
			, TowarKod
			, TowarNazwa
			, Cecha
			, SUM(Ilosc) AS Ilosc
		FROM SBD.Dostawy d
		WHERE d.Ilosc > 0
		GROUP BY TowarId, TowarKod, TowarNazwa, Cecha
	)
	SELECT t.Id, t.Nazwa, t.Kod, ISNULL(sm.Cecha, N'') AS Cecha, ISNULL(sm.Ilosc, 0) AS Ilosc FROM SBD.Towary t
	LEFT JOIN StanyMagazynowe sm ON sm.TowarId = t.Id

GO


SELECT * FROM SBD.TowaryView