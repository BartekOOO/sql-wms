CREATE OR ALTER PROCEDURE SBD.WalidacjaBlokady
	@DokumentId INT,
	@Operator NVARCHAR(50)
AS
BEGIN
SET NOCOUNT ON;

	IF @Operator IS NULL
		THROW 51029, N'nie podano kodu operatora.', 1

	IF NOT EXISTS (SELECT 1 FROM SBD.Dokumenty WHERE Id = @DokumentId)
		THROW 51029, N'nie istnieje dokument z takim identyfikatorem.', 1

	DECLARE @BlokowanyPrzez NVARCHAR(50) = (SELECT OperatorKod FROM SBD.Dokumenty WHERE Id = @DokumentId)
	
	IF @BlokowanyPrzez IS NULL
		THROW 51029, N'dokument nie jest otwarty.', 1

	IF @BlokowanyPrzez <> @Operator
		THROW 51029, N'dokument jest otwarty przez innego operatora.', 1

	IF (SELECT [Status] FROM SBD.Dokumenty WHERE Id = @DokumentId) = 'Anulowany'
		THROW 51029, N'dokument zosta³ anulowany.', 1

	IF (SELECT [Status] FROM SBD.Dokumenty WHERE Id = @DokumentId) = 'Zatwierdzony'
		THROW 51029, N'dokument zosta³ zatwierdzony.', 1

END
GO