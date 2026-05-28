CREATE OR ALTER TRIGGER SBD.WalidacjaJednostkiPodstawowejTowaru
ON SBD.Jednostki
FOR INSERT, UPDATE
AS
BEGIN
SET NOCOUNT ON;

	DECLARE @JednostkaId INT;
	DECLARE @JednostkaTowarId INT;
	DECLARE @JednostkaPrzelicznik DECIMAL(18, 6)

	DECLARE kursor CURSOR FAST_FORWARD FOR
	    SELECT Id, TowarId, Przelicznik FROM inserted;
	
	OPEN kursor;
	
	FETCH NEXT FROM kursor INTO @JednostkaId, @JednostkaTowarId, @JednostkaPrzelicznik;
	
	WHILE @@FETCH_STATUS = 0
		BEGIN

		IF @JednostkaPrzelicznik = 1 
			AND EXISTS (SELECT * FROM SBD.Jednostki WHERE TowarId = @JednostkaTowarId AND Przelicznik = 1 AND Id <> @JednostkaId)
			THROW 51029, N'towar mo¿e mieæ tylko jedn¹ jednostkê podstawow¹.', 1

		FETCH NEXT FROM kursor INTO @JednostkaId, @JednostkaTowarId, @JednostkaPrzelicznik;
	END
	
	CLOSE kursor;
	DEALLOCATE kursor;



END
GO


INSERT INTO SBD.Jednostki
(
    TowarId,
    Kod,
    Nazwa,
    Przelicznik
)
VALUES
(
    1,
    N'kwwawrt',
    N'Karwwwwton',
    1
);