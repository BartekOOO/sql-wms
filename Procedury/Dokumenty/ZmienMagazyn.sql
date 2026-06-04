CREATE OR ALTER PROCEDURE SBD.ZmienMagazyn
	@Id INT,
	@Magazyn NVARCHAR(50),
	@Typ NVARCHAR(50), --Docelowy/ród³owy
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

		IF NOT EXISTS (SELECT 1 FROM SBD.Dokumenty WHERE Id = @Id)
			THROW 51029, N'nie istnieje dokument z takim identyfikatorem.', 1

		IF NOT EXISTS (SELECT 1 FROM SBD.Magazyny WHERE Kod = @Magazyn)
			THROW 51029, N'nie istnieje magazyn z takim kodem.', 1

		IF @Typ NOT IN (N'Docelowy', N'ród³owy')
			THROW 51029, N'nierozpoznany typ magazynu. Dostêpne: ród³owy/Docelowy', 1

		EXEC SBD.WalidacjaBlokady @DokumentId = @Id, @Operator = @Operator

		DECLARE @StaryMagazyn NVARCHAR(50), @TypDokumentu NVARCHAR(50)

		IF NOT EXISTS (SELECT 1 FROM SBD.Magazyny WHERE Kod = @Magazyn)
			THROW 51029, N'nie istnieje magazyn z takim kodem.', 1

		SELECT @StaryMagazyn = CASE WHEN @Typ = N'ród³owy' THEN MagazynZrodlowyKod
									  WHEN @Typ = N'Docelowy' THEN MagazynDocelowyKod END,
				@TypDokumentu = TypDokumentu
									  FROM SBD.Dokumenty WHERE ID = @Id

		IF @TypDokumentu = N'PM' AND @Typ = N'ród³owy'
			THROW 51029, N'nie mo¿na ustawiæ magazynu Ÿród³owego dokumentom przychodu.', 1

		IF @TypDokumentu = N'WM' AND @Typ = N'Docelowy'
			THROW 51029, N'nie mo¿na ustawiæ magazynu docelowego dokumentom rozchodu.', 1



		IF ISNULL(@StaryMagazyn, N'') = @Magazyn
		BEGIN
			IF @StartedTran = 1
				COMMIT TRAN;
			SELECT N'Brak zmian.' AS Odpowiedz
			RETURN;
		END

		IF @Typ = N'ród³owy'
				UPDATE dok
				SET dok.MagazynZrodlowyId = mag.Id,
					dok.MagazynZrodlowyKod = mag.Kod,
					dok.MagazynZrodlowyNazwa = mag.Nazwa,
					dok.SektorZrodlowyId = NULL,
					dok.SektorZrodlowyKod = NULL,
					dok.SektorZrodlowyNazwa = NULL,
					dok.DataModyfikacji = GETDATE()
				FROM SBD.Dokumenty dok
				JOIN SBD.Magazyny mag ON mag.Kod = @Magazyn
				WHERE dok.ID = @Id

		IF @Typ = N'Docelowy'
			UPDATE dok
			SET dok.MagazynDocelowyId = mag.Id,
				dok.MagazynDocelowyKod = mag.Kod,
				dok.MagazynDocelowyNazwa = mag.Nazwa,
				dok.SektorDocelowyId = NULL,
				dok.SektorDocelowyKod = NULL,
				dok.SektorDocelowyNazwa = NULL,
				dok.DataModyfikacji = GETDATE()
			FROM SBD.Dokumenty dok
			JOIN SBD.Magazyny mag ON mag.Kod = @Magazyn
			WHERE dok.ID = @Id


		IF @StartedTran = 1
			COMMIT TRAN;

		SELECT CONCAT(N'Pomyœlnie edytowano dokument', N'.') AS Odpowiedz

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