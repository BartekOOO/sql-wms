CREATE OR ALTER PROCEDURE SBD.ObslugaWM
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

			--Oddaniu iloœci na dostawy Ÿród³owe wskazane przez alokacje Rozchód.
			UPDATE d
			SET 
			    d.Ilosc = d.Ilosc + a.Ilosc,
			    d.DataModyfikacji = GETDATE()
			FROM SBD.Dostawy d
			JOIN SBD.Alokacje a
			    ON a.DostawaId = d.Id
			WHERE a.DokumentId = @Id
			  AND a.Kierunek = N'Rozchód';
			

			UPDATE SBD.Dokumenty SET [Status] = N'Anulowany' WHERE ID = @Id
		END
	ELSE IF @Akcja = N'Zatwierdz'
		BEGIN
			
			IF (SELECT COUNT(*) FROM SBD.Pozycje WHERE DokumentId = @Id) = 0
				THROW 51029, N'dokument nie posiada ¿adnych pozycji.', 1


			DECLARE 
				@SektorZrodlowy INT,
				@MagazynZrodlowy INT;

			SELECT 
			    @SektorZrodlowy = SektorZrodlowyId,
			    @MagazynZrodlowy = MagazynZrodlowyId
			FROM SBD.Dokumenty
			WHERE Id = @Id;

			DECLARE 
				@AlokacjaId INT,
				@PozycjaId INT,
				@Pozostalo DECIMAL(18,6),
				@TowarKod NVARCHAR(200),
				@Cecha NVARCHAR(200);
			
			DECLARE kursorAlokacji CURSOR LOCAL FAST_FORWARD FOR
            SELECT Id FROM SBD.Alokacje
            WHERE DokumentId = @Id
              AND Kierunek = N'Szkic';

			DECLARE 
			    @ZbijanaDostawaId INT,
			    @IloscZbijanejDostawy DECIMAL(18,6),
			    @IloscDoZdjecia DECIMAL(18,6);

			OPEN kursorAlokacji;

			FETCH NEXT FROM kursorAlokacji INTO @AlokacjaId;

			WHILE @@FETCH_STATUS = 0
			BEGIN

			    SELECT 
			        @Pozostalo = a.Ilosc, @Cecha = a.Cecha, @PozycjaId = a.PozycjaId,
					@TowarKod = p.TowarKod
			    FROM SBD.Alokacje a
			    JOIN SBD.Pozycje p
			        ON p.Id = a.PozycjaId
			    WHERE a.Id = @AlokacjaId;

			    SELECT d.*
			    INTO #dane
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
			      );

			    IF (SELECT ISNULL(SUM(Ilosc), 0) FROM #dane) < @Pozostalo
			        THROW 51029, N'niewystarczaj¹ca iloœæ zasobów.', 1;

			    DELETE FROM SBD.Alokacje
			    WHERE Id = @AlokacjaId;

			    WHILE @Pozostalo > 0
			    BEGIN

			        SELECT TOP 1
			            @ZbijanaDostawaId = Id,
			            @IloscZbijanejDostawy = Ilosc
			        FROM #dane
			        WHERE Ilosc > 0
			        ORDER BY DataUtworzenia, Id;

			        SET @IloscDoZdjecia =
			            CASE
			                WHEN @Pozostalo < @IloscZbijanejDostawy THEN @Pozostalo
			                ELSE @IloscZbijanejDostawy
			            END;

			        SET @Pozostalo = @Pozostalo - @IloscDoZdjecia;

			        UPDATE SBD.Dostawy
			        SET 
			            Ilosc = Ilosc - @IloscDoZdjecia,
			            DataModyfikacji = GETDATE()
			        WHERE Id = @ZbijanaDostawaId;

			        INSERT INTO SBD.Alokacje
			        (
			            DokumentId,
			            PozycjaId,
			            DostawaId,
			            Kierunek,
			            Ilosc,
			            DataUtworzenia,
			            Cecha
			        )
			        VALUES
			        (
			            @Id,
			            @PozycjaId,
			            @ZbijanaDostawaId,
			            N'Rozchód',
			            @IloscDoZdjecia,
			            GETDATE(),
			            @Cecha
			        );

			        UPDATE #dane
			        SET Ilosc = Ilosc - @IloscDoZdjecia
			        WHERE Id = @ZbijanaDostawaId;

			        DELETE FROM #dane
			        WHERE Id = @ZbijanaDostawaId
			          AND Ilosc <= 0;
			    END;

			    DROP TABLE #dane;

			    FETCH NEXT FROM kursorAlokacji INTO @AlokacjaId;
			END;

			CLOSE kursorAlokacji;
			DEALLOCATE kursorAlokacji;

			UPDATE SBD.Dokumenty SET [Status] = N'Zatwierdzony' WHERE ID = @Id
	END

END
GO