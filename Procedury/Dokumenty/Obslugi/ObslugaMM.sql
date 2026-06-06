CREATE OR ALTER PROCEDURE SBD.ObslugaMM
	@Id INT,
	@Akcja NVARCHAR(20),
	@ObecnyStan NVARCHAR(20)
AS
BEGIN
SET NOCOUNT ON;
SET XACT_ABORT ON;

	IF @Akcja = N'Anuluj'
		BEGIN
			IF EXISTS (SELECT 1 FROM SBD.Alokacje a 
				JOIN SBD.Dostawy d ON d.ZakladajacaAlokacjaId = a.Id
				WHERE a.DokumentId = @Id AND a.Ilosc <> d.Ilosc)
				THROW 51029, N'dostawy tego dokumentu zosta³y przesuniête dalej.', 1

			IF 1 = 1
			THROW 51029, N'jeszcze nie zaimplementowane', 1

			UPDATE SBD.Dokumenty SET [Status] = N'Anulowany' WHERE ID = @Id
		END
	ELSE IF @Akcja = N'Zatwierdz'
		BEGIN
			
			IF (SELECT COUNT(*) FROM SBD.Pozycje WHERE DokumentId = @Id) = 0
				THROW 51029, N'dokument nie posiada ¿adnych pozycji.', 1

			DECLARE @SektorDocelowy INT, @SektorZrodlowy INT;
			DECLARE @MagazynDocelowy INT, @MagazynZrodlowy INT;
			SELECT @SektorDocelowy = SektorDocelowyId,
				   @SektorZrodlowy = SektorZrodlowyId,
				   @MagazynDocelowy = MagazynDocelowyId,
				   @MagazynZrodlowy = MagazynZrodlowyId FROM SBD.Dokumenty WHERE Id = @Id

			DECLARE @AlokacjaId INT, @Pozostalo DECIMAL(18, 6), @Cecha NVARCHAR(200);
			
			DECLARE kursorAlokacji CURSOR FAST_FORWARD FOR
			    SELECT Id FROM SBD.Alokacje WHERE DokumentId = @Id;
			
			OPEN kursorAlokacji;
			
			FETCH NEXT FROM kursorAlokacji INTO @AlokacjaId;
			
			WHILE @@FETCH_STATUS = 0
			BEGIN
			
				--Ustalamy ile alokacja oczekuje do zrealizowania, jakiej cechy
			   SELECT @Pozostalo = Ilosc, @Cecha = Cecha FROM SBD.Alokacje WHERE Id = @AlokacjaId

			   SELECT * INTO #dane FROM SBD.Dostawy 
					WHERE MagazynId = @MagazynZrodlowy
					AND (@SektorZrodlowy IS NULL OR SektorId = @SektorZrodlowy)
					AND Cecha = @Cecha AND Ilosc > 0
				ORDER BY DataUtworzenia

				IF (SELECT ISNULL(SUM(Ilosc), 0) FROM #dane) < @Pozostalo
					THROW 51029, N'niewystarczaj¹ca iloœæ zasobów.', 1

				DELETE SBD.Alokacje WHERE Id = @AlokacjaId

				WHILE @Pozostalo > 0
				BEGIN
					

				END

			
			    FETCH NEXT FROM kursorAlokacji INTO @AlokacjaId;
			END
			
			CLOSE kursorAlokacji;
			DEALLOCATE kursorAlokacji;

			IF 1 = 1
				THROW 51029, N'w trakcie implemntacji', 1
			UPDATE SBD.Dokumenty SET [Status] = N'Zatwierdzony' WHERE ID = @Id
	END

END
GO