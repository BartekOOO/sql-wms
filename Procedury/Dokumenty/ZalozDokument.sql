CREATE OR ALTER PROCEDURE SBD.ZalozDokument
	@TypDokumentu NVARCHAR(10),
	@DataWystawienia DATETIME = NULL,

	@MagazynZrodlowy NVARCHAR(50) = NULL,
	@SektorZrodlowy NVARCHAR(50) = NULL,

	@MagazynDocelowy NVARCHAR(50) = NULL,
	@SektorDocelowy NVARCHAR(50) = NULL,

	@Seria NVARCHAR(20) = NULL,

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

		IF @TypDokumentu NOT IN (N'PM', N'WM', N'MM') OR @TypDokumentu IS NULL
			THROW 51029, N'nierozpoznany typ dokumentu. Dozwolone: WM, PM, MM.', 1

		IF @DataWystawienia IS NULL
			SET @DataWystawienia = GETDATE()

		IF @TypDokumentu = N'PM'
		BEGIN
			IF @MagazynDocelowy IS NULL
				THROW 51029, N'dokument przychodu musi mieæ zdefiniowany magazyn docelowy.', 1

			IF @MagazynZrodlowy IS NOT NULL OR @SektorZrodlowy IS NOT NULL
				THROW 51029, N'nie mo¿na okreœliæ lokalizacji Ÿród³owej dla dokumentów przychodu.', 1
		END

		IF @TypDokumentu = N'WM'
		BEGIN
			IF @MagazynZrodlowy IS NULL
				THROW 51029, N'dokument rozchodu musi mieæ zdefiniowany magazyn Ÿród³owy.', 1

			IF @MagazynDocelowy IS NOT NULL OR @SektorDocelowy IS NOT NULL
				THROW 51029, N'nie mo¿na okreœliæ lokalizacji docelowej dla dokumentów rozchodowych.', 1
		END

		IF @TypDokumentu = N'MM'
		BEGIN
			IF @MagazynDocelowy IS NULL OR @MagazynZrodlowy IS NULL
				THROW 51029, N'dokument przesuniêcia miêdzymagazynowego musi mieæ okreœlony magazyn Ÿród³owy i docelowy.', 1

			IF @MagazynDocelowy = @MagazynZrodlowy
			BEGIN
				IF @SektorDocelowy IS NULL OR @SektorZrodlowy IS NULL
					THROW 51029, N'dokument przesuniêcia miêdzysektorowego musi mieæ okreœlony sektor Ÿród³owy i docelowy.', 1

				IF @SektorDocelowy = @SektorZrodlowy
					THROW 51029, N'sektor docelowy jest ten sam co sektor Ÿród³owy', 1
			END
		END

		IF @MagazynDocelowy IS NOT NULL AND NOT EXISTS (SELECT 1 FROM SBD.Magazyny WHERE Kod = @MagazynDocelowy)
			THROW 51029, N'magazyn docelowy o takim kodzie nie istnieje.', 1

		IF @MagazynZrodlowy IS NOT NULL AND NOT EXISTS (SELECT 1 FROM SBD.Magazyny WHERE Kod = @MagazynZrodlowy)
			THROW 51029, N'magazyn Ÿród³owy o takim kodzie nie istnieje.', 1

		IF @SektorDocelowy IS NOT NULL AND NOT EXISTS (SELECT 1 FROM SBD.Sektory WHERE Kod = @SektorDocelowy)
			THROW 51029, N'sektor docelowy o takim kodzie nie istnieje.', 1

		IF @SektorZrodlowy IS NOT NULL AND NOT EXISTS (SELECT 1 FROM SBD.Sektory WHERE Kod = @SektorZrodlowy)
			THROW 51029, N'sektor Ÿród³owy o takim kodzie nie istnieje.', 1

		IF @SektorZrodlowy IS NOT NULL
			AND (SELECT m.Kod FROM SBD.Sektory s JOIN SBD.Magazyny m ON m.Id = s.MagazynId WHERE s.Kod = @SektorZrodlowy) <> @MagazynZrodlowy
			THROW 51029, N'podany sektor nie nale¿y do tego magazynu', 1

		IF @SektorDocelowy IS NOT NULL
			AND (SELECT m.Kod FROM SBD.Sektory s JOIN SBD.Magazyny m ON m.Id = s.MagazynId WHERE s.Kod = @SektorDocelowy) <> @MagazynDocelowy
			THROW 51029, N'podany sektor nie nale¿y do tego magazynu', 1

		DECLARE 
			@MagazynZrodlowyId INT = NULL,
			@MagazynZrodlowyNazwa NVARCHAR(200) = NULL,

			@MagazynDocelowyId INT = NULL,
			@MagazynDocelowyNazwa NVARCHAR(200) = NULL,

			@SektorZrodlowyId INT = NULL,
			@SektorZrodlowyNazwa NVARCHAR(200) = NULL,

			@SektorDocelowyId INT = NULL,
			@SektorDocelowyNazwa NVARCHAR(200) = NULL,

			@NowyNumer INT,
			@NoweId INT;

		SELECT 
			@MagazynZrodlowyId = Id,
			@MagazynZrodlowyNazwa = Nazwa
		FROM SBD.Magazyny
		WHERE Kod = @MagazynZrodlowy;
		
		SELECT 
		    @MagazynDocelowyId = Id,
		    @MagazynDocelowyNazwa = Nazwa
		FROM SBD.Magazyny
		WHERE Kod = @MagazynDocelowy;
		
		SELECT 
		    @SektorZrodlowyId = Id,
		    @SektorZrodlowyNazwa = Nazwa
		FROM SBD.Sektory
		WHERE Kod = @SektorZrodlowy;
		
		SELECT 
		    @SektorDocelowyId = Id,
		    @SektorDocelowyNazwa = Nazwa
		FROM SBD.Sektory
		WHERE Kod = @SektorDocelowy;

		SELECT @NowyNumer = ISNULL(MAX(Numer), 0) + 1
			FROM SBD.Dokumenty WITH (UPDLOCK, HOLDLOCK)
			WHERE TypDokumentu = @TypDokumentu
			  AND RokDokumentu = YEAR(@DataWystawienia)
			  AND MiesiacDokumentu = MONTH(@DataWystawienia)
			  AND ISNULL(Seria, N'') = ISNULL(@Seria, N'');

		INSERT SBD.Dokumenty 
		(
		    TypDokumentu,
		    DataDokumentu,
		    Opis,
		    [Status],

		    MagazynZrodlowyId,
		    MagazynZrodlowyKod,
		    MagazynZrodlowyNazwa,

		    SektorZrodlowyId,
		    SektorZrodlowyKod,
		    SektorZrodlowyNazwa,

		    MagazynDocelowyId,
		    MagazynDocelowyKod,
		    MagazynDocelowyNazwa,

		    SektorDocelowyId,
		    SektorDocelowyKod,
		    SektorDocelowyNazwa,

		    DataUtworzenia,
		    DataModyfikacji,
		    Numer,
			Seria,
		    OperatorKod
		) 
		VALUES 
		(
		    UPPER(@TypDokumentu),
		    @DataWystawienia,
		    N'',
		    N'Szkic',

		    @MagazynZrodlowyId,
		    @MagazynZrodlowy,
		    @MagazynZrodlowyNazwa,

		    @SektorZrodlowyId,
		    @SektorZrodlowy,
		    @SektorZrodlowyNazwa,

		    @MagazynDocelowyId,
		    @MagazynDocelowy,
		    @MagazynDocelowyNazwa,

		    @SektorDocelowyId,
		    @SektorDocelowy,
		    @SektorDocelowyNazwa,

		    SYSDATETIME(),
		    NULL,
		    @NowyNumer,
			@Seria,
		    @Operator
		);

		SET @NoweId = SCOPE_IDENTITY();

		IF @StartedTran = 1
            COMMIT TRAN;
		
		SELECT CONCAT(N'Pomyœlnie za³o¿ono nowy dokument ', QUOTENAME(NumerDokumentu, N'''')) AS Odpowiedz
				,	@NoweId AS DokumentId, NumerDokumentu AS DokumentNumer
		FROM SBD.Dokumenty WHERE Id = @NoweId


	END TRY
	BEGIN CATCH

		IF @StartedTran = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRAN;

		SELECT CONCAT(N'Wyst¹pi³ b³¹d w trakcie zak³adania dokumentu - ', ERROR_MESSAGE()) AS Odpowiedz, ERROR_NUMBER() AS Kod
	
	END CATCH
END
GO


SELECT * FROM SBD.Adresy

select * from SBD.Dokumenty

SELECT * FROM SBD.Magazyny