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
	FROM SBD.Alokacje a
	JOIN SBD.Pozycje p ON p.Id = a.PozycjaId
	JOIN SBD.Dokumenty d ON d.Id = p.DokumentId


GO

SELECT * FROM SBD.AlokacjeView