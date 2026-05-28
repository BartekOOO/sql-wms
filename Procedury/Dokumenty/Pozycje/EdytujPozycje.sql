CREATE OR ALTER PROCEDURE SBD.EdytujPozycje
	@Id INT = NULL,
	@TowarKod NVARCHAR(50) = NULL,
	@Ilosc DECIMAL(18, 6) = NULL,
	@Operator NVARCHAR(100)
AS
BEGIN
SET NOCOUNT ON;
SET XACT_ABORT ON;

	DECLARE @StartedTran BIT = 0;

	BEGIN TRY

		IF @@TRANCOUNT = 0
        BEGIN
            BEGIN TRAN;
            SET @StartedTran = 1;
        END

		IF NOT EXISTS (SELECT 1 FROM SBD.Pozycje WHERE Id = @Id)
			THROW 51029, N'nie istnieje pozycja z takim identyfikatorem.', 1

		DECLARE @DokumentId INT = (SELECT DokumentId FROM SBD.Pozycje WHERE Id = @Id)
		EXEC SBD.WalidacjaBlokady @DokumentId = @DokumentId, @Operator = @Operator

		DECLARE @AnyChanged INT = 0;

		SET @Ilosc = (SELECT JednostkaPrzelicznik FROM SBD.Pozycje WHERE Id = @Id) * @Ilosc;

		IF @Ilosc IS NOT NULL
		BEGIN
			
			IF @Ilosc <= 0 
				THROW 51029, N'iloœæ pozycji musi byæ wiêksza od zera.', 1



			IF @Ilosc <> (SELECT Ilosc FROM SBD.Pozycje WHERE Id = @Id)
			BEGIN

				IF (SELECT COUNT(*) FROM SBD.Alokacje WHERE PozycjaId = @Id) = 1 --Dla prostych przypadków
					BEGIN
						UPDATE SBD.Alokacje SET Ilosc = @Ilosc WHERE PozycjaId = @Id
					END
				ELSE
					BEGIN

					DECLARE @StaraIlosc DECIMAL(18, 6) = (SELECT Ilosc FROM SBD.Pozycje WHERE Id = @Id)

					IF @Ilosc - @StaraIlosc > 0
						BEGIN
							
							IF NOT EXISTS (SELECT * FROM SBD.Alokacje WHERE PozycjaId = @Id AND Cecha = SBD.DajKluczPustejCechy())
								BEGIN
									INSERT INTO SBD.Alokacje (DokumentId, PozycjaId, DostawaId, Ilosc, DataUtworzenia, Cecha)
									SELECT p.DokumentId, p.Id, NULL, @Ilosc - @StaraIlosc, GETDATE(), SBD.DajKluczPustejCechy() FROM SBD.Pozycje p
									WHERE p.Id = @Id
								END
							ELSE 
								BEGIN
								
									UPDATE alokacja 
										SET alokacja.Ilosc = alokacja.Ilosc + (@Ilosc - @StaraIlosc)
									FROM SBD.Alokacje alokacja
									JOIN
									(
										SELECT TOP 1 * 
										FROM SBD.Alokacje
										WHERE PozycjaId = @Id 
										AND Cecha = SBD.DajKluczPustejCechy()
										ORDER BY Id
									) a ON a.Id = alokacja.Id

							END

						END
					ELSE 
						BEGIN
							IF NOT EXISTS (SELECT 1 FROM SBD.Alokacje WHERE PozycjaId = @Id AND Cecha = SBD.DajKluczPustejCechy() AND Ilosc >= @StaraIlosc - @Ilosc)
								THROW 51029, N'zmniejszenie iloœci jest niemo¿liwe. Zmieñ rozk³ad alokacji i spróbuj ponownie.', 1

							UPDATE alokacja
								SET alokacja.Ilosc = alokacja.Ilosc - (@StaraIlosc - @Ilosc)
								FROM SBD.Alokacje alokacja
								JOIN 
								(
								    SELECT TOP 1 *
								    FROM SBD.Alokacje 
								    WHERE PozycjaId = @Id 
								      AND Cecha = SBD.DajKluczPustejCechy()
								      AND Ilosc >= @StaraIlosc - @Ilosc
								    ORDER BY Id
								) a ON a.Id = alokacja.Id;

							DELETE FROM SBD.Alokacje WHERE PozycjaId = @Id AND Ilosc = 0

						END
				END


				UPDATE SBD.Pozycje SET Ilosc = @Ilosc
				WHERE Id = @Id

				SET @AnyChanged = 1;
			END
		END

		IF @TowarKod IS NOT NULL
		BEGIN
			
			IF NOT EXISTS (SELECT * FROM SBD.Towary WHERE Kod = @TowarKod)
				THROW 51029, N'nie istnieje towar z takim kodem.', 1
			
			DECLARE @DomyslnaJednostka NVARCHAR(20), @DomyslnaJednostkaPrzelicznik DECIMAL(18, 6), @DomyslnaJednostkaId INT
			SELECT @DomyslnaJednostka = j.Kod, @DomyslnaJednostkaPrzelicznik = j.Przelicznik, @DomyslnaJednostkaId = j.Id
				FROM SBD.Jednostki j JOIN SBD.Towary t ON t.Id = j.TowarId WHERE t.Kod = @TowarKod AND j.Przelicznik = 1

			IF @TowarKod <> (SELECT TowarKod FROM SBD.Pozycje WHERE Id = @Id)
			BEGIN
				UPDATE pozycja
					SET pozycja.JednostkaPrzelicznik = @DomyslnaJednostkaPrzelicznik,
						pozycja.JednostkaKod = @DomyslnaJednostka,
						pozycja.JednostkaId = @DomyslnaJednostkaId,
						pozycja.TowarKod = t.Kod,
						pozycja.TowarNazwa = t.Nazwa,
						pozycja.TowarId = t.Id
					FROM SBD.Pozycje pozycja
					JOIN SBD.Towary t ON t.Kod = @TowarKod
					WHERE pozycja.Id = @Id

				SET @AnyChanged = 1;

				DELETE FROM SBD.Alokacje WHERE PozycjaId = @Id

				INSERT INTO SBD.Alokacje 
					(PozycjaId, DokumentId, DostawaId, Ilosc, DataUtworzenia, Cecha)
					VALUES
					(@Id, @DokumentId, NULL, (SELECT COALESCE(@Ilosc, Ilosc) FROM SBD.Pozycje WHERE Id = @Id), GETDATE(), SBD.DajKluczPustejCechy())

			END
		END

		IF @AnyChanged = 1
			UPDATE SBD.Pozycje SET DataModyfikacji = GETDATE() WHERE Id = @Id;

		IF @StartedTran = 1
            COMMIT TRAN;

		IF @AnyChanged = 1
			SELECT N'Pomyœlnie edytowano pozycjê' AS Odpowiedz
		ELSE
			SELECT N'Brak zmian' AS Odpowiedz

	END TRY
	BEGIN CATCH

		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;

		SELECT CONCAT(N'Wyst¹pi³ b³¹d w trakcie edycotwania pozycji - ', ERROR_MESSAGE()) AS Odpowiedz, ERROR_NUMBER() AS Kod
	
	END CATCH
END
GO


SELECT * FROM SBD.Pozycje

SELECT * FROM SBD.Jednostki

SELECT * FROM SBD.Alokacje
