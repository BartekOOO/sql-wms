CREATE OR ALTER TRIGGER SBD.WalidacjaKoduSektora
ON SBD.Sektory
FOR INSERT, UPDATE
AS
BEGIN
SET NOCOUNT ON;

	IF EXISTS (SELECT 1 FROM inserted WHERE Kod = SBD.DajKluczOdpiecia())
		THROW 51029, N'taki kod sektora jest zabroniony przez system.', 1

END
GO

INSERT INTO SBD.Sektory (MagazynId, Kod, Nazwa) VALUES (1, SBD.DajKluczOdpiecia(), '')