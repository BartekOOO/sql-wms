CREATE OR ALTER PROCEDURE SBD.OtworzDokument
	@Id INT,
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

		IF NOT EXISTS (SELECT 1 FROM SBD.Dokumenty WHERE Id = @Id)
			THROW 51029, N'nie istnieje dokument z takim identyfikatorem.', 1

		DECLARE @ObecnyBloker NVARCHAR(100) = (SELECT OperatorKod FROM SBD.Dokumenty WHERE Id = @Id)

		IF @ObecnyBloker IS NULL
			BEGIN
				UPDATE SBD.Dokumenty SET OperatorKod = @Operator WHERE Id = @Id
				SELECT N'Pomyœlnie uda³o siê otworzyæ dokument.' AS Odpowiedz
			END
		ELSE IF @ObecnyBloker = @Operator
			BEGIN
				SELECT N'Dokument jest ju¿ otwarty.' AS Odpowiedz
			END
		ELSE
			BEGIN
				DECLARE @Odpowiedz NVARCHAR(MAX) = CONCAT(N'dokument jest zablokowany przez ', QUOTENAME(@ObecnyBloker, N''''), N'.');
				THROW 51029, @Odpowiedz, 1
		END

		IF @StartedTran = 1
			COMMIT TRAN;

		SELECT N'Pomyœlnie otworzono dokument.' AS Odpowiedz

	END TRY
	BEGIN CATCH

		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;

		DECLARE @ErrorMessage NVARCHAR(4000);

		SET @ErrorMessage = CONCAT(
		    N'Wyst¹pi³ b³¹d w trakcie otwierania dokumentu - ',
		    ERROR_MESSAGE()
		);

		THROW 51029, @ErrorMessage, 1;
	
	END CATCH
END
GO


SELECT * FROM SBD.Adresy

select * from SBD.Dokumenty

SELECT * FROM SBD.Magazyny