CREATE OR ALTER PROCEDURE SBD.ZmienSektor
	@Id INT,
	@Sektor NVARCHAR(50),
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

		IF NOT EXISTS (SELECT 1 FROM SBD.Sektory WHERE Kod = @Sektor)
			THROW 51029, N'nie istnieje sektor z takim kodem.', 1

		IF @Typ NOT IN (N'Docelowy', N'ród³owy')
			THROW 51029, N'nierozpoznany typ sektora. Dostêpne: ród³owy/Docelowy', 1

		EXEC SBD.WalidacjaBlokady @DokumentId = @Id, @Operator = @Operator

		DECLARE @StarySektor NVARCHAR(50), @TypDokumentu NVARCHAR(50);

		SELECT @StarySektor = CASE WHEN @Typ = N'ród³owy' THEN SektorZrodlowyKod
									  WHEN @Typ = N'Docelowy' THEN SektorDocelowyKod END,
				@TypDokumentu = TypDokumentu
									  FROM SBD.Dokumenty WHERE ID = @Id


		IF @TypDokumentu = N'PM' AND @Typ = N'ród³owy'
			THROW 51029, N'nie mo¿na ustawiæ sektora Ÿród³owego dokumentom przychodu.', 1

		IF @TypDokumentu = N'WM' AND @Typ = N'Docelowy'
			THROW 51029, N'nie mo¿na ustawiæ sektora docelowego dokumentom rozchodu.', 1

		





		IF @StartedTran = 1
			COMMIT TRAN;

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