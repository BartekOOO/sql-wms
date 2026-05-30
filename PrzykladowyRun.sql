SELECT * FROM SBD.Magazyny


EXEC SBD.ZalozDokument
	@TypDokumentu = N'PM',
	@Operator = N'BAWLA'

SELECT * FROM SBD.Dokumenty



SELECT * FROM SBD.Towary
SELECT * FROM SBD.Jednostki WHERE TowarId = 8

EXEC SBD.DodajPozycje
	@TowarKod = N'FOLIA-STRETCH',
	@DokumentId = 1,
	@Ilosc = 10,
	@Jednostka = N'kart',
	@Operator = N'BAWLA'

SELECT * FROM SBD.Pozycje
SELECT * FROM SBD.Alokacje

EXEC SBD.UsunPozycje
	@Id = 2,
	@Operator = 'BAWLA'


EXEC SBD.RozbijAlokacje @Id = 1, @Ilosc = 10, @Operator = 'BAWLA'

SELECT * FROM SBD.Alokacje