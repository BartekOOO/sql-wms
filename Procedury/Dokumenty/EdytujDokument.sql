CREATE OR ALTER PROCEDURE SBD.EdytujDokument
	@Id INT,

	@DataDokumentu DATETIME = NULL,

	@MagazynZrodlowy NVARCHAR(50) = NULL,
	@SektorZrodlowy NVARCHAR(50) = NULL,

	@MagazynDocelowy NVARCHAR(50) = NULL,
	@SektorDocelowy NVARCHAR(50) = NULL,

	@Opis NVARCHAR(500) = NULL,

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

		IF @Operator IS NULL
			THROW 51029, N'nie podano kodu operatora.', 1

		IF NOT EXISTS (SELECT * FROM SBD.Dokumenty WHERE Id = @Id)
			THROW 51029, N'nie istnieje dokument z takim identyfikatorem.', 1

		EXEC SBD.WalidacjaBlokady @DokumentId = @Id, @Operator = @Operator

		DECLARE @StaraDataDokumentu DATETIME, @StaryMagazynZrodlowy NVARCHAR(50),
			@StarySektorZrodlowy NVARCHAR(50), @StaryMagazynDocelowy NVARCHAR(50),
			@StarySektorDocelowy NVARCHAR(50), @StaraSeria NVARCHAR(20), @StaryOpis NVARCHAR(500);

		DECLARE @AnyChanged INT = 0;
		DECLARE @TypDokumentu NVARCHAR(10);

		SELECT
			   @StaraDataDokumentu = DataDokumentu,
			   @StaryMagazynZrodlowy = MagazynZrodlowyKod,
			   @StarySektorZrodlowy = SektorZrodlowyKod,
			   @StarySektorDocelowy = SektorDocelowyKod,
			   @StaryMagazynDocelowy = MagazynDocelowyKod,
			   @StaraSeria = Seria,
			   @StaryOpis = Opis,
			   @TypDokumentu = TypDokumentu
		FROM SBD.Dokumenty WHERE Id = @Id

		IF @DataDokumentu IS NOT NULL AND @DataDokumentu <> @StaraDataDokumentu
		BEGIN
			SET @AnyChanged = 1;
			UPDATE SBD.Dokumenty SET DataDokumentu = @DataDokumentu WHERE Id = @Id
		END

		IF @MagazynZrodlowy IS NOT NULL AND @TypDokumentu = N'PM'
			THROW 51029, N'nie mo¿na ustawiæ magazynu Ÿród³owego dokumentom przychodu.', 1

		IF @SektorZrodlowy IS NOT NULL AND @TypDokumentu = N'PM'
			THROW 51029, N'nie mo¿na ustawiaæ sektora Ÿród³owego dokumentom przychodu', 1

		IF @MagazynDocelowy IS NOT NULL AND @TypDokumentu = N'WM'
			THROW 51029, N'nie mo¿na ustawiæ magazynu docelowego dokumentom rozchodu.', 1

		IF @SektorDocelowy IS NOT NULL AND @TypDokumentu = N'WM'
			THROW 51029, N'nie mo¿na ustawiaæ sektora docelowego dokumentom rozchodu', 1

		DECLARE @FinalnySektorZrodlowy NVARCHAR(50), @FinalnyMagazynZrodlowy NVARCHAR(50);
		DECLARE @FinalnySektorDocelowy NVARCHAR(50), @FinalnyMagazynDocelowy NVARCHAR(50);

		SET @FinalnyMagazynDocelowy = ISNULL(@MagazynDocelowy, @StaryMagazynDocelowy);
		SET @FinalnyMagazynZrodlowy = ISNULL(@MagazynZrodlowy, @StaryMagazynZrodlowy);
		SET @FinalnySektorDocelowy = ISNULL(@SektorDocelowy, @StarySektorDocelowy);
		SET @FinalnySektorZrodlowy = ISNULL(@SektorZrodlowy, @StarySektorZrodlowy)

		IF @FinalnySektorDocelowy = @FinalnySektorZrodlowy AND @TypDokumentu = N'MM'
			THROW 51029, N'sektor docelowy i Ÿród³owy nie mo¿e byæ ten sam.', 1

		IF @MagazynZrodlowy IS NOT NULL AND @MagazynZrodlowy <> @StaryMagazynZrodlowy
		BEGIN
			SET @AnyChanged = 1;
			IF NOT EXISTS (SELECT 1 FROM SBD.Magazyny WHERE Kod = @MagazynZrodlowy)
				THROW 51029, N'nie istnieje magazyn z takim kodem.', 1

			UPDATE dok
				SET dok.MagazynZrodlowyId = mag.Id,
					dok.MagazynZrodlowyKod = mag.Kod,
					dok.MagazynZrodlowyNazwa = mag.Nazwa
				FROM SBD.Dokumenty dok
				JOIN SBD.Magazyny mag ON mag.Kod = @FinalnyMagazynZrodlowy 
				WHERE dok.Id = @Id

		END

		IF @SektorZrodlowy IS NOT NULL AND @SektorZrodlowy <> @StarySektorZrodlowy
		BEGIN
			SET @AnyChanged = 1;
			IF @SektorZrodlowy <> SBD.DajKluczOdpiecia() AND NOT EXISTS (SELECT 1 FROM SBD.Sektory WHERE Kod = @SektorZrodlowy)
				THROW 51029, N'nie istnieje sektor z takim kodem.', 1

			IF @SektorZrodlowy = SBD.DajKluczOdpiecia()
				BEGIN
					UPDATE SBD.Dokumenty
						SET SektorZrodlowyId = NULL,
							SektorZrodlowyKod = NULL,
							SektorZrodlowyNazwa = NULL
						WHERE Id = @Id
					SET @FinalnySektorZrodlowy = NULL;
				END
			ELSE
				BEGIN
					UPDATE dok
						SET dok.SektorZrodlowyId = sek.Id,
							dok.SektorZrodlowyKod = sek.Kod,
							dok.SektorZrodlowyNazwa = sek.Nazwa
						FROM SBD.Dokumenty dok
						JOIN SBD.Sektory sek ON sek.Kod = @FinalnySektorZrodlowy
						WHERE dok.Id = @Id
				END
		END

		IF @MagazynDocelowy IS NOT NULL AND @MagazynDocelowy <> @StaryMagazynDocelowy
		BEGIN
			SET @AnyChanged = 1;
			IF NOT EXISTS (SELECT 1 FROM SBD.Magazyny WHERE Kod = @MagazynDocelowy)
				THROW 51029, N'nie istnieje magazyn z takim kodem.', 1

			UPDATE dok
				SET dok.MagazynDocelowyId = mag.Id,
					dok.MagazynDocelowyKod = mag.Kod,
					dok.MagazynDocelowyNazwa = mag.Nazwa
				FROM SBD.Dokumenty dok
				JOIN SBD.Magazyny mag ON mag.Kod = @FinalnyMagazynDocelowy 
				WHERE dok.Id = @Id

		END

		IF @SektorDocelowy IS NOT NULL AND @SektorDocelowy <> @StarySektorDocelowy
		BEGIN
			SET @AnyChanged = 1;
			IF @SektorDocelowy <> SBD.DajKluczOdpiecia() AND NOT EXISTS (SELECT 1 FROM SBD.Sektory WHERE Kod = @SektorDocelowy)
				THROW 51029, N'nie istnieje sektor z takim kodem.', 1

			IF @SektorDocelowy = SBD.DajKluczOdpiecia()
				BEGIN
					UPDATE SBD.Dokumenty
						SET SektorDocelowyId = NULL,
							SektorDocelowyKod = NULL,
							SektorDocelowyNazwa = NULL
						WHERE Id = @Id
					SET @FinalnySektorDocelowy = NULL;
				END
			ELSE
				BEGIN
					UPDATE dok
						SET dok.SektorDocelowyId = sek.Id,
							dok.SektorDocelowyKod = sek.Kod,
							dok.SektorDocelowyNazwa = sek.Nazwa
						FROM SBD.Dokumenty dok
						JOIN SBD.Sektory sek ON sek.Kod = @FinalnySektorDocelowy
						WHERE dok.Id = @Id
				END
		END
		
		IF @FinalnySektorDocelowy IS NOT NULL AND NOT EXISTS (SELECT 1 FROM 
				SBD.Sektory s JOIN SBD.Magazyny m ON m.Id = s.MagazynId
				WHERE m.Kod = @FinalnyMagazynDocelowy AND s.Kod = @FinalnySektorDocelowy)
			THROW 51029, N'magazyn docelowy nie posiada takiego sektora.', 1

		IF @FinalnySektorZrodlowy IS NOT NULL AND NOT EXISTS (SELECT 1 FROM 
				SBD.Sektory s JOIN SBD.Magazyny m ON m.Id = s.MagazynId
				WHERE m.Kod = @FinalnyMagazynZrodlowy AND s.Kod = @FinalnySektorZrodlowy)
			THROW 51029, N'magazyn Ÿród³owy nie posiada takiego sektora.', 1

		IF @Opis IS NOT NULL AND @Opis <> @StaryOpis
		BEGIN
			SET @AnyChanged = 1;
			UPDATE SBD.Dokumenty SET Opis = @Opis WHERE Id = @Id
		END

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

		SELECT CONCAT(N'Wyst¹pi³ b³¹d w trakcie edytowania dokumentu - ', ERROR_MESSAGE()) AS Odpowiedz, ERROR_NUMBER() AS Kod
	
	END CATCH
END
GO


SELECT * FROM SBD.Dokumenty

select * from SBD.Dokumenty

SELECT * FROM SBD.Magazyny