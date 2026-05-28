CREATE OR ALTER PROCEDURE SBD.DodajPozycje
	@TowarKod NVARCHAR(50),
	@DokumentId INT,
	@Ilosc DECIMAL(16,6) = NULL,
	@Jednostka NVARCHAR(20) = NULL,
	@Cecha NVARCHAR(200) = NULL,
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

		EXEC SBD.WalidacjaBlokady @DokumentId = @DokumentId, @Operator = @Operator

		IF NOT EXISTS (SELECT 1 FROM SBD.Towary WHERE Kod = @TowarKod)
			THROW 51029, N'nie istnieje towar z takim kodem.', 1

		DECLARE @TowarId INT = (SELECT Id FROM SBD.Towary WHERE Kod = @TowarKod)

		IF @Jednostka IS NULL
			SELECT TOP 1 @Jednostka = Kod FROM SBD.Jednostki WHERE TowarId = @TowarId AND Przelicznik = 1 ORDER BY DataUtworzenia

		IF NOT EXISTS (SELECT 1 FROM SBD.Jednostki WHERE Kod = @Jednostka AND TowarId = @TowarId)
			THROW 51029, N'jednostka z takim kodem nie istnieje.', 1

		DECLARE @JednostkaId INT, @JednostkaPrzelicznik DECIMAL(18, 6)
		
		SELECT @JednostkaId = Id, @JednostkaPrzelicznik = Przelicznik FROM SBD.Jednostki WHERE Kod = @Jednostka AND TowarId = @TowarId

		IF NOT EXISTS (SELECT * FROM SBD.Towary t JOIN SBD.Jednostki j ON j.TowarId = t.Id WHERE t.Kod = @TowarKod AND j.Kod = @Jednostka)
			THROW 51029, N'podany towar nie posiada takiej jednostki.', 1

		IF @Ilosc IS NULL
			SET @Ilosc = 1

		IF @Ilosc <= 0
			THROW 51029, N'iloœæ nie mo¿e byæ ujemna.', 1

		DECLARE @TowarNazwa NVARCHAR(200)
		SELECT @TowarNazwa = Nazwa FROM SBD.Towary WHERE Id = @TowarId

		INSERT INTO SBD.Pozycje (
			DokumentId,
			TowarId,
			TowarKod,
			TowarNazwa,
			JednostkaId,
			JednostkaKod,
			JednostkaPrzelicznik,
			Ilosc,
			DataUtworzenia,
			DataModyfikacji
		) VALUES (
			@DokumentId,
			@TowarId,
			@TowarKod,
			@TowarNazwa,
			@JednostkaId,
			@Jednostka,
			@JednostkaPrzelicznik,
			@Ilosc * @JednostkaPrzelicznik,
			GETDATE(),
			NULL
		)

		DECLARE @PozycjaId INT = SCOPE_IDENTITY()

		INSERT INTO SBD.Alokacje (
			DokumentId,
			PozycjaId,
			DostawaId,
			Ilosc,
			Cecha,
			DataUtworzenia
		) VALUES (
			@DokumentId,
			@PozycjaId,
			NULL,
			@Ilosc * @JednostkaPrzelicznik,
			ISNULL(@Cecha, N''),
			GETDATE()
		)

		IF @StartedTran = 1
            COMMIT TRAN;

		SELECT N'Pomyœlnie dodano now¹ pozycjê' AS Odpowiedz
				,	@PozycjaId AS PozycjaId
		FROM SBD.Pozycje WHERE Id = @PozycjaId

	END TRY
	BEGIN CATCH

		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;

		SELECT CONCAT(N'Wyst¹pi³ b³¹d w trakcie dodawania pozycji - ', ERROR_MESSAGE()) AS Odpowiedz, ERROR_NUMBER() AS Kod
	
	END CATCH
END
GO


SELECT * FROM SBD.Pozycje

SELECT * FROM SBD.Jednostki

SELECT * FROM SBD.Alokacje
