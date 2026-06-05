/*
    DEMO SEED: duża piekarnia rzemieślniczo-produkcyjna z magazynami, sektorami,
    wieloma rodzajami pieczywa oraz jednostkami pomocniczymi typu taca / wózek / kosz.

    Schema assumed from uploaded scripts:
      SBD.Adresy(Id, Kraj, KodKraju, Wojewodztwo, Powiat, Gmina, Miejscowosc, KodPocztowy, Poczta, Ulica, NumerDomu, NumerLokalu)
      SBD.Magazyny(Id, Kod, Nazwa, AdresId, Opis)
      SBD.Towary(Id, Kod, Nazwa, Opis, KodKreskowy)
      SBD.Jednostki(Id, TowarId, Kod, Nazwa, Przelicznik)
      SBD.Sektory(Id, MagazynId, Kod, Nazwa, Opis)

    Ważna reguła jednostek:
      DLA KAŻDEGO TOWARU dokładnie jedna jednostka ma Przelicznik = 1.
      Wszystkie jednostki pomocnicze mają Przelicznik > 1.

    Założenie wdrożenia:
      - Piekarnia działa w dużym obiekcie produkcyjnym z kilkoma halami.
      - Budynek piekarni i budynek sklepiku są przy dwóch bardzo bliskich ulicach.
      - Hala pieca, magazyn pieczywa, chłodnia, hala gotowych wyrobów i sklepik są modelowane jako osobne magazyny.
      - Sektory odwzorowują realne miejsca: stół wypiekowy, regały, chłodnię, sklepik, odbiór.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================
   0. CZYSZCZENIE OBECNYCH DANYCH DEMONSTRACYJNYCH
   ============================================================
   Kolejność jest dzieci -> rodzice, żeby nie wywalić się na FK.
   Jeżeli masz już dokumenty powiązane z magazynami, usuń/wyczyść je wcześniej
   albo zakomentuj ten blok i użyj samego INSERT/WHERE NOT EXISTS.
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
   ============================================================
   Dwa budynki bardzo blisko siebie:
     - budynek piekarni: duża hala produkcyjna, piec, chłodnia, magazyn pieczywa,
     - budynek sklepiku: sprzedaż i odbiór wyrobów gotowych przed piekarnią.
*/

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
    (N'Polska', N'PL', N'Mazowieckie', N'Warszawa', N'Warszawa', N'Warszawa', N'03-736', N'Warszawa', N'Piekarska', N'18', NULL),
    (N'Polska', N'PL', N'Mazowieckie', N'Warszawa', N'Warszawa', N'Warszawa', N'03-736', N'Warszawa', N'Mączna',    N'2',  NULL)
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

DECLARE @AdresPiekarniaId INT;
DECLARE @AdresSklepikId INT;

SELECT @AdresPiekarniaId = Id
FROM SBD.Adresy
WHERE KodKraju = N'PL'
  AND Miejscowosc = N'Warszawa'
  AND Ulica = N'Piekarska'
  AND NumerDomu = N'18';

SELECT @AdresSklepikId = Id
FROM SBD.Adresy
WHERE KodKraju = N'PL'
  AND Miejscowosc = N'Warszawa'
  AND Ulica = N'Mączna'
  AND NumerDomu = N'2';

