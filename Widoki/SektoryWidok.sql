CREATE OR ALTER VIEW SBD.SektoryView
AS

	SELECT 
			s.Nazwa AS SektorNazwa
		,	s.Kod AS SektorKod
		,	m.Nazwa AS MagazynNazwa
		,	m.Kod AS MagazynKod
	FROM SBD.Sektory s
	JOIN SBD.Magazyny m ON m.Id = s.MagazynId


GO

SELECT * FROM SBD.SektoryView