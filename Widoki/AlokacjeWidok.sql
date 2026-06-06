CREATE OR ALTER VIEW SBD.AlokacjeView
AS
	SELECt 
			a.Id AS AlokacjaId
		,	a.Cecha AS AlokacjaCecha
		,	a.Kierunek AS AlokacjaKierunek
		,	a.Ilosc AS Ilosc
		,	a.Ilosc / p.JednostkaPrzelicznik AS IloscJednostkowa
		,	p.JednostkaKod AS Jednostka
		,	p.TowarKod AS KodTowaru
		,	p.TowarNazwa AS NazwaTowaru
		,	d.NumerDokumentu 
		,	zd.NumerDokumentu AS ZrodlowyNumerDokumentu 
	FROM SBD.Alokacje a
	LEFT JOIN SBD.Dostawy dos ON dos.Id = a.DostawaId
	LEFT JOIN SBD.Alokacje za ON za.Id = dos.ZrodlowaAlokacjaId
	LEFT JOIN SBD.Dokumenty zd ON zd.Id = za.DokumentId
	JOIN SBD.Pozycje p ON p.Id = a.PozycjaId
	JOIN SBD.Dokumenty d ON d.Id = p.DokumentId


GO

SELECT * FROM SBD.AlokacjeView