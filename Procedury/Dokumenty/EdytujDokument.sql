CREATE OR ALTER PROCEDURE SBD.EdytujDokument
	@Id INT,

	@DataDokumentu DATETIME = NULL,
	@Opis NVARCHAR(500) = NULL,

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

		IF NOT EXISTS (SELECT * FROM SBD.Dokumenty WHERE Id = @Id)
			THROW 51029, N'nie istnieje dokument z takim identyfikatorem.', 1

		EXEC SBD.WalidacjaBlokady @DokumentId = @Id, @Operator = @Operator

		DECLARE @StaraDataDokumentu DATETIME, @StaryOpis NVARCHAR(500);

		DECLARE @AnyChanged INT = 0;
		DECLARE @TypDokumentu NVARCHAR(10);

		SELECT
			   @StaraDataDokumentu = DataDokumentu,
			   @StaryOpis = Opis,
			   @TypDokumentu = TypDokumentu
		FROM SBD.Dokumenty WHERE Id = @Id

		IF @DataDokumentu IS NOT NULL AND @DataDokumentu <> @StaraDataDokumentu
		BEGIN
			SET @AnyChanged = 1;
			UPDATE SBD.Dokumenty SET DataDokumentu = @DataDokumentu WHERE Id = @Id
		END


		IF @Opis IS NOT NULL AND @Opis <> @StaryOpis
		BEGIN
			SET @AnyChanged = 1;
			UPDATE SBD.Dokumenty SET Opis = @Opis WHERE Id = @Id
		END

		IF @AnyChanged = 1
			UPDATE SBD.Dokumenty 
				SET DataModyfikacji = GETDATE()
				WHERE Id = @Id;

		IF @StartedTran = 1
			COMMIT TRAN;

		IF @AnyChanged = 1
			SELECT CONCAT(N'Pomyœlnie edytowano dokument', N'.') AS Odpowiedz
		ELSE
			SELECT N'Brak zmian.' AS Odpowiedz

	END TRY
	BEGIN CATCH

		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;

		DECLARE @ErrorMessage NVARCHAR(4000);

		SET @ErrorMessage = CONCAT(
		    N'Wyst¹pi³ b³¹d w trakcie edytowania dokumentu - ',
		    ERROR_MESSAGE()
		);

		THROW 51029, @ErrorMessage, 1;
	
	END CATCH
END
GO

