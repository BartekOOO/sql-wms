CREATE OR ALTER VIEW SBD.PozycjeView
AS
	SELECT 
			p.Id
		,	p.TowarKod AS TowarKod
		,	p.TowarNazwa AS TowarNazwa
		,	p.TowarId AS TowarId
		,	p.Ilosc / p.JednostkaPrzelicznik AS IloscJednostkowa
		,	p.JednostkaKod AS Jednostka
		,	p.Ilosc AS Ilosc
		,	d.Numer AS NumerDokumentu
		,	d.Id AS IdDokumentu
		,	d.TypDokumentu AS TypDokumentu
	FROM SBD.Pozycje p
	JOIN SBD.Dokumenty d ON d.Id = p.DokumentId

GO

SELECT * FROM SBD.PozycjeView