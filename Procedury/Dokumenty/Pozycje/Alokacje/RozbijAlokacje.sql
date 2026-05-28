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

		DECLARE @DokumentId INT = (SELECT a.DokumentId FROM SBD.Alokacje a WHERE a.Id = @Id)

		EXEC SBD.WalidacjaBlokady @DokumentId = @DokumentId, @Operator = @Operator

		IF @Ilosc <= 0
			THROW 51029, N'alokowana iloœæ musi byæ dodatnia.', 1

		IF (SELECT Ilosc FROM #tmp) < @Ilosc
			THROW 51029, N'¿¹dana iloœæ przekracza obecn¹ iloœæ alokacji.', 1

		UPDATE SBD.Alokacje 
			SET Ilosc = Ilosc - @Ilosc
		WHERE Id = @Id

		IF (SELECT Ilosc FROM SBD.Alokacje WHERE Id = @Id) = 0
			DELETE FROM SBD.Alokacje WHERE Id = @Id

		INSERT INTO SBD.Alokacje
		(DokumentId, PozycjaId, DostawaId, Ilosc, DataUtworzenia, Cecha)
		VALUES
		(@DokumentId, (SELECT PozycjaId FROm #tmp), NULL, @Ilosc, GETDATE(), ISNULL(@Cecha, N''))

		IF @StartedTran = 1
            COMMIT TRAN;

		SELECT N'Pomyœlnie rozdzielono alokacjê.' AS Odpowiedz

	END TRY
	BEGIN CATCH
		
		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;

		SELECT CONCAT(N'Wyst¹pi³ b³¹d w trakcie rozdzielania alokacji - ', ERROR_MESSAGE()) AS Odpowiedz, ERROR_NUMBER() AS Kod
	
	END CATCH
END
GO


SELECT * FROM SBD.Dokumenty

SELECT * FROM SBD.Jednostki

SELECT * FROM SBD.Alokacje
