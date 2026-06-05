CREATE OR ALTER PROCEDURE SBD.ObslugaPM
	@Id INT,
	@Akcja NVARCHAR(20),
	@ObecnyStan NVARCHAR(20)
AS
BEGIN
SET NOCOUNT ON;
SET XACT_ABORT ON;

	IF @Akcja = N'Anuluj'
		BEGIN
			IF EXISTS (SELECT 1 FROM SBD.Alokacje a 
				JOIN SBD.Dostawy d ON d.ZakladajacaAlokacjaId = a.Id
				WHERE a.DokumentId = @Id AND a.Ilosc <> d.Ilosc)
				THROW 51029, N'dostawy tego dokumentu zosta³y przesuniête dalej.', 1

			UPDATE dos
				SET dos.Ilosc = 0
			FROM SBD.Dostawy dos
			JOIN SBD.Alokacje a ON a.Id = dos.ZakladajacaAlokacjaId
			WHERE a.DokumentId = @Id

			UPDATE SBD.Dokumenty SET [Status] = N'Anulowany' WHERE ID = @Id
		END
	ELSE IF @Akcja = N'Zatwierdz'
		BEGIN
			
			IF (SELECT COUNT(*) FROM SBD.Pozycje WHERE DokumentId = @Id) = 0
				THROW 51029, N'dokument nie posiada ¿adnych pozycji.', 1

			DECLARE @SektorDocelowy INT;
			DECLARE @MagazynDocelowy INT;

			SELECT 
			      @SektorDocelowy = SektorDocelowyId
			    , @MagazynDocelowy = MagazynDocelowyId
			FROM SBD.Dokumenty
			WHERE Id = @Id;

			IF @SektorDocelowy IS NULL
			BEGIN
			    SELECT TOP 1
			        @SektorDocelowy = s.Id
			    FROM SBD.Sektory s
			    LEFT JOIN SBD.Dostawy d
			        ON d.SektorId = s.Id
			       AND d.MagazynId = @MagazynDocelowy
			    WHERE s.MagazynId = @MagazynDocelowy
			    GROUP BY s.Id
			    ORDER BY ISNULL(SUM(d.Ilosc), 0) ASC, s.Id ASC;

			    IF @SektorDocelowy IS NULL
			        THROW 51029, N'nie uda³o siê wyznaczyæ sektora docelowego dla dokumentu PM.', 1;
			END;

			INSERT INTO SBD.Dostawy
			(
			      TowarId
			    , TowarKod
			    , TowarNazwa
			    , MagazynId
			    , SektorId
			    , ZakladajacaPozycjaId
			    , Ilosc
			    , DataUtworzenia
			    , DataModyfikacji
			    , Cecha
			    , ZakladajacaAlokacjaId
			    , ZrodlowaAlokacjaId
			)
			SELECT
			      p.TowarId
			    , p.TowarKod
			    , p.TowarNazwa
			    , @MagazynDocelowy
			    , @SektorDocelowy
			    , p.Id
			    , a.Ilosc
			    , SYSDATETIME()
			    , CAST(NULL AS DATETIME2(0))
			    , a.Cecha
			    , a.Id
			    , a.Id
			FROM SBD.Alokacje a
			JOIN SBD.Pozycje p
			    ON p.Id = a.PozycjaId
			WHERE a.DokumentId = @Id;

			UPDATE SBD.Dokumenty SET [Status] = N'Zatwierdzony' WHERE ID = @Id
	END
END
GO

SELECT * FROM SBD.Alokacje