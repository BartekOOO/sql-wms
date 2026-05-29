CREATE OR ALTER PROCEDURE SBD.ZamknijDokument
	@Id INT,
	@Akcja NVARCHAR(20) = N'Brak',
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

		IF @Operator IS NULL
			THROW 51029, N'nie podano kodu operatora.', 1

		IF @Akcja IS NULL
			SET @Akcja = N'Brak';

		IF @Akcja NOT IN (N'Usun', N'Zatwierdz', N'Anuluj', N'Brak')
			THROW 51029, N'nierozpoznana akcja zamykania dokumentu. Dozwolone: Usun, Zatwierdz, Anuluj, Brak.', 1
		
		IF NOT EXISTS (SELECT 1 FROM SBD.Dokumenty WHERE Id = @Id)
			THROW 51029, N'nie istnieje dokument z takim identyfikatorem.', 1

		DECLARE @ZablokowanyPrzez NVARCHAR(100) = (SELECT OperatorKod FROM SBD.Dokumenty WHERE Id = @Id)

		IF @ZablokowanyPrzez IS NULL
			THROW 51029, N'dokument nie jest otwarty.', 1

		IF @ZablokowanyPrzez <> @Operator
			THROW 51029, N'dokument jest otwarty przez innego u¿ytkownika.', 1

		--Matryca przejœæ

		UPDATE SBD.Dokumenty SET OperatorKod = NULL WHERE Id = @Id

		IF @StartedTran = 1
			COMMIT TRAN;

	END TRY
	BEGIN CATCH

		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;

		SELECT CONCAT(N'Wyst¹pi³ b³¹d w trakcie zamykania dokumentu - ', ERROR_MESSAGE()) AS Odpowiedz, ERROR_NUMBER() AS Kod
	
	END CATCH
END
GO
