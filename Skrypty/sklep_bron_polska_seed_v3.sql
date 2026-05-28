/*
    DEMO SEED: polski koncesjonowany sklep z bronia / outdoor / akcesoria
    Schema assumed from uploaded scripts:
      SBD.Adresy(Id, Kraj, KodKraju, Wojewodztwo, Powiat, Gmina, Miejscowosc, KodPocztowy, Poczta, Ulica, NumerDomu, NumerLokalu)
      SBD.Magazyny(Id, Kod, Nazwa, AdresId, Opis)
      SBD.Towary(Id, Kod, Nazwa, Opis, KodKreskowy)
      SBD.Jednostki(Id, TowarId, Kod, Nazwa, Przelicznik)
      SBD.Sektory(Id, MagazynId, Kod, Nazwa, Opis)

    Ważna reguła jednostek:
      DLA KAŻDEGO TOWARU dokładnie jedna jednostka ma Przelicznik = 1.
      Wszystkie jednostki pomocnicze mają Przelicznik > 1.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================
   0. CZYSZCZENIE OBECNYCH DANYCH DEMONSTRACYJNYCH
   ============================================================
   Kolejność jest dzieci -> rodzice, żeby nie wywalić się na FK.
   Uwaga: to czyści dane z podstawowych tabel demo w schemacie SBD.
*/

BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID(N'SBD.Sektory', N'U') IS NOT NULL
        DELETE FROM SBD.Sektory;

    IF OBJECT_ID(N'SBD.Jednostki', N'U') IS NOT NULL
        DELETE FROM SBD.Jednostki;

    IF OBJECT_ID(N'SBD.Magazyny', N'U') IS NOT NULL
        DELETE FROM SBD.Magazyny;

    IF OBJECT_ID(N'SBD.Towary', N'U') IS NOT NULL
        DELETE FROM SBD.Towary;

    IF OBJECT_ID(N'SBD.Adresy', N'U') IS NOT NULL
        DELETE FROM SBD.Adresy;

    IF OBJECT_ID(N'SBD.Sektory', N'U') IS NOT NULL
        DBCC CHECKIDENT (N'SBD.Sektory', RESEED, 0) WITH NO_INFOMSGS;

    IF OBJECT_ID(N'SBD.Jednostki', N'U') IS NOT NULL
        DBCC CHECKIDENT (N'SBD.Jednostki', RESEED, 0) WITH NO_INFOMSGS;

    IF OBJECT_ID(N'SBD.Magazyny', N'U') IS NOT NULL
        DBCC CHECKIDENT (N'SBD.Magazyny', RESEED, 0) WITH NO_INFOMSGS;

    IF OBJECT_ID(N'SBD.Towary', N'U') IS NOT NULL
        DBCC CHECKIDENT (N'SBD.Towary', RESEED, 0) WITH NO_INFOMSGS;

    IF OBJECT_ID(N'SBD.Adresy', N'U') IS NOT NULL
        DBCC CHECKIDENT (N'SBD.Adresy', RESEED, 0) WITH NO_INFOMSGS;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
GO

/* ============================================================
   1. ADRESY
   ============================================================ */

INSERT INTO SBD.Adresy
(
    Kraj, KodKraju, Wojewodztwo, Powiat, Gmina,
    Miejscowosc, KodPocztowy, Poczta, Ulica, NumerDomu, NumerLokalu
)
SELECT v.Kraj, v.KodKraju, v.Wojewodztwo, v.Powiat, v.Gmina,
       v.Miejscowosc, v.KodPocztowy, v.Poczta, v.Ulica, v.NumerDomu, v.NumerLokalu
FROM
(
    VALUES
    (N'Polska', N'PL', N'Mazowieckie', N'warszawski zachodni', N'Ożarów Mazowiecki', N'Ożarów Mazowiecki', N'05-850', N'Ożarów Mazowiecki', N'Poznańska',      N'45',  NULL),
    (N'Polska', N'PL', N'Mazowieckie', N'warszawski zachodni', N'Błonie',              N'Błonie',              N'05-870', N'Błonie',              N'Magazynowa',    N'12',  NULL),
    (N'Polska', N'PL', N'Mazowieckie', N'warszawski zachodni', N'Błonie',              N'Błonie',              N'05-870', N'Błonie',              N'Pakowa',        N'8',   N'B')
) v(Kraj, KodKraju, Wojewodztwo, Powiat, Gmina, Miejscowosc, KodPocztowy, Poczta, Ulica, NumerDomu, NumerLokalu)
WHERE NOT EXISTS
(
    SELECT 1
    FROM SBD.Adresy a
    WHERE a.KodKraju = v.KodKraju
      AND a.Miejscowosc = v.Miejscowosc
      AND a.Ulica = v.Ulica
      AND a.NumerDomu = v.NumerDomu
);
GO

