CREATE OR ALTER PROCEDURE SBD.RozbijAlokacje
	@Id INT,
	@Ilosc DECIMAL(18,6),
	@Cecha NVARCHAR(200) = NULL,
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

		SELECT * INTO #tmp FROM SBD.Alokacje WHERE Id = @Id

		IF NOT EXISTS (SELECT * FROM #tmp)
			THROW 51029, N'nie istnieje alokacja z takim identyfikatorem.', 1

		DECLARE @DokumentId INT, @Kierunek NVARCHAR(100)

		SET @Ilosc = (SELECT p.JednostkaPrzelicznik FROM SBD.Alokacje a JOIN SBD.Pozycje p ON p.id = a.PozycjaId WHERE a.Id = @Id) * @Ilosc;
		
		SELECT @DokumentId = a.DokumentId, @Kierunek = Kierunek FROM SBD.Alokacje a WHERE a.Id = @Id

		EXEC SBD.WalidacjaBlokady @DokumentId = @DokumentId, @Operator = @Operator

		IF @Ilosc <= 0
			THROW 51029, N'alokowana iloœæ musi byæ dodatnia.', 1

		IF (SELECT Ilosc FROM SBD.Alokacje WHERE Id = @Id) = @Ilosc
			DELETE FROM SBD.Alokacje WHERE Id = @Id

		IF (SELECT Ilosc FROM #tmp) < @Ilosc
			THROW 51029, N'¿¹dana iloœæ przekracza obecn¹ iloœæ alokacji.', 1

		UPDATE SBD.Alokacje 
			SET Ilosc = Ilosc - @Ilosc
		WHERE Id = @Id


		INSERT INTO SBD.Alokacje
		(DokumentId, PozycjaId, DostawaId, Ilosc, DataUtworzenia, Cecha, Kierunek)
		VALUES
		(@DokumentId, (SELECT PozycjaId FROm #tmp), NULL, @Ilosc, GETDATE(), ISNULL(@Cecha, N''), @Kierunek)

		IF @StartedTran = 1
            COMMIT TRAN;

		SELECT N'Pomyœlnie rozdzielono alokacjê.' AS Odpowiedz

	END TRY
	BEGIN CATCH
		
		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;

		DECLARE @ErrorMessage NVARCHAR(4000);

		SET @ErrorMessage = CONCAT(
		    N'Wyst¹pi³ b³¹d w trakcie zak³adania alokacji - ',
		    ERROR_MESSAGE()
		);

		THROW 51029, @ErrorMessage, 1;
	
	END CATCH
END
GO


SELECT * FROM SBD.Dokumenty

SELECT * FROM SBD.Jednostki

SELECT * FROM SBD.Alokacje
