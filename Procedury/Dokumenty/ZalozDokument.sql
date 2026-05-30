CREATE OR ALTER PROCEDURE SBD.ZalozDokument
	@TypDokumentu NVARCHAR(10),
	@DataWystawienia DATETIME = NULL,

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

		DECLARE 
			@NowyNumer INT,
			@NoweId INT;

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

		    NULL,
		    NULL,
		    NULL,

		    NULL,
		    NULL,
		    NULL,

		    NULL,
		    NULL,
		    NULL,

		    NULL,
		    NULL,
		    NULL,

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