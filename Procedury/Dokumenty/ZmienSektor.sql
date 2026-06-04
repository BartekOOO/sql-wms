CREATE OR ALTER PROCEDURE SBD.ZmienSektor
	@Id INT,
	@Sektor NVARCHAR(50),
	@Typ NVARCHAR(50), --Docelowy/èrÛd≥owy
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

		IF NOT EXISTS (SELECT 1 FROM SBD.Sektory WHERE Kod = @Sektor) AND @Sektor <> SBD.DajKluczOdpiecia()
			THROW 51029, N'nie istnieje sektor z takim kodem.', 1

		IF @Typ NOT IN (N'Docelowy', N'èrÛd≥owy')
			THROW 51029, N'nierozpoznany typ sektora. DostÍpne: èrÛd≥owy/Docelowy', 1

		EXEC SBD.WalidacjaBlokady @DokumentId = @Id, @Operator = @Operator

		DECLARE @StarySektor NVARCHAR(50), @TypDokumentu NVARCHAR(50);

		SELECT @StarySektor = CASE WHEN @Typ = N'èrÛd≥owy' THEN SektorZrodlowyKod
									  WHEN @Typ = N'Docelowy' THEN SektorDocelowyKod END,
				@TypDokumentu = TypDokumentu
									  FROM SBD.Dokumenty WHERE ID = @Id


		IF @TypDokumentu = N'PM' AND @Typ = N'èrÛd≥owy'
			THROW 51029, N'nie moøna ustawiÊ sektora ürÛd≥owego dokumentom przychodu.', 1

		IF @TypDokumentu = N'WM' AND @Typ = N'Docelowy'
			THROW 51029, N'nie moøna ustawiÊ sektora docelowego dokumentom rozchodu.', 1

		
		--Warunek specjalnie na odwrot by sprawdzic czy sektory siÍ nie pokry≥y
		IF (SELECT CASE WHEN @Typ = N'èrÛd≥owy' THEN SektorDocelowyKod ELSE SektorZrodlowyKod END
			FROM SBD.Dokumenty WHERE Id = @Id) = @Sektor
			THROW 51029, N'sektor docelowy nie moøe byÊ ten sam co ürÛd≥owy.', 1
		
		DECLARE @MagazynSektora NVARCHAR(50) = (SELECT 
			CASE WHEN @Typ = N'èrÛd≥owy' THEN MagazynZrodlowyKod ELSE MagazynDocelowyKod END FROM SBD.Dokumenty WHERE Id = @Id)

		IF NOT EXISTS (SELECT * FROM SBD.Magazyny mag JOIN SBD.Sektory sek ON sek.MagazynId = mag.Id
			WHERE mag.Kod = @MagazynSektora AND sek.Kod = @Sektor) AND @Sektor <> SBD.DajKluczOdpiecia()
			THROW 51029, N'magazyn nie posiada takiego sektora.', 1


		IF ISNULL(@StarySektor, N'') = @Sektor
		BEGIN
		    IF @StartedTran = 1
		        COMMIT TRAN;
		
		    SELECT N'Brak zmian.' AS Odpowiedz;
		    RETURN;
		END

		IF @Typ = N'èrÛd≥owy'
			BEGIN

				UPDATE dok
					SET dok.SektorZrodlowyId =
						CASE WHEN @Sektor = SBD.DajKluczOdpiecia() THEN NULL ELSE sek.Id END,
						dok.SektorZrodlowyKod =
						CASE WHEN @Sektor = SBD.DajKluczOdpiecia() THEN NULL ELSE sek.Kod END,
						dok.SektorZrodlowyNazwa = 
						CASE WHEN @Sektor = SBD.DajKluczOdpiecia() THEN NULL ELSE sek.Nazwa END,
						dok.DataModyfikacji = GETDATE()
				FROM SBD.Dokumenty dok
				LEFT JOIN SBD.Sektory sek ON sek.Kod = @Sektor
				WHERE dok.Id = @Id

			END
		ELSE
			BEGIN

				UPDATE dok
					SET dok.SektorDocelowyId =
						CASE WHEN @Sektor = SBD.DajKluczOdpiecia() THEN NULL ELSE sek.Id END,
						dok.SektorDocelowyKod =
						CASE WHEN @Sektor = SBD.DajKluczOdpiecia() THEN NULL ELSE sek.Kod END,
						dok.SektorDocelowyNazwa = 
						CASE WHEN @Sektor = SBD.DajKluczOdpiecia() THEN NULL ELSE sek.Nazwa END,
						dok.DataModyfikacji = GETDATE()
				FROM SBD.Dokumenty dok
				LEFT JOIN SBD.Sektory sek ON sek.Kod = @Sektor
				WHERE dok.Id = @Id

			END

		
		IF @StartedTran = 1
			COMMIT TRAN;

		SELECT CONCAT(N'Pomyúlnie edytowano dokument', N'.') AS Odpowiedz

	END TRY
	BEGIN CATCH

		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;

		DECLARE @ErrorMessage NVARCHAR(4000);

		SET @ErrorMessage = CONCAT(
		    N'Wystπpi≥ b≥πd w trakcie edytowania dokumentu - ',
		    ERROR_MESSAGE()
		);

		THROW 51029, @ErrorMessage, 1;
	
	END CATCH
END
GO