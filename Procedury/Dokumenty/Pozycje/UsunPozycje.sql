CREATE OR ALTER PROCEDURE SBD.UsunPozycje
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

		IF NOT EXISTS (SELECT * FROM SBD.Pozycje WHERE Id = @Id)
			THROW 51029, N'nie istnieje pozycja z takim identyfikatorem.', 1

		DECLARE @DokumentId INT = (SELECT p.DokumentId FROM SBD.Pozycje p WHERE p.Id = @Id)

		EXEC SBD.WalidacjaBlokady @DokumentId = @DokumentId, @Operator = @Operator

		DELETE FROM SBD.Alokacje WHERE PozycjaId = @Id
		DELETE FROM SBD.Pozycje WHERE Id = @Id

		IF @StartedTran = 1
            COMMIT TRAN;

		SELECT N'Pomyœlnie usuniêto pozycjê' AS Odpowiedz

	END TRY
	BEGIN CATCH
		
		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;

		SELECT CONCAT(N'Wyst¹pi³ b³¹d w trakcie usuwania pozycji - ', ERROR_MESSAGE()) AS Odpowiedz, ERROR_NUMBER() AS Kod
	
	END CATCH
END
GO


SELECT * FROM SBD.Pozycje

SELECT * FROM SBD.Jednostki

SELECT * FROM SBD.Alokacje
