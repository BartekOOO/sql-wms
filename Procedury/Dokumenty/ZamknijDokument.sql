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


		DECLARE @TypDokumentu NVARCHAR(10), @ObecnyStan NVARCHAR(20)
		
		SELECT @TypDokumentu = TypDokumentu, @ObecnyStan = [Status]
			FROM SBD.Dokumenty WHERE Id = @Id

		IF @TypDokumentu IN (N'PM', N'MM') AND EXISTS (SELECT 1 FROM SBD.Dokumenty WHERE Id = @Id AND MagazynDocelowyKod IS NULL)
			THROW 51029, N'przed zamkniêciem dokumentu nale¿y ustawiæ jego magazyn docelowy.', 1

		IF @TypDokumentu IN (N'WM', N'MM') AND EXISTS (SELECT 1 FROM SBD.Dokumenty WHERE Id = @Id AND MagazynZrodlowyKod IS NULL)
			THROW 51029, N'przed zamkniêciem dokumentu nale¿y ustawiæ jego magazyn Ÿród³owy.', 1

		IF @ObecnyStan = N'Anulowany' AND @Akcja = N'Anuluj'
			THROW 51029, N'dokument zosta³ ju¿ anulowany.', 1

		IF @ObecnyStan = N'Zatwierdzony' AND @Akcja = N'Zatwierdz'
			THROW 51029, N'dokument zosta³ ju¿ zatwierdzony', 1

		IF @ObecnyStan = N'Szkic' AND @Akcja = N'Anuluj'
			THROW 51029, N'nie mo¿na anulowaæ szkiców.', 1

		IF @ObecnyStan = N'Anulowany' AND @Akcja = N'Usun'
			THROW 51029, N'nie mo¿na usuwaæ anulowanych dokumentów.', 1

		IF @ObecnyStan = N'Zatwierdzony' AND @Akcja = N'Usun'
			THROW 51029, N'nie mo¿na usuwaæ zatwierdzonych dokumentów.', 1

		IF @ObecnyStan = N'Anulowany' AND @Akcja = N'Zatwierdz'
			THROW 51029, N'nie mo¿na zatwierdzaæ anulowanych dokumentów.', 1

		IF @Akcja NOT IN (N'Brak', N'Usun')
		BEGIN
			IF @TypDokumentu = N'PM'
				EXEC SBD.ObslugaPM @Id, @Akcja, @ObecnyStan
			IF @TypDokumentu = N'WM'
				EXEC SBD.ObslugaWM @Id, @Akcja, @ObecnyStan
			IF @TypDokumentu = N'MM'
				EXEC SBD.ObslugaMM @Id, @Akcja, @ObecnyStan
		END

		IF @Akcja = N'Usun'
		BEGIN
			SET NOCOUNT ON;

		END

		UPDATE SBD.Dokumenty SET OperatorKod = NULL WHERE Id = @Id

		IF @StartedTran = 1
			COMMIT TRAN;

		DECLARE @Odpowiedz NVARCHAR(MAX) = CONCAT(
			N'Pomyœlnie uda³o siê ', 
			CASE WHEN @Akcja = N'Brak' THEN N'zamkn¹æ' 
				 WHEN @Akcja = N'Usun' THEN N'usun¹æ'
				 WHEN @Akcja = N'Zatwierdz' THEN N'zatwierdziæ'
				 WHEN @Akcja = N'Anuluj' THEN N'anulowaæ'
				 END, N'dokument', N'.');

		SELECT @Odpowiedz AS Odpowiedz

	END TRY
	BEGIN CATCH

		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;

		SELECT CONCAT(N'Wyst¹pi³ b³¹d w trakcie zamykania dokumentu - ', ERROR_MESSAGE()) AS Odpowiedz, ERROR_NUMBER() AS Kod
	
	END CATCH
END
GO