INSERT INTO SBD.Magazyny (Kod, Nazwa, AdresId, Opis)
SELECT v.Kod, v.Nazwa, v.AdresId, v.Opis
FROM
(
    VALUES
    (
        N'MAG-PIEC',
        N'Magazyn główny piec - Hala wypieku',
        @AdresPiekarniaId,
        N'Duża hala produkcyjna z piecami wsadowymi i taśmowymi. Tu pieczywo schodzi z pieca, trafia na wielki stół odbiorczy, stygnie i jest kierowane dalej.'
    ),
    (
        N'MAG-PIECZYWO',
        N'Magazyn główny magazyn pieczywa - Regały świeżego wypieku',
        @AdresPiekarniaId,
        N'Główna hala magazynowania świeżego pieczywa. Wiele regałów, alejki kompletacyjne, strefy dla chlebów, bułek, bagietek, pieczywa słodkiego i zamówień hurtowych.'
    ),
    (
        N'MAG-CHLOD',
        N'Chłodnia - Pieczywo starsze i kontrolowane dojrzewanie',
        @AdresPiekarniaId,
        N'Chłodnia w budynku piekarni. Przechowywanie starszego pieczywa, pieczywa do odpieku, zwrotów jakościowych i produktów wymagających niższej temperatury.'
    ),
    (
        N'MAG-GOTOWE',
        N'Magazyn gotowych wyrobów - Hala ekspedycji i załadunku',
        @AdresPiekarniaId,
        N'Osobna hala w dużym budynku piekarni. Tu trafiają gotowe wyroby po kompletacji: pełne wózki, tace i kosze są sortowane na trasy, odkładane na bramy i ładowane do ciężarówek.'
    ),
    (
        N'MAG-SKLEP',
        N'Sklepik firmowy przed piekarnią - Magazyn sprzedaży detalicznej',
        @AdresSklepikId,
        N'Oddzielny budynek sklepiku przy drugiej ulicy, bardzo blisko hali produkcyjnej. To osobny magazyn sprzedaży detalicznej, z ladą, regałami, witryną i małym zapleczem.'
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

DECLARE @MagPiecId INT;
DECLARE @MagPieczywoId INT;
DECLARE @MagChlodId INT;
DECLARE @MagGotoweId INT;
DECLARE @MagSklepId INT;

SELECT @MagPiecId     = Id FROM SBD.Magazyny WHERE Kod = N'MAG-PIEC';
SELECT @MagPieczywoId = Id FROM SBD.Magazyny WHERE Kod = N'MAG-PIECZYWO';
SELECT @MagChlodId    = Id FROM SBD.Magazyny WHERE Kod = N'MAG-CHLOD';
SELECT @MagGotoweId   = Id FROM SBD.Magazyny WHERE Kod = N'MAG-GOTOWE';
SELECT @MagSklepId    = Id FROM SBD.Magazyny WHERE Kod = N'MAG-SKLEP';

INSERT INTO SBD.Sektory (MagazynId, Kod, Nazwa, Opis)
SELECT v.MagazynId, v.Kod, v.Nazwa, v.Opis
FROM
(
    VALUES
    -- Magazyn główny piec / hala wypieku
    (@MagPiecId,     N'PIEC-WSAD-01',       N'Piec wsadowy 01 - chleb tradycyjny',          N'Główne stanowisko wypieku chlebów pszennych, żytnich i mieszanych.'),
    (@MagPiecId,     N'PIEC-TASM-01',       N'Piec taśmowy 01 - bułki i bagietki',          N'Szybki wypiek bułek, kajzerek, paluchów, bagietek i pieczywa śniadaniowego.'),
    (@MagPiecId,     N'PIEC-SLOD-01',       N'Piec cukierniczy 01 - słodkie wypieki',       N'Sektor wypieku drożdżówek, rogalików, chałek i bułek maślanych.'),
    (@MagPiecId,     N'STOL-GOT-01',        N'Wielki stół z gotowymi pieczywami',           N'Centralny, szeroki stół odbiorczy. Tu trafiają tace prosto z pieca przed sortowaniem i rozjazdem na wózki.'),
    (@MagPiecId,     N'STOL-KONTROLA-01',   N'Stół kontroli wypieku',                       N'Miejsce kontroli koloru skórki, stopnia wypieczenia, gramatury i ewentualnych braków jakościowych.'),
    (@MagPiecId,     N'WOZKI-PUSTE-01',     N'Parking pustych wózków wypiekowych',          N'Sektor oczekiwania pustych wózków na tace po kolejnym wypieku.'),
    (@MagPiecId,     N'WOZKI-PELNE-01',     N'Bufor pełnych wózków po wypieku',             N'Krótkotrwały bufor wózków z gorącym lub stygnącym pieczywem.'),

    -- Magazyn główny magazyn pieczywa / regały świeżego wypieku
    (@MagPieczywoId, N'REGAL-CHLEB-A',      N'Regał A - chleby klasyczne',                  N'Regał na chleby pszenne, wiejskie, baltonowskie, firmowe i mieszane.'),
    (@MagPieczywoId, N'REGAL-CHLEB-B',      N'Regał B - chleby żytnie i razowe',             N'Regał na chleby żytnie, razowe, graham, orkiszowe i pieczywo z ziarnami.'),
    (@MagPieczywoId, N'REGAL-BULKI-A',      N'Regał C - bułki śniadaniowe',                 N'Regał na kajzerki, poznańskie, grahamki, bułki maślane i bułki z ziarnami.'),
    (@MagPieczywoId, N'REGAL-BAGIETKI-A',   N'Regał D - bagietki i paluchy',                N'Regał na bagietki, półbagietki, paluchy czosnkowe i pieczywo długie.'),
    (@MagPieczywoId, N'REGAL-SLOD-A',       N'Regał E - pieczywo słodkie',                  N'Regał na drożdżówki, chałki, rogale, bułki maślane i sezonowe wypieki.'),
    (@MagPieczywoId, N'REGAL-HURT-A',       N'Regał F - zamówienia hurtowe',                N'Regał kompletacyjny dla restauracji, hoteli, szkół i sklepów zewnętrznych.'),
    (@MagPieczywoId, N'ALEJKA-KOMPL-01',    N'Alejka kompletacji porannej',                 N'Główna alejka zbiórki zamówień wcześnie rano, przed wyjazdem dostaw.'),
    (@MagPieczywoId, N'BUFOR-SKLEPIK-01',   N'Bufor przesunięcia do sklepiku',              N'Miejsce odkładania tac i wózków przeznaczonych do punktu sprzedaży na ulicy Mącznej.'),

    -- Chłodnia
    (@MagChlodId,    N'CHLOD-REGAL-A',      N'Regał chłodniczy A - pieczywo starsze',       N'Regał na pieczywo z poprzedniej zmiany, przeznaczone do przeceny, przerobu lub odpieku.'),
    (@MagChlodId,    N'CHLOD-REGAL-B',      N'Regał chłodniczy B - pieczywo do odpieku',     N'Regał na produkty przeznaczone do późniejszego dopieczenia lub kontrolowanego odświeżenia.'),
    (@MagChlodId,    N'CHLOD-ZWROTY-01',    N'Strefa zwrotów jakościowych',                 N'Sektor odizolowany na zwroty, partie reklamacyjne i wyroby do oceny technologicznej.'),
    (@MagChlodId,    N'CHLOD-SLOD-01',      N'Strefa chłodna pieczywa słodkiego',           N'Chłodniejsze miejsce dla wybranych wypieków słodkich, chałek i produktów sezonowych.'),
    (@MagChlodId,    N'CHLOD-KOSZE-01',     N'Kosze pieczywa czerstwego',                   N'Miejsce na pieczywo do bułki tartej, grzanek lub dalszego przerobu.'),

    -- Magazyn gotowych wyrobów / hala ekspedycji i załadunku ciężarówek
    (@MagGotoweId,   N'EXP-BUFOR-01',       N'Bufor przyjęcia z magazynu pieczywa',          N'Sektor, do którego trafiają pełne wózki i tace po kompletacji z głównego magazynu pieczywa.'),
    (@MagGotoweId,   N'EXP-SORT-TRASY-01',  N'Sortownia tras dostawczych',                   N'Miejsce rozdzielania pieczywa na trasy: sklepy partnerskie, gastronomia, szkoły, hotele i odbiorcy hurtowi.'),
    (@MagGotoweId,   N'EXP-TRASA-A',        N'Trasa A - centrum miasta',                     N'Regał/alejka ekspedycyjna na zamówienia ładowane do pierwszych samochodów porannych.'),
    (@MagGotoweId,   N'EXP-TRASA-B',        N'Trasa B - osiedla i sklepy lokalne',           N'Sektor odkładczy dla dostaw do mniejszych sklepów i punktów osiedlowych.'),
    (@MagGotoweId,   N'EXP-TRASA-C',        N'Trasa C - gastronomia i hotele',               N'Sektor dla większych zamówień gastronomicznych, często pakowanych w kartony i pełne wózki.'),
    (@MagGotoweId,   N'EXP-RAMPA-01',       N'Rampa załadunkowa 01 - ciężarówki',            N'Pierwsza brama załadunkowa dla dużych samochodów dostawczych i ciężarówek.'),
    (@MagGotoweId,   N'EXP-RAMPA-02',       N'Rampa załadunkowa 02 - busy piekarnicze',      N'Druga brama załadunkowa dla busów i krótkich tras miejskich.'),
    (@MagGotoweId,   N'EXP-KONTROLA-01',    N'Kontrola wydań i kompletności',                N'Stanowisko sprawdzania, czy na wózku albo palecie jest komplet zamówienia przed załadunkiem.'),
    (@MagGotoweId,   N'EXP-ZWROT-OPAK-01',  N'Strefa zwrotu pustych koszy i tac',            N'Miejsce odkładania pustych koszy, tac i wózków wracających z tras.'),
    (@MagGotoweId,   N'EXP-PAK-01',         N'Pakowanie hurtowe i etykietowanie',            N'Sektor pakowania zamówień w kartony, torby zbiorcze i oznaczone zestawy dla kierowców.'),

    -- Sklepik firmowy / oddzielny magazyn sprzedaży detalicznej
    (@MagSklepId,    N'SKLEP-LADA-01',      N'Lada sprzedaży sklepiku',                     N'Frontowa lada sprzedaży detalicznej i bieżącej obsługi klientów.'),
    (@MagSklepId,    N'SKLEP-REGAL-CHLEB',  N'Regał sklepowy - chleby',                     N'Eksponowany regał sklepowy na najświeższe chleby z hali wypieku.'),
    (@MagSklepId,    N'SKLEP-REGAL-BULKI',  N'Regał sklepowy - bułki',                      N'Regał na bułki śniadaniowe, kajzerki, grahamki i bułki maślane.'),
    (@MagSklepId,    N'SKLEP-KOSZE-SLOD',   N'Kosze sklepowe - słodkie wypieki',            N'Kosze przy ladzie na rogale, drożdżówki, chałki i sezonowe wypieki.'),
    (@MagSklepId,    N'SKLEP-ZAM-01',       N'Sklepowy regał zamówień imiennych',           N'Mały regał na odbiory klientów indywidualnych, rezerwacje telefoniczne i paczki rodzinne.'),
    (@MagSklepId,    N'WITRYNA-PORANNA-01', N'Witryna poranna',                             N'Pierwsza ekspozycja dnia: najbardziej świeże pieczywo sprzedawane od otwarcia sklepiku.'),
    (@MagSklepId,    N'KASA-PAK-01',        N'Stanowisko pakowania przy kasie',             N'Miejsce pakowania pieczywa do toreb papierowych, koszyków klienta i pudeł cateringowych.'),
    (@MagSklepId,    N'SKLEP-ZAPLECZE-01',  N'Małe zaplecze sklepiku',                      N'Niewielki podręczny sektor sklepiku na torby, opakowania i krótkotrwały zapas najpopularniejszego pieczywa.')
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
    -- chleby klasyczne
    (N'CHL-PSZ-500',      N'Chleb pszenny 500 g',                    N'Klasyczny chleb pszenny, codzienny wypiek poranny, krojony lub cały.',                         N'590700000001'),
    (N'CHL-WIEJ-800',     N'Chleb wiejski 800 g',                    N'Duży bochen z grubszą skórką, wypiekany na hali pieca wsadowego.',                            N'590700000002'),
    (N'CHL-BALT-600',     N'Chleb baltonowski 600 g',                N'Popularny chleb mieszany pszenno-żytni do sprzedaży detalicznej i hurtowej.',                  N'590700000003'),
    (N'CHL-FIRM-700',     N'Chleb firmowy Piekarska 700 g',          N'Flagowy chleb piekarni z lekko kwaśnym aromatem i chrupiącą skórką.',                          N'590700000004'),
    (N'CHL-TOS-450',      N'Chleb tostowy 450 g',                    N'Miękki chleb pszenny do tostów, pakowany w porcje sklepowe.',                                  N'590700000005'),

    -- chleby żytnie, razowe, ziarna
    (N'CHL-ZYT-720',      N'Chleb żytni 720 g',                      N'Cięższy chleb żytni o wilgotnym miękiszu, dobry do dłuższego przechowania.',                    N'590700000006'),
    (N'CHL-RAZ-750',      N'Chleb razowy 750 g',                     N'Chleb razowy z mąki z pełnego przemiału, sektor regałów żytnich i razowych.',                   N'590700000007'),
    (N'CHL-GRAHAM-650',   N'Chleb graham 650 g',                     N'Chleb graham z delikatnym środkiem, popularny w zamówieniach porannych.',                       N'590700000008'),
    (N'CHL-ORK-650',      N'Chleb orkiszowy 650 g',                  N'Chleb orkiszowy z segmentu premium, często odkładany do sklepiku i zamówień imiennych.',        N'590700000009'),
    (N'CHL-ZIAR-700',     N'Chleb wieloziarnisty 700 g',             N'Chleb z mieszanką ziaren: słonecznik, siemię, dynia i sezam.',                                  N'590700000010'),
    (N'CHL-SLON-650',     N'Chleb słonecznikowy 650 g',              N'Chleb z dodatkiem słonecznika, często sprzedawany w sklepiku w godzinach porannych.',           N'590700000011'),
    (N'CHL-DYNIA-650',    N'Chleb z pestkami dyni 650 g',            N'Chleb z pestkami dyni, wypiekany partiami dla sklepiku i odbiorców hurtowych.',                 N'590700000012'),

    -- bułki
    (N'BUL-KAJ-60',       N'Bułka kajzerka 60 g',                    N'Podstawowa bułka śniadaniowa, wypiek masowy na piecu taśmowym.',                                N'590700000013'),
    (N'BUL-POZN-80',      N'Bułka poznańska 80 g',                   N'Bułka z charakterystycznym nacięciem, większa od kajzerki.',                                    N'590700000014'),
    (N'BUL-GRA-75',       N'Bułka grahamka 75 g',                    N'Bułka graham, często kompletowana do szkół i firm cateringowych.',                              N'590700000015'),
    (N'BUL-ZIAR-90',      N'Bułka z ziarnami 90 g',                  N'Bułka posypana ziarnami, magazynowana na regale bułek i w koszach sklepiku.',                   N'590700000016'),
    (N'BUL-MAS-80',       N'Bułka maślana 80 g',                     N'Delikatna bułka maślana, częściowo traktowana jako pieczywo słodkie.',                          N'590700000017'),
    (N'BUL-RODZ-85',      N'Bułka mleczna z rodzynkami 85 g',        N'Miękka bułka mleczna z rodzynkami, produkt poranny do sklepiku.',                               N'590700000018'),
    (N'BUL-HAMB-90',      N'Bułka hamburgerowa 90 g',                N'Bułka gastronomiczna dla lokali, często kompletowana hurtowo na pełne wózki.',                   N'590700000019'),
    (N'BUL-HOTDOG-80',    N'Bułka hot-dog 80 g',                     N'Podłużna bułka gastronomiczna, wydawana z regału zamówień hurtowych.',                          N'590700000020'),

    -- bagietki, paluchy, pieczywo długie
    (N'BAG-KLAS-280',     N'Bagietka klasyczna 280 g',               N'Długa bagietka pszenna, transportowana zwykle w tacach długich lub koszach.',                    N'590700000021'),
    (N'BAG-CZOS-300',     N'Bagietka czosnkowa 300 g',               N'Bagietka z masłem czosnkowym, trafia często do chłodniejszej strefy przed sprzedażą.',           N'590700000022'),
    (N'BAG-ZIAR-300',     N'Bagietka wieloziarnista 300 g',          N'Bagietka z ziarnami, produkt premium na regale pieczywa długiego.',                             N'590700000023'),
    (N'PAL-SER-120',      N'Paluch serowy 120 g',                    N'Paluch z serem, produkt przekąskowy, popularny przy ladzie sklepiku.',                          N'590700000024'),
    (N'PAL-MAK-110',      N'Paluch z makiem 110 g',                  N'Podłużne pieczywo z makiem, kompletowane na tackach po 15 sztuk.',                              N'590700000025'),

    -- słodkie i półsłodkie wypieki
    (N'DRO-SER-120',      N'Drożdżówka z serem 120 g',               N'Słodka drożdżówka z nadzieniem serowym, wypiek z pieca cukierniczego.',                         N'590700000026'),
    (N'DRO-JAG-120',      N'Drożdżówka z jagodami 120 g',            N'Drożdżówka sezonowa z jagodami, produkt sklepowy i zamówieniowy.',                              N'590700000027'),
    (N'DRO-MAK-130',      N'Drożdżówka z makiem 130 g',              N'Słodki wypiek z makiem, często sprzedawany w koszach przy ladzie.',                             N'590700000028'),
    (N'ROG-MAS-95',       N'Rogal maślany 95 g',                     N'Klasyczny rogal maślany, lekki, pakowany na tace i do koszy sklepiku.',                         N'590700000029'),
    (N'ROG-MAK-110',      N'Rogal z makiem 110 g',                   N'Rogal z posypką makową, produkt poranny i świąteczny.',                                        N'590700000030'),
    (N'CHAL-MALA-350',    N'Chałka mała 350 g',                      N'Mała chałka zaplatana, sprzedaż detaliczna i zamówienia weekendowe.',                           N'590700000031'),
    (N'CHAL-DUZA-650',    N'Chałka duża 650 g',                      N'Duża chałka zaplatana, produkt rodzinny i świąteczny.',                                        N'590700000032'),

    -- pieczywo specjalne / sezonowe / gastronomiczne
    (N'FOC-ROZM-400',     N'Focaccia z rozmarynem 400 g',            N'Pieczywo specjalne z oliwą i rozmarynem, sprzedawane w sklepie i dla gastronomii.',             N'590700000033'),
    (N'CIAB-KLAS-120',    N'Ciabatta klasyczna 120 g',               N'Włoska bułka o dużych porach, dobra dla gastronomii i kanapek premium.',                        N'590700000034'),
    (N'PITA-90',          N'Pita pszenna 90 g',                      N'Płaskie pieczywo gastronomiczne, kompletowane zwykle w pakiety.',                               N'590700000035'),
    (N'LAWASZ-100',       N'Lawasz pszenny 100 g',                   N'Cienki placek pszenny, produkt specjalny pod zamówienia gastronomiczne.',                       N'590700000036'),
    (N'GRZ-KOST-1KG',     N'Grzanki kostka 1 kg',                    N'Produkt z pieczywa czerstwego, jednostka bazowa kilogram.',                                     N'590700000037'),
    (N'BUL-TARTA-1KG',    N'Bułka tarta 1 kg',                       N'Produkt przerobu z pieczywa starszego, przechowywany w workach i kartonach.',                    N'590700000038'),

    -- opakowania sklepiku / wydania
    (N'OPK-TOR-PAP-S',    N'Torba papierowa mała',                   N'Mała torba papierowa do bułek i drobnego pieczywa.',                                            N'590700000039'),
    (N'OPK-TOR-PAP-D',    N'Torba papierowa duża',                   N'Duża torba papierowa do chlebów, bagietek i większych zamówień.',                               N'590700000040'),
    (N'OPK-KART-CATER',   N'Karton cateringowy na pieczywo',         N'Karton do pakowania zamówień firmowych i większych odbiorów.',                                  N'590700000041')
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

   Interpretacja:
     - szt = pojedynczy wyrób piekarniczy,
     - kg = jednostka bazowa dla produktów sypkich/przerobu,
     - taca = liczba sztuk na jednej tacy,
     - wozek = liczba sztuk na wózku, czyli wiele tac,
     - kosz = jednostka sklepowa/odbiorcza,
     - pak = pakiet/opakowanie zbiorcze,
     - worek/karton = jednostki dla bułki tartej, grzanek i opakowań.
*/

;WITH JednostkiDoDodania AS
(
    SELECT t.Id AS TowarId, v.KodTowaru, v.Kod, v.Nazwa, v.Przelicznik
    FROM SBD.Towary t
    JOIN
    (
        VALUES
        -- chleby klasyczne: bazowo sztuka, pomocniczo taca/wózek
        (N'CHL-PSZ-500',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-PSZ-500',    N'taca',  N'Taca 12 szt.',                    CAST(12 AS DECIMAL(18,6))),
        (N'CHL-PSZ-500',    N'wozek', N'Wózek 8 tac / 96 szt.',           CAST(96 AS DECIMAL(18,6))),
        (N'CHL-WIEJ-800',   N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-WIEJ-800',   N'taca',  N'Taca 8 szt.',                     CAST(8 AS DECIMAL(18,6))),
        (N'CHL-WIEJ-800',   N'wozek', N'Wózek 8 tac / 64 szt.',           CAST(64 AS DECIMAL(18,6))),
        (N'CHL-BALT-600',   N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-BALT-600',   N'taca',  N'Taca 10 szt.',                    CAST(10 AS DECIMAL(18,6))),
        (N'CHL-BALT-600',   N'wozek', N'Wózek 8 tac / 80 szt.',           CAST(80 AS DECIMAL(18,6))),
        (N'CHL-FIRM-700',   N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-FIRM-700',   N'taca',  N'Taca 10 szt.',                    CAST(10 AS DECIMAL(18,6))),
        (N'CHL-FIRM-700',   N'wozek', N'Wózek 8 tac / 80 szt.',           CAST(80 AS DECIMAL(18,6))),
        (N'CHL-TOS-450',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-TOS-450',    N'taca',  N'Taca 14 szt.',                    CAST(14 AS DECIMAL(18,6))),
        (N'CHL-TOS-450',    N'wozek', N'Wózek 8 tac / 112 szt.',          CAST(112 AS DECIMAL(18,6))),

        -- chleby żytnie/razowe/ziarna
        (N'CHL-ZYT-720',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-ZYT-720',    N'taca',  N'Taca 9 szt.',                     CAST(9 AS DECIMAL(18,6))),
        (N'CHL-ZYT-720',    N'wozek', N'Wózek 8 tac / 72 szt.',           CAST(72 AS DECIMAL(18,6))),
        (N'CHL-RAZ-750',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-RAZ-750',    N'taca',  N'Taca 9 szt.',                     CAST(9 AS DECIMAL(18,6))),
        (N'CHL-RAZ-750',    N'wozek', N'Wózek 8 tac / 72 szt.',           CAST(72 AS DECIMAL(18,6))),
        (N'CHL-GRAHAM-650', N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-GRAHAM-650', N'taca',  N'Taca 10 szt.',                    CAST(10 AS DECIMAL(18,6))),
        (N'CHL-GRAHAM-650', N'wozek', N'Wózek 8 tac / 80 szt.',           CAST(80 AS DECIMAL(18,6))),
        (N'CHL-ORK-650',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-ORK-650',    N'taca',  N'Taca 10 szt.',                    CAST(10 AS DECIMAL(18,6))),
        (N'CHL-ORK-650',    N'kosz',  N'Kosz sklepowy 6 szt.',            CAST(6 AS DECIMAL(18,6))),
        (N'CHL-ZIAR-700',   N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-ZIAR-700',   N'taca',  N'Taca 10 szt.',                    CAST(10 AS DECIMAL(18,6))),
        (N'CHL-ZIAR-700',   N'wozek', N'Wózek 8 tac / 80 szt.',           CAST(80 AS DECIMAL(18,6))),
        (N'CHL-SLON-650',   N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-SLON-650',   N'taca',  N'Taca 10 szt.',                    CAST(10 AS DECIMAL(18,6))),
        (N'CHL-DYNIA-650',  N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHL-DYNIA-650',  N'taca',  N'Taca 10 szt.',                    CAST(10 AS DECIMAL(18,6))),

        -- bułki: bazowo sztuka, duże przeliczniki na tace/wózki
        (N'BUL-KAJ-60',     N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'BUL-KAJ-60',     N'taca',  N'Taca 30 szt.',                    CAST(30 AS DECIMAL(18,6))),
        (N'BUL-KAJ-60',     N'wozek', N'Wózek 10 tac / 300 szt.',         CAST(300 AS DECIMAL(18,6))),
        (N'BUL-KAJ-60',     N'kosz',  N'Kosz sklepowy 40 szt.',           CAST(40 AS DECIMAL(18,6))),
        (N'BUL-POZN-80',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'BUL-POZN-80',    N'taca',  N'Taca 24 szt.',                    CAST(24 AS DECIMAL(18,6))),
        (N'BUL-POZN-80',    N'wozek', N'Wózek 10 tac / 240 szt.',         CAST(240 AS DECIMAL(18,6))),
        (N'BUL-GRA-75',     N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'BUL-GRA-75',     N'taca',  N'Taca 24 szt.',                    CAST(24 AS DECIMAL(18,6))),
        (N'BUL-GRA-75',     N'wozek', N'Wózek 10 tac / 240 szt.',         CAST(240 AS DECIMAL(18,6))),
        (N'BUL-ZIAR-90',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'BUL-ZIAR-90',    N'taca',  N'Taca 20 szt.',                    CAST(20 AS DECIMAL(18,6))),
        (N'BUL-ZIAR-90',    N'wozek', N'Wózek 10 tac / 200 szt.',         CAST(200 AS DECIMAL(18,6))),
        (N'BUL-MAS-80',     N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'BUL-MAS-80',     N'taca',  N'Taca 24 szt.',                    CAST(24 AS DECIMAL(18,6))),
        (N'BUL-MAS-80',     N'kosz',  N'Kosz sklepowy 30 szt.',           CAST(30 AS DECIMAL(18,6))),
        (N'BUL-RODZ-85',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'BUL-RODZ-85',    N'taca',  N'Taca 20 szt.',                    CAST(20 AS DECIMAL(18,6))),
        (N'BUL-HAMB-90',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'BUL-HAMB-90',    N'taca',  N'Taca 20 szt.',                    CAST(20 AS DECIMAL(18,6))),
        (N'BUL-HAMB-90',    N'wozek', N'Wózek 10 tac / 200 szt.',         CAST(200 AS DECIMAL(18,6))),
        (N'BUL-HOTDOG-80',  N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'BUL-HOTDOG-80',  N'taca',  N'Taca 25 szt.',                    CAST(25 AS DECIMAL(18,6))),
        (N'BUL-HOTDOG-80',  N'wozek', N'Wózek 10 tac / 250 szt.',         CAST(250 AS DECIMAL(18,6))),

        -- bagietki i paluchy
        (N'BAG-KLAS-280',   N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'BAG-KLAS-280',   N'taca',  N'Taca długa 12 szt.',              CAST(12 AS DECIMAL(18,6))),
        (N'BAG-KLAS-280',   N'wozek', N'Wózek 8 tac / 96 szt.',           CAST(96 AS DECIMAL(18,6))),
        (N'BAG-CZOS-300',   N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'BAG-CZOS-300',   N'taca',  N'Taca długa 10 szt.',              CAST(10 AS DECIMAL(18,6))),
        (N'BAG-ZIAR-300',   N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'BAG-ZIAR-300',   N'taca',  N'Taca długa 10 szt.',              CAST(10 AS DECIMAL(18,6))),
        (N'PAL-SER-120',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'PAL-SER-120',    N'taca',  N'Taca 15 szt.',                    CAST(15 AS DECIMAL(18,6))),
        (N'PAL-SER-120',    N'kosz',  N'Kosz sklepowy 20 szt.',           CAST(20 AS DECIMAL(18,6))),
        (N'PAL-MAK-110',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'PAL-MAK-110',    N'taca',  N'Taca 15 szt.',                    CAST(15 AS DECIMAL(18,6))),

        -- słodkie wypieki
        (N'DRO-SER-120',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'DRO-SER-120',    N'taca',  N'Taca 18 szt.',                    CAST(18 AS DECIMAL(18,6))),
        (N'DRO-SER-120',    N'kosz',  N'Kosz sklepowy 12 szt.',           CAST(12 AS DECIMAL(18,6))),
        (N'DRO-JAG-120',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'DRO-JAG-120',    N'taca',  N'Taca 18 szt.',                    CAST(18 AS DECIMAL(18,6))),
        (N'DRO-MAK-130',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'DRO-MAK-130',    N'taca',  N'Taca 18 szt.',                    CAST(18 AS DECIMAL(18,6))),
        (N'ROG-MAS-95',     N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'ROG-MAS-95',     N'taca',  N'Taca 20 szt.',                    CAST(20 AS DECIMAL(18,6))),
        (N'ROG-MAK-110',    N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'ROG-MAK-110',    N'taca',  N'Taca 20 szt.',                    CAST(20 AS DECIMAL(18,6))),
        (N'CHAL-MALA-350',  N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHAL-MALA-350',  N'taca',  N'Taca 8 szt.',                     CAST(8 AS DECIMAL(18,6))),
        (N'CHAL-DUZA-650',  N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CHAL-DUZA-650',  N'taca',  N'Taca 5 szt.',                     CAST(5 AS DECIMAL(18,6))),

        -- specjalne / gastronomiczne
        (N'FOC-ROZM-400',   N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'FOC-ROZM-400',   N'taca',  N'Taca 6 szt.',                     CAST(6 AS DECIMAL(18,6))),
        (N'CIAB-KLAS-120',  N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'CIAB-KLAS-120',  N'taca',  N'Taca 18 szt.',                    CAST(18 AS DECIMAL(18,6))),
        (N'CIAB-KLAS-120',  N'wozek', N'Wózek 10 tac / 180 szt.',         CAST(180 AS DECIMAL(18,6))),
        (N'PITA-90',        N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'PITA-90',        N'pak',   N'Pakiet 10 szt.',                  CAST(10 AS DECIMAL(18,6))),
        (N'PITA-90',        N'kart',  N'Karton 10 pakietów / 100 szt.',   CAST(100 AS DECIMAL(18,6))),
        (N'LAWASZ-100',     N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'LAWASZ-100',     N'pak',   N'Pakiet 10 szt.',                  CAST(10 AS DECIMAL(18,6))),
        (N'GRZ-KOST-1KG',   N'kg',    N'Kilogram',                       CAST(1 AS DECIMAL(18,6))),
        (N'GRZ-KOST-1KG',   N'worek', N'Worek 5 kg',                      CAST(5 AS DECIMAL(18,6))),
        (N'GRZ-KOST-1KG',   N'kart',  N'Karton 4 worki / 20 kg',          CAST(20 AS DECIMAL(18,6))),
        (N'BUL-TARTA-1KG',  N'kg',    N'Kilogram',                       CAST(1 AS DECIMAL(18,6))),
        (N'BUL-TARTA-1KG',  N'worek', N'Worek 10 kg',                     CAST(10 AS DECIMAL(18,6))),
        (N'BUL-TARTA-1KG',  N'pal',   N'Paleta 50 worków / 500 kg',       CAST(500 AS DECIMAL(18,6))),

        -- opakowania sklepiku / ekspedycji
        (N'OPK-TOR-PAP-S',  N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'OPK-TOR-PAP-S',  N'pak',   N'Pakiet 100 szt.',                 CAST(100 AS DECIMAL(18,6))),
        (N'OPK-TOR-PAP-D',  N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'OPK-TOR-PAP-D',  N'pak',   N'Pakiet 100 szt.',                 CAST(100 AS DECIMAL(18,6))),
        (N'OPK-KART-CATER', N'szt',   N'Sztuka',                         CAST(1 AS DECIMAL(18,6))),
        (N'OPK-KART-CATER', N'pak',   N'Pakiet 25 szt.',                  CAST(25 AS DECIMAL(18,6)))
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
   7. PODGLĄD DANYCH WDROŻENIOWYCH
   ============================================================ */

SELECT
    m.Kod AS MagazynKod,
    m.Nazwa AS Magazyn,
    a.Ulica,
    a.NumerDomu,
    s.Kod AS SektorKod,
    s.Nazwa AS Sektor
FROM SBD.Magazyny m
JOIN SBD.Adresy a ON a.Id = m.AdresId
LEFT JOIN SBD.Sektory s ON s.MagazynId = m.Id
ORDER BY m.Kod, s.Kod;

SELECT
    t.Kod AS TowarKod,
    t.Nazwa AS Towar,
    j.Kod AS JednostkaKod,
    j.Nazwa AS Jednostka,
    j.Przelicznik
FROM SBD.Towary t
JOIN SBD.Jednostki j ON j.TowarId = t.Id
ORDER BY t.Kod, j.Przelicznik, j.Kod;
GO

