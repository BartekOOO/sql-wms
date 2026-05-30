CREATE OR ALTER VIEW SBD.MagazynyView
AS

	SELECT 
			m.Id AS Id
		,	m.Nazwa AS Nazwa
		,	m.Kod AS Kod
		,	m.Opis AS Opis
		,	a.AdresPelny AS Adres
		,	sektory.Ilosc AS LiczbaSektorow
		FROM SBD.Magazyny m
	JOIN SBD.Adresy a ON a.Id = m.AdresId
	OUTER APPLY (
		SELECT COUNT(*) AS Ilosc FROM SBD.Sektory s
		WHERE s.MagazynId = m.Id
	) sektory

GO

SELECT * FROM SBD.MagazynyView