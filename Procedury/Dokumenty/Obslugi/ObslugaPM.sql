CREATE OR ALTER PROCEDURE SBD.ObslugaPM
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

			UPDATE dos
				SET dos.Ilosc = 0, DataModyfikacji = GETDATE()
			FROM SBD.Dostawy dos
			JOIN SBD.Alokacje a ON a.Id = dos.ZakladajacaAlokacjaId
			WHERE a.DokumentId = @Id

			UPDATE SBD.Dokumenty SET [Status] = N'Anulowany' WHERE ID = @Id
		END
	ELSE IF @Akcja = N'Zatwierdz'
		BEGIN
			
			IF (SELECT COUNT(*) FROM SBD.Pozycje WHERE DokumentId = @Id) = 0
				THROW 51029, N'dokument nie posiada ¿adnych pozycji.', 1
	
			DECLARE @AlokacjaId INT;
			DECLARE kursorPozycji CURSOR FAST_FORWARD FOR
			    SELECT Id FROM SBD.Alokacje WHERE DokumentId = @Id;
			OPEN kursorPozycji;
			
			FETCH NEXT FROM kursorPozycji INTO @AlokacjaId;
			
			WHILE @@FETCH_STATUS = 0
			BEGIN
	
				--Przygotowanie wstêpnych danych
				SELECT p.TowarId, p.TowarKod, p.TowarNazwa, d.MagazynDocelowyId, d.SektorDocelowyId,
					p.Id AS ZakladajacaPozycja, a.Ilosc, GETDATE() AS DataUtworzenia, CAST(NULL AS DATETIME2(0)) AS DataModyfikacji,
					a.Cecha AS Cecha, a.Id AS ZakladajacaAlokacja
				INTO #dane
				FROM SBD.Alokacje a
					JOIN SBD.Pozycje p ON p.Id = a.PozycjaId
					JOIN SBD.Dokumenty d ON d.Id = a.DokumentId
				WHERE a.Id = @AlokacjaId
			
				IF (SELECT COUNT(*) FROM #dane) > 1
					THROW 51029, N'wyst¹pi³ b³¹d integralnoœci danych. Zapytanie zwróci³o wiêcej ni¿ jedn¹ alokacjê z tym samym identyfikatorem.', 1
	
				IF EXISTS
				(
				    SELECT 1
				    FROM SBD.Dostawy
				    WHERE ZakladajacaAlokacjaId = @AlokacjaId
				)
				THROW 51029, N'dostawa dla tej alokacji zosta³a ju¿ za³o¿ona.', 1;
	
	
				IF EXISTS (SELECT 1 FROM #dane WHERE SektorDocelowyId IS NULL)
				BEGIN
				    UPDATE dan
				        SET dan.SektorDocelowyId = NajmniejZapelnionySektor.SektorId
				    FROM #dane dan
				    OUTER APPLY
				    (
				        SELECT TOP 1
				              s.Id AS SektorId
				            , ISNULL(SUM(d.Ilosc), 0) AS Ilosc
				        FROM SBD.Sektory s
				        LEFT JOIN SBD.Dostawy d
				            ON d.SektorId = s.Id
				           AND d.MagazynId = dan.MagazynDocelowyId
				           AND d.Cecha = dan.Cecha
				        WHERE s.MagazynId = dan.MagazynDocelowyId
				        GROUP BY s.Id
				        ORDER BY ISNULL(SUM(d.Ilosc), 0) ASC, s.Id ASC
				    ) NajmniejZapelnionySektor
				    WHERE dan.SektorDocelowyId IS NULL;
				END
	
			    INSERT INTO SBD.Dostawy 
				(TowarId, TowarKod, TowarNazwa, MagazynId, SektorId, ZakladajacaPozycjaId, Ilosc
					, DataUtworzenia, DataModyfikacji, Cecha, ZakladajacaAlokacjaId, ZrodlowaAlokacjaId)
				SELECT TowarId, TowarKod, TowarNazwa, MagazynDocelowyId, SektorDocelowyId, ZakladajacaPozycja, Ilosc
				,	DataUtworzenia, DataModyfikacji, Cecha, ZakladajacaAlokacja, ZakladajacaAlokacja FROM #dane
				
				UPDATE SBD.Alokacje SET Kierunek = N'Przychód' WHERE Id = @AlokacjaId
				DROP TABLE #dane
			    FETCH NEXT FROM kursorPozycji INTO @AlokacjaId;
			END
			
			CLOSE kursorPozycji;
			DEALLOCATE kursorPozycji;
	
			UPDATE SBD.Dokumenty SET [Status] = N'Zatwierdzony' WHERE ID = @Id
	END
END
GO

SELECT * FROM SBD.Alokacje