/* ============================================================
   2. MAGAZYNY
   ============================================================ */

DECLARE @AdresSklepId INT;
DECLARE @AdresGlownyId INT;
DECLARE @AdresOpakowaniaId INT;

SELECT @AdresSklepId = Id
FROM SBD.Adresy
WHERE KodKraju = N'PL' AND Miejscowosc = N'Ożarów Mazowiecki' AND Ulica = N'Poznańska' AND NumerDomu = N'45';

SELECT @AdresGlownyId = Id
FROM SBD.Adresy
WHERE KodKraju = N'PL' AND Miejscowosc = N'Błonie' AND Ulica = N'Magazynowa' AND NumerDomu = N'12';

SELECT @AdresOpakowaniaId = Id
FROM SBD.Adresy
WHERE KodKraju = N'PL' AND Miejscowosc = N'Błonie' AND Ulica = N'Pakowa' AND NumerDomu = N'8';

INSERT INTO SBD.Magazyny (Kod, Nazwa, AdresId, Opis)
SELECT v.Kod, v.Nazwa, v.AdresId, v.Opis
FROM
(
    VALUES
    (
        N'MAG-GL',
        N'Główny magazyn - Mazowieckie Centrum Dystrybucyjne',
        @AdresGlownyId,
        N'Duży magazyn zaplecza: regały wysokiego składowania, zabezpieczona strefa amunicji, przyjęcia dostaw i wydzielona sejfownia na broń.'
    ),
    (
        N'MAG-SKL',
        N'Sklep stacjonarny - Koncesjonowany Salon Broni',
        @AdresSklepId,
        N'Sala sprzedaży w polskich realiach: gabloty za ladą, ściana broni długiej, lada optyki, alejka akcesoriów, odzież outdoor i strefa obsługi koncesyjnej.'
    ),
    (
        N'MAG-OP',
        N'Mały magazyn opakowań - Zaplecze Pakowania',
        @AdresOpakowaniaId,
        N'Mały magazyn na pudełka, etykiety, folie, plomby, wkłady piankowe, protokoły pakowania i materiały do bezpiecznej wysyłki.'
    )
) v(Kod, Nazwa, AdresId, Opis)
WHERE v.AdresId IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM SBD.Magazyny m
      WHERE m.Kod = v.Kod
  );
GO

/* ============================================================
   3. SEKTORY
   ============================================================ */

DECLARE @MagGlownyId INT;
DECLARE @MagSklepId INT;
DECLARE @MagOpId INT;

SELECT @MagGlownyId = Id FROM SBD.Magazyny WHERE Kod = N'MAG-GL';
SELECT @MagSklepId  = Id FROM SBD.Magazyny WHERE Kod = N'MAG-SKL';
SELECT @MagOpId     = Id FROM SBD.Magazyny WHERE Kod = N'MAG-OP';

INSERT INTO SBD.Sektory (MagazynId, Kod, Nazwa, Opis)
SELECT v.MagazynId, v.Kod, v.Nazwa, v.Opis
FROM
(
    VALUES
    -- sklep stacjonarny / sala sprzedaży
    (@MagSklepId,  N'GBL-HG-01',    N'Gablota broń krótka 01',              N'Szklana, zamykana gablota za ladą na pistolety i rewolwery pokazowe.'),
    (@MagSklepId,  N'GBL-HG-02',    N'Gablota broń krótka premium 02',              N'Druga gablota z modelami premium, wersjami kompaktowymi i akcesoriami ekspozycyjnymi.'),
    (@MagSklepId,  N'WALL-LG',      N'Ściana broni długiej',                    N'Ściana ekspozycyjna na karabinki i strzelby, zabezpieczona zamkami ekspozycyjnymi.'),
    (@MagSklepId,  N'AMMO-CAGE',    N'Szafa amunicyjna przy ladzie',                        N'Zabezpieczona szafa przy ladzie na amunicję w pudełkach i kartonach zbiorczych.'),
    (@MagSklepId,  N'OPTICS-CTR',   N'Lada optyki',                   N'Lada z kolimatorami, lunetami, montażami, latarkami i dalmierzami.'),
    (@MagSklepId,  N'ACC-AISLE',    N'Regał akcesoriów taktycznych',       N'Regał akcesoriów: kabury, pasy, ładownice, chwyty, torby i zestawy czyszczenia.'),
    (@MagSklepId,  N'APP-WALL',     N'Ściana odzieży outdoor',                     N'Ściana z odzieżą: koszulki, bluzy, czapki, rękawice i merch sklepu.'),
    (@MagSklepId,  N'CHECKOUT',     N'Lada sprzedaży i odbioru',           N'Lada kasowa, stanowisko obsługi dokumentów i miejsce odbioru zamówień internetowych.'),

    -- główny magazyn
    (@MagGlownyId, N'VAULT-A',      N'Sejfownia A - broń bieżąca',                      N'Zabezpieczony sektor na broń długą i krótką przeznaczoną do uzupełniania sali sprzedaży.'),
    (@MagGlownyId, N'VAULT-B',      N'Sejfownia B - modele premium',            N'Zabezpieczony sektor na modele premium, limitowane i droższe egzemplarze.'),
    (@MagGlownyId, N'RACK-AMMO-01', N'Regał amunicji zbiorczej 01',                N'Regał wysokiego składowania na kartony amunicji 9mm, .45 ACP, .223 Rem i 12GA.'),
    (@MagGlownyId, N'RACK-AMMO-02', N'Regał amunicji zbiorczej 02',                N'Drugi regał na amunicję treningową, buckshot i zapas ekspozycyjny.'),
    (@MagGlownyId, N'RACK-ACC-01',  N'Regał akcesoriów 01',              N'Regał na optykę, montaże, magazynki, kabury, pasy i torby transportowe.'),
    (@MagGlownyId, N'RACK-APP-01',  N'Regał odzieży 01',                  N'Regał na odzież, rozmiary S-XXL, czapki i rękawice.'),
    (@MagGlownyId, N'RCV-DOCK',     N'Rampa przyjęć',                   N'Strefa przyjęć dostaw: kontrola ilości, kodów kreskowych, dokumentów i kierowanie na sektory.'),
    (@MagGlownyId, N'REPLENISH',    N'Bufor uzupełnienia sali sprzedaży',   N'Strefa kompletacji towaru do uzupełnienia gablot, szaf i półek sklepu.'),

    -- magazyn opakowań
    (@MagOpId,     N'BOX-SHELF-S',  N'Półka pudełek S/M',                N'Półka na małe i średnie pudełka do akcesoriów, optyki i odzieży.'),
    (@MagOpId,     N'BOX-SHELF-L',  N'Półka pudełek L/XL',               N'Półka na duże pudełka, tuby, wkłady piankowe i pudełka do broń długas.'),
    (@MagOpId,     N'LABEL-BIN',    N'Pojemnik etykiet i plomb',         N'Pojemniki z etykietami ostrzegawczymi, plombami i naklejkami wysyłkowymi.'),
    (@MagOpId,     N'WRAP-RACK',    N'Regał folii i taśm',               N'Regał na folie stretch, taśmy pakowe, narożniki i zabezpieczenia paczek.')
) v(MagazynId, Kod, Nazwa, Opis)
WHERE v.MagazynId IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM SBD.Sektory s
      WHERE s.MagazynId = v.MagazynId
        AND s.Kod = v.Kod
  );
GO

/* ============================================================
   4. TOWARY
   ============================================================ */

INSERT INTO SBD.Towary (Kod, Nazwa, Opis, KodKreskowy)
SELECT v.Kod, v.Nazwa, v.Opis, v.KodKreskowy
FROM
(
    VALUES
    -- broń / ekspozycja demo
    (N'HG-9MM-RNCHR',       N'Rancher 9 Compact 9mm',             N'Pistolet kompaktowy 9mm, wariant ekspozycyjny do gabloty Handguns. Kategoria: broń krótka.', N'860100000001'),
    (N'HG-45-MARSHAL',      N'Marshal 1911 .45 ACP',              N'Klasyczny pistolet typu 1911 .45 ACP, model premium do gabloty. Kategoria: broń krótka.', N'860100000002'),
    (N'RV-357-TRAIL',       N'Trail Boss Revolver .357',          N'Rewolwer .357 w klimacie western/outdoor, produkt pokazowy. Kategoria: broń krótka.', N'860100000003'),
    (N'LG-556-PRAIRIE',     N'Prairie Carbine 5.56',              N'Karabinek sportowo-outdoorowy 5.56 na ścianę Broń długa Wall. Kategoria: broń długa.', N'860100000004'),
    (N'SG-12-RANCH',        N'Ranch Defender Shotgun 12GA',       N'Strzelba 12GA do ekspozycji broń długa, model użytkowy/outdoor. Kategoria: broń długa.', N'860100000005'),
    (N'RF-22-PLINKER',      N'Plinker Rifle .22 LR',              N'Karabinek .22 LR do treningu i rekreacji strzeleckiej. Kategoria: broń długa.', N'860100000006'),

    -- amunicja
    (N'AM-9MM-50',          N'9mm Range FMJ - pudełko 50 szt.',    N'Amunicja treningowa 9mm w pudełku 50 sztuk. Kategoria: amunicja.', N'860200000001'),
    (N'AM-45ACP-50',        N'.45 ACP Range FMJ - pudełko 50 szt.',N'Amunicja treningowa .45 ACP w pudełku 50 sztuk. Kategoria: amunicja.', N'860200000002'),
    (N'AM-223-20',          N'.223 Rem Range - pudełko 20 szt.',   N'Amunicja .223 Rem w pudełku 20 sztuk. Kategoria: amunicja.', N'860200000003'),
    (N'AM-12GA-BUCK-25',    N'12GA Buckshot - pudełko 25 szt.',    N'Amunicja 12GA buckshot w pudełku 25 sztuk. Kategoria: amunicja.', N'860200000004'),
    (N'AM-22LR-100',        N'.22 LR Value Pack - pudełko 100 szt.',N'Amunicja .22 LR w pudełku 100 sztuk. Kategoria: amunicja.', N'860200000005'),

    -- optyka i akcesoria
    (N'OPT-RD-COYOTE',      N'Coyote Red Dot Sight',               N'Kolimator do lady optyki, szybki montaż, produkt premium. Kategoria: optyka.', N'860300000001'),
    (N'OPT-SCOPE-3X9',      N'Frontier Scope 3-9x40',              N'Luneta 3-9x40 do karabinków outdoorowych. Kategoria: optyka.', N'860300000002'),
    (N'MNT-RAIL-STD',       N'Universal Rail Mount',               N'Uniwersalny montaż szynowy do optyki i akcesoriów. Kategoria: akcesoria.', N'860300000003'),
    (N'CLN-KIT-UNIV',       N'Universal Cleaning Kit',             N'Zestaw czyszczenia w pudełku: szczotki, wyciory, ściereczki. Kategoria: akcesoria.', N'860300000004'),
    (N'HLSTR-IWB-9',        N'IWB Holster 9mm Compact',            N'Kabura wewnętrzna do kompaktowych pistoletów 9mm. Kategoria: akcesoria.', N'860300000005'),
    (N'CASE-HG-FOAM',       N'Foam Handgun Case',                  N'Twarda walizka z wkładem piankowym na broń krótką. Kategoria: akcesoria.', N'860300000006'),
    (N'CASE-LG-HARD',       N'Broń długa Hard Case',                 N'Długa walizka transportowa z pianką na broń długą. Kategoria: akcesoria.', N'860300000007'),
    (N'SAFE-CABLE-LOCK',    N'Cable Safety Lock',                  N'Linka zabezpieczająca do ekspozycji i sprzedaży detalicznej. Kategoria: akcesoria.', N'860300000008'),

    -- ubrania / merch / gadżety
    (N'TSH-FAS-BLK-M',      N'Koszulka Mazovia Arms M czarna',     N'Koszulka firmowa sklepu, rozmiar M, kolor czarny. Kategoria: ubrania/merch.', N'860400000001'),
    (N'TSH-FAS-BLK-L',      N'Koszulka Mazovia Arms L czarna',     N'Koszulka firmowa sklepu, rozmiar L, kolor czarny. Kategoria: ubrania/merch.', N'860400000002'),
    (N'HOODIE-RANGE-XL',    N'Bluza Range Crew XL',                N'Bluza z kapturem w klimacie range/outdoor, rozmiar XL. Kategoria: ubrania.', N'860400000003'),
    (N'CAP-PL-PATCH',       N'Czapka Polska Patch',                 N'Czapka z naszywką sklepu i motywem outdoor. Kategoria: gadżety/ubrania.', N'860400000004'),
    (N'PATCH-FLAG-TAN',     N'Naszywka Flag Tan',                  N'Naszywka velcro w kolorze tan, dodatek do plecaków i kurtek. Kategoria: gadżety.', N'860400000005'),
    (N'MUG-RANGE-DAY',      N'Kubek Range Day',                    N'Kubek ceramiczny z grafiką sklepu. Kategoria: gadżety.', N'860400000006'),

    -- opakowania
    (N'PKG-BOX-S',          N'Pudełko wysyłkowe S',                N'Małe pudełko do wysyłki optyki, kabur, czapek i drobnych akcesoriów. Kategoria: opakowania.', N'860500000001'),
    (N'PKG-BOX-L',          N'Pudełko wysyłkowe L',                N'Duże pudełko do wysyłki większych akcesoriów i zestawów. Kategoria: opakowania.', N'860500000002'),
    (N'PKG-LG-TUBE',        N'Długie pudło transportowe',                N'Długie pudełko transportowe do produktów broń długa i długich akcesoriów. Kategoria: opakowania.', N'860500000003'),
    (N'PKG-FOAM-INSERT',    N'Wkład piankowy uniwersalny',         N'Piankowy wkład zabezpieczający do walizek i pudeł. Kategoria: opakowania.', N'860500000004'),
    (N'PKG-TAPE-WARN',      N'Taśma ostrzegawcza wysyłkowa',        N'Taśma pakowa z nadrukiem ostrzegawczym do paczek demonstracyjnych. Kategoria: opakowania.', N'860500000005')
) v(Kod, Nazwa, Opis, KodKreskowy)
WHERE NOT EXISTS
(
    SELECT 1
    FROM SBD.Towary t
    WHERE t.Kod = v.Kod
);
GO

