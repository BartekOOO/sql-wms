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

			DECLARE @AlokacjaId INT, @PozycjaId INT, @Pozostalo DECIMAL(18, 6), @TowarKod NVARCHAR(200), @Cecha NVARCHAR(200);
			
			DECLARE kursorAlokacji CURSOR FAST_FORWARD FOR
			    SELECT Id FROM SBD.Alokacje WHERE DokumentId = @Id AND Kierunek = N'Szkic';

			--Zmienne obecnego cyklu pêtli poni¿ej
			DECLARE @ZbijanaDostawaId INT, @IloscZbijanejDostawy DECIMAL(18,6), @ZakladajacaAlokacjaId INT
			, @ZrodlowaAlokacjaId INT, @IloscDoZdjecia DECIMAL(18,6), @NowaDostawaId INT, @EfektywnySektorDocelowy INT;
			
			OPEN kursorAlokacji;
			
			FETCH NEXT FROM kursorAlokacji INTO @AlokacjaId;
			
			WHILE @@FETCH_STATUS = 0
			BEGIN
			
				--Ustalamy ile alokacja oczekuje do zrealizowania, jakiej cechy
			   SELECT @Pozostalo = a.Ilosc, @Cecha = a.Cecha, @PozycjaId = a.PozycjaId, @TowarKod = p.TowarKod
			   FROM SBD.Alokacje a
			   JOIN SBD.Pozycje p ON p.Id = a.PozycjaId
			   WHERE a.Id = @AlokacjaId

			   --Nape³niamy tabelkê dostawami które nas interesuj¹
			   SELECT d.* INTO #dane
			   FROM SBD.Dostawy d
					WHERE d.MagazynId = @MagazynZrodlowy
					AND (@SektorZrodlowy IS NULL OR d.SektorId = @SektorZrodlowy)
					AND d.Cecha = @Cecha 
					AND d.TowarKod = @TowarKod
					AND d.Ilosc > 0
					AND NOT EXISTS
					(
					    SELECT 1
					    FROM SBD.Alokacje a
					    WHERE a.DokumentId = @Id
					      AND 
					      (
					          a.Id = d.ZakladajacaAlokacjaId
					          OR a.Id = d.ZrodlowaAlokacjaId
					      )
					)
				ORDER BY DataUtworzenia

				SET @EfektywnySektorDocelowy = @SektorDocelowy;

				IF @EfektywnySektorDocelowy IS NULL
				BEGIN
				    SELECT TOP 1
					     @EfektywnySektorDocelowy = s.Id
					 FROM SBD.Sektory s
					 LEFT JOIN SBD.Dostawy d
					     ON d.SektorId = s.Id
					    AND d.MagazynId = @MagazynDocelowy
					    AND d.TowarKod = @TowarKod
					    AND d.Cecha = @Cecha
					    AND d.Ilosc > 0
					 WHERE s.MagazynId = @MagazynDocelowy
					 GROUP BY s.Id
					 ORDER BY 
					     ISNULL(SUM(d.Ilosc), 0),
					     s.Id;
				
				    IF @EfektywnySektorDocelowy IS NULL
				        THROW 51029, N'nie uda³o siê automatycznie wybraæ sektora docelowego.', 1;
				END;

				--Je¿eli suma naszych danych jest mniejsza ni¿ tyle ile pozostaje to nie ma sensu dalsza kontynuacja algorytmu
				IF (SELECT ISNULL(SUM(Ilosc), 0) FROM #dane) < @Pozostalo
					THROW 51029, N'niewystarczaj¹ca iloœæ zasobów.', 1

				DELETE SBD.Alokacje WHERE Id = @AlokacjaId

				WHILE @Pozostalo > 0
				BEGIN
					--Pobieramy pierwsz¹ dostawê
					SELECT TOP 1 @ZbijanaDostawaId = Id, @IloscZbijanejDostawy = Ilosc FROM #dane
					ORDER BY DataUtworzenia, Id;

					SET @IloscDoZdjecia =
					CASE 
					    WHEN @Pozostalo < @IloscZbijanejDostawy THEN @Pozostalo
					    ELSE @IloscZbijanejDostawy
					END;

					SET @Pozostalo = @Pozostalo - @IloscDoZdjecia;

					--Zabieramy dla dostawy Iloœæ
					UPDATE SBD.Dostawy SET Ilosc =
						Ilosc - @IloscDoZdjecia,
						DataModyfikacji = GETDATE()
					WHERE Id = @ZbijanaDostawaId

					--Tworzymy alokacje rozchodow¹
					INSERT INTO SBD.Alokacje 
					(DokumentId, PozycjaId, DostawaId, Kierunek, Ilosc, DataUtworzenia, Cecha)
					SELECT @Id, @PozycjaId, @ZbijanaDostawaId, N'Rozchód', 
					@IloscDoZdjecia,
					GETDATE(), @Cecha
					FROM #dane WHERE Id = @ZbijanaDostawaId

					SET @ZrodlowaAlokacjaId = SCOPE_IDENTITY();

					--Tworzymy alokacje przychodow¹
					INSERT INTO SBD.Alokacje 
					(DokumentId, PozycjaId, DostawaId, Kierunek, Ilosc, DataUtworzenia, Cecha)
					SELECT @Id, @PozycjaId, NULL, N'Przychód', 
					@IloscDoZdjecia,
					GETDATE(), @Cecha
					FROM #dane WHERE Id = @ZbijanaDostawaId

					SET @ZakladajacaAlokacjaId = SCOPE_IDENTITY()

					INSERT INTO SBD.Dostawy 
					(TowarId, TowarKod, TowarNazwa, MagazynId, SektorId, ZakladajacaPozycjaId, Ilosc,
					DataUtworzenia, DataModyfikacji, Cecha, ZakladajacaAlokacjaId, ZrodlowaAlokacjaId)
					SELECT d.TowarId, d.TowarKod, d.TowarNazwa, @MagazynDocelowy, @EfektywnySektorDocelowy, @PozycjaId, 
					@IloscDoZdjecia,
					GETDATE(), CAST(NULL AS DATETIME), @Cecha, @ZakladajacaAlokacjaId, @ZrodlowaAlokacjaId
					FROM #dane d WHERE d.Id = @ZbijanaDostawaId

					SET @NowaDostawaId = SCOPE_IDENTITY();
					UPDATE SBD.Alokacje SET DostawaId = @NowaDostawaId WHERE Id = @ZakladajacaAlokacjaId

					UPDATE #dane SET Ilosc = Ilosc - @IloscDoZdjecia WHERE Id = @ZbijanaDostawaId

					DELETE FROM #dane
						WHERE Id = @ZbijanaDostawaId
						  AND Ilosc <= 0;

				END

				DROP TABLE #dane
			
			    FETCH NEXT FROM kursorAlokacji INTO @AlokacjaId;
			END
			
			CLOSE kursorAlokacji;
			DEALLOCATE kursorAlokacji;

			UPDATE SBD.Dokumenty SET [Status] = N'Zatwierdzony' WHERE ID = @Id
	END

END
GO


SELECT * FROM SBD.Dostawy