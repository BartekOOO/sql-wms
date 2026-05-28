CREATE OR ALTER PROCEDURE SBD.UsunAlokacje
	@Id INT,
	@Operator NVARCHAR(100)
AS
BEGIN
SET NOCOUNT ON;

	DECLARE @StartedTran BIT = 0;

	BEGIN TRY

		IF @@TRANCOUNT = 0
        BEGIN
            BEGIN TRAN;
            SET @StartedTran = 1;
        END

		IF NOT EXISTS (SELECT 1 FROM SBD.Alokacje WHERE Id = @Id)
			THROW 51029, N'Nie istnieje alokacja z takim identyfikatorem.', 1;

		DECLARE @DokumentId INT, @PozycjaId INT, @Cecha NVARCHAR(200) 
		
		SELECT @DokumentId = a.DokumentId, @PozycjaId = PozycjaId, @Cecha = Cecha
			FROM SBD.Alokacje a WHERE a.Id = @Id

		EXEC SBD.WalidacjaBlokady @DokumentId = @DokumentId, @Operator = @Operator

		IF (SELECT COUNT(*) FROM SBD.Alokacje WHERE PozycjaId = @PozycjaId) = 1
			THROW 51029, N'pozycja musi mieæ przynajmniej jedn¹ alokacjê.', 1

		DECLARE @InnaAlokacja INT = (SELECT TOP 1 Id FROM SBD.Alokacje WHERE PozycjaId = @PozycjaId AND Cecha = @Cecha AND Id <> @Id)

		IF @InnaAlokacja IS NOT NULL
			BEGIN
		
				UPDATE nowaAlokacja
					SET nowaAlokacja.Ilosc = nowaAlokacja.Ilosc + staraAlokacja.Ilosc
				FROM SBD.Alokacje staraAlokacja
				JOIN SBD.Alokacje nowaAlokacja ON nowaAlokacja.Id = @InnaAlokacja
				WHERE staraAlokacja.Id = @Id
					
				DELETE FROM SBD.Alokacje WHERE Id = @Id

			END
		ELSE
			BEGIN
		
				UPDATE SBD.Alokacje SET Cecha = N'' WHERE Id = @Id
		END

		IF @StartedTran = 1
            COMMIT TRAN;

		SELECT N'Pomyœlnie usuniêto alokacjê.' AS Odpowiedz


	END TRY
	BEGIN CATCH

		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;


		SELECT CONCAT(N'Wyst¹pi³ b³¹d w trakcie usuwania alokacji - ', ERROR_MESSAGE()) AS Odpowiedz, ERROR_NUMBER() AS Kod
	
	END CATCH
END
GO


SELECT * FROM SBD.Dokumenty

SELECT * FROM SBD.Jednostki

SELECT * FROM SBD.Alokacje