/* ============================================================
   5. JEDNOSTKI
   ============================================================
   Reguła: jedna jednostka bazowa na towar ma Przelicznik = 1.
   Reszta jednostek pomocniczych ma Przelicznik > 1.
*/

;WITH JednostkiDoDodania AS
(
    SELECT t.Id AS TowarId, v.KodTowaru, v.Kod, v.Nazwa, v.Przelicznik
    FROM SBD.Towary t
    JOIN
    (
        VALUES
        -- broń: bazowo sztuka, zbiorczo skrzynia/partia
        (N'HG-9MM-RNCHR',    N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'HG-9MM-RNCHR',    N'skrz', N'Skrzynia transportowa',   CAST(5 AS DECIMAL(18,6))),
        (N'HG-45-MARSHAL',   N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'HG-45-MARSHAL',   N'skrz', N'Skrzynia transportowa',   CAST(5 AS DECIMAL(18,6))),
        (N'RV-357-TRAIL',    N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'RV-357-TRAIL',    N'skrz', N'Skrzynia transportowa',   CAST(4 AS DECIMAL(18,6))),
        (N'LG-556-PRAIRIE',  N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'LG-556-PRAIRIE',  N'kart', N'Karton zbiorczy',         CAST(2 AS DECIMAL(18,6))),
        (N'SG-12-RANCH',     N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'SG-12-RANCH',     N'kart', N'Karton zbiorczy',         CAST(2 AS DECIMAL(18,6))),
        (N'RF-22-PLINKER',   N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'RF-22-PLINKER',   N'kart', N'Karton zbiorczy',         CAST(3 AS DECIMAL(18,6))),

        -- amunicja: bazowo pudełko, dalej karton/case/paleta
        (N'AM-9MM-50',       N'pud',  N'Pudełko 50 szt.',         CAST(1 AS DECIMAL(18,6))),
        (N'AM-9MM-50',       N'case', N'Case 20 pudełek',         CAST(20 AS DECIMAL(18,6))),
        (N'AM-9MM-50',       N'pal',  N'Paleta 60 case',          CAST(1200 AS DECIMAL(18,6))),
        (N'AM-45ACP-50',     N'pud',  N'Pudełko 50 szt.',         CAST(1 AS DECIMAL(18,6))),
        (N'AM-45ACP-50',     N'case', N'Case 20 pudełek',         CAST(20 AS DECIMAL(18,6))),
        (N'AM-223-20',       N'pud',  N'Pudełko 20 szt.',         CAST(1 AS DECIMAL(18,6))),
        (N'AM-223-20',       N'case', N'Case 25 pudełek',         CAST(25 AS DECIMAL(18,6))),
        (N'AM-12GA-BUCK-25', N'pud',  N'Pudełko 25 szt.',         CAST(1 AS DECIMAL(18,6))),
        (N'AM-12GA-BUCK-25', N'case', N'Case 10 pudełek',         CAST(10 AS DECIMAL(18,6))),
        (N'AM-22LR-100',     N'pud',  N'Pudełko 100 szt.',        CAST(1 AS DECIMAL(18,6))),
        (N'AM-22LR-100',     N'case', N'Case 50 pudełek',         CAST(50 AS DECIMAL(18,6))),

        -- optyka/akcesoria: bazowo sztuka, dalej karton/pakiet
        (N'OPT-RD-COYOTE',   N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'OPT-RD-COYOTE',   N'kart', N'Karton 6 szt.',           CAST(6 AS DECIMAL(18,6))),
        (N'OPT-SCOPE-3X9',   N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'OPT-SCOPE-3X9',   N'kart', N'Karton 4 szt.',           CAST(4 AS DECIMAL(18,6))),
        (N'MNT-RAIL-STD',    N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'MNT-RAIL-STD',    N'pak',  N'Pakiet 10 szt.',          CAST(10 AS DECIMAL(18,6))),
        (N'CLN-KIT-UNIV',    N'kpl',  N'Komplet',                 CAST(1 AS DECIMAL(18,6))),
        (N'CLN-KIT-UNIV',    N'kart', N'Karton 12 kompletów',     CAST(12 AS DECIMAL(18,6))),
        (N'HLSTR-IWB-9',     N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'HLSTR-IWB-9',     N'pak',  N'Pakiet 8 szt.',           CAST(8 AS DECIMAL(18,6))),
        (N'CASE-HG-FOAM',    N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'CASE-HG-FOAM',    N'kart', N'Karton 6 szt.',           CAST(6 AS DECIMAL(18,6))),
        (N'CASE-LG-HARD',    N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'CASE-LG-HARD',    N'kart', N'Karton 2 szt.',           CAST(2 AS DECIMAL(18,6))),
        (N'SAFE-CABLE-LOCK', N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'SAFE-CABLE-LOCK', N'pak',  N'Pakiet 25 szt.',          CAST(25 AS DECIMAL(18,6))),

        -- ubrania/merch/gadżety
        (N'TSH-FAS-BLK-M',   N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'TSH-FAS-BLK-M',   N'kart', N'Karton 24 szt.',          CAST(24 AS DECIMAL(18,6))),
        (N'TSH-FAS-BLK-L',   N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'TSH-FAS-BLK-L',   N'kart', N'Karton 24 szt.',          CAST(24 AS DECIMAL(18,6))),
        (N'HOODIE-RANGE-XL', N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'HOODIE-RANGE-XL', N'kart', N'Karton 12 szt.',          CAST(12 AS DECIMAL(18,6))),
        (N'CAP-PL-PATCH',    N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'CAP-PL-PATCH',    N'kart', N'Karton 36 szt.',          CAST(36 AS DECIMAL(18,6))),
        (N'PATCH-FLAG-TAN',  N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'PATCH-FLAG-TAN',  N'pak',  N'Pakiet 50 szt.',          CAST(50 AS DECIMAL(18,6))),
        (N'MUG-RANGE-DAY',   N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'MUG-RANGE-DAY',   N'kart', N'Karton 12 szt.',          CAST(12 AS DECIMAL(18,6))),

        -- opakowania: bazowo konkretna jednostka użytkowa, potem paczki/kartony
        (N'PKG-BOX-S',       N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'PKG-BOX-S',       N'pak',  N'Pakiet 50 szt.',          CAST(50 AS DECIMAL(18,6))),
        (N'PKG-BOX-L',       N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'PKG-BOX-L',       N'pak',  N'Pakiet 25 szt.',          CAST(25 AS DECIMAL(18,6))),
        (N'PKG-LG-TUBE',     N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'PKG-LG-TUBE',     N'pak',  N'Pakiet 10 szt.',          CAST(10 AS DECIMAL(18,6))),
        (N'PKG-FOAM-INSERT', N'szt',  N'Sztuka',                  CAST(1 AS DECIMAL(18,6))),
        (N'PKG-FOAM-INSERT', N'pak',  N'Pakiet 20 szt.',          CAST(20 AS DECIMAL(18,6))),
        (N'PKG-TAPE-WARN',   N'rol',  N'Rolka',                   CAST(1 AS DECIMAL(18,6))),
        (N'PKG-TAPE-WARN',   N'kart', N'Karton 36 rolek',         CAST(36 AS DECIMAL(18,6)))
    ) v(KodTowaru, Kod, Nazwa, Przelicznik)
        ON t.Kod = v.KodTowaru
)
INSERT INTO SBD.Jednostki (TowarId, Kod, Nazwa, Przelicznik)
SELECT j.TowarId, j.Kod, j.Nazwa, j.Przelicznik
FROM JednostkiDoDodania j
WHERE NOT EXISTS
(
    SELECT 1
    FROM SBD.Jednostki x
    WHERE x.TowarId = j.TowarId
      AND x.Kod = j.Kod
);
GO

/* ============================================================
   6. KONTROLA REGUŁY JEDNOSTEK
   ============================================================ */

;WITH BaseUnits AS
(
    SELECT
        t.Kod,
        t.Nazwa,
        BaseUnitsCount = SUM(CASE WHEN j.Przelicznik = 1 THEN 1 ELSE 0 END),
        UnitsCount = COUNT(*)
    FROM SBD.Towary t
    LEFT JOIN SBD.Jednostki j ON j.TowarId = t.Id
    GROUP BY t.Kod, t.Nazwa
)
SELECT *
FROM BaseUnits
WHERE BaseUnitsCount <> 1;

IF EXISTS
(
    SELECT 1
    FROM
    (
        SELECT t.Id
        FROM SBD.Towary t
        JOIN SBD.Jednostki j ON j.TowarId = t.Id
        GROUP BY t.Id
        HAVING SUM(CASE WHEN j.Przelicznik = 1 THEN 1 ELSE 0 END) <> 1
    ) x
)
BEGIN
    THROW 51000, N'Błąd danych demo: każdy towar musi mieć dokładnie jedną jednostkę z Przelicznik = 1.', 1;
END;
GO

/* ============================================================
   OPCJONALNIE: TWARDY RESET WSZYSTKICH TABEL W SCHEMACIE SBD
   ============================================================
   Tego NIE uruchamiam automatycznie, bo może usunąć też inne Twoje tabele.
   Gdybyś chciał wyczyścić absolutnie wszystkie tabele w SBD, użyj tego ręcznie.

DECLARE @Sql NVARCHAR(MAX) = N'';

SELECT @Sql = @Sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N' NOCHECK CONSTRAINT ALL;' + CHAR(13)
FROM sys.tables t
WHERE SCHEMA_NAME(t.schema_id) = N'SBD';

SELECT @Sql = @Sql + N'DELETE FROM ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N';' + CHAR(13)
FROM sys.tables t
WHERE SCHEMA_NAME(t.schema_id) = N'SBD';

SELECT @Sql = @Sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N' WITH CHECK CHECK CONSTRAINT ALL;' + CHAR(13)
FROM sys.tables t
WHERE SCHEMA_NAME(t.schema_id) = N'SBD';

EXEC sys.sp_executesql @Sql;
*/
