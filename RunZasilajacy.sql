DECLARE @Operator NVARCHAR(100) = N'BAWLA';

DECLARE @NowyDokument TABLE
(
    Odpowiedz NVARCHAR(MAX),
    DokumentId INT,
    DokumentNumer NVARCHAR(100)
);

DECLARE @DokumentId INT;


/* ============================================================
   RUN 1 - PM na piec wsadowy: chleby klasyczne / mieszane
   MAG-PIEC / PIEC-WSAD-01
   ============================================================ */

DELETE FROM @NowyDokument;

INSERT INTO @NowyDokument
EXEC SBD.ZalozDokument
    @TypDokumentu = N'PM',
    @DataWystawienia = '2026-06-06',
    @Seria = NULL,
    @Operator = @Operator;

SELECT @DokumentId = DokumentId FROM @NowyDokument;

EXEC SBD.ZmienMagazyn
    @Id = @DokumentId,
    @Magazyn = N'MAG-PIEC',
    @Typ = N'Docelowy',
    @Operator = @Operator;

EXEC SBD.ZmienSektor
    @Id = @DokumentId,
    @Sektor = N'PIEC-WSAD-01',
    @Typ = N'Docelowy',
    @Operator = @Operator;

EXEC SBD.DodajPozycje @TowarKod = N'CHL-PSZ-500',  @DokumentId = @DokumentId, @Ilosc = 8,  @Jednostka = N'taca', @Cecha = N'Jasno pieczony',  @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'CHL-WIEJ-800', @DokumentId = @DokumentId, @Ilosc = 6,  @Jednostka = N'taca', @Cecha = N'Mocno pieczony',  @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'CHL-FIRM-700', @DokumentId = @DokumentId, @Ilosc = 10, @Jednostka = N'taca', @Cecha = N'Standard',        @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'CHL-ZYT-720',  @DokumentId = @DokumentId, @Ilosc = 5,  @Jednostka = N'taca', @Cecha = N'¯ytni ciê¿ki',   @Operator = @Operator;

EXEC SBD.ZamknijDokument
    @Id = @DokumentId,
    @Akcja = N'Zatwierdz',
    @Operator = @Operator;


/* ============================================================
   RUN 2 - PM na piec taœmowy: bu³ki, bagietki, paluchy
   MAG-PIEC / PIEC-TASM-01
   ============================================================ */

DELETE FROM @NowyDokument;

INSERT INTO @NowyDokument
EXEC SBD.ZalozDokument
    @TypDokumentu = N'PM',
    @DataWystawienia = '2026-06-06',
    @Seria = NULL,
    @Operator = @Operator;

SELECT @DokumentId = DokumentId FROM @NowyDokument;

EXEC SBD.ZmienMagazyn
    @Id = @DokumentId,
    @Magazyn = N'MAG-PIEC',
    @Typ = N'Docelowy',
    @Operator = @Operator;

EXEC SBD.ZmienSektor
    @Id = @DokumentId,
    @Sektor = N'PIEC-TASM-01',
    @Typ = N'Docelowy',
    @Operator = @Operator;

EXEC SBD.DodajPozycje @TowarKod = N'BUL-KAJ-60',    @DokumentId = @DokumentId, @Ilosc = 4, @Jednostka = N'wozek', @Cecha = N'Poranna partia', @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'BUL-ZIAR-90',   @DokumentId = @DokumentId, @Ilosc = 5, @Jednostka = N'taca',  @Cecha = N'Mocno ziarnista', @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'BAG-KLAS-280',  @DokumentId = @DokumentId, @Ilosc = 8, @Jednostka = N'taca',  @Cecha = N'Pszenna',        @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'BAG-CZOS-300',  @DokumentId = @DokumentId, @Ilosc = 6, @Jednostka = N'taca',  @Cecha = N'Czosnkowa',      @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'PAL-MAK-110',   @DokumentId = @DokumentId, @Ilosc = 7, @Jednostka = N'taca',  @Cecha = N'Du¿o maku',      @Operator = @Operator;

EXEC SBD.ZamknijDokument
    @Id = @DokumentId,
    @Akcja = N'Zatwierdz',
    @Operator = @Operator;


/* ============================================================
   RUN 3 - PM na piec cukierniczy: s³odkie wypieki
   MAG-PIEC / PIEC-SLOD-01
   ============================================================ */

DELETE FROM @NowyDokument;

INSERT INTO @NowyDokument
EXEC SBD.ZalozDokument
    @TypDokumentu = N'PM',
    @DataWystawienia = '2026-06-06',
    @Seria = NULL,
    @Operator = @Operator;

SELECT @DokumentId = DokumentId FROM @NowyDokument;

EXEC SBD.ZmienMagazyn
    @Id = @DokumentId,
    @Magazyn = N'MAG-PIEC',
    @Typ = N'Docelowy',
    @Operator = @Operator;

EXEC SBD.ZmienSektor
    @Id = @DokumentId,
    @Sektor = N'PIEC-SLOD-01',
    @Typ = N'Docelowy',
    @Operator = @Operator;

EXEC SBD.DodajPozycje @TowarKod = N'DRO-SER-120',   @DokumentId = @DokumentId, @Ilosc = 12, @Jednostka = N'kosz', @Cecha = N'Glutenowa',      @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'DRO-SER-120',   @DokumentId = @DokumentId, @Ilosc = 4,  @Jednostka = N'kosz', @Cecha = N'Bezglutenowa',   @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'ROG-MAS-95',    @DokumentId = @DokumentId, @Ilosc = 9,  @Jednostka = N'taca', @Cecha = N'Maœlany',        @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'CHAL-MALA-350', @DokumentId = @DokumentId, @Ilosc = 12, @Jednostka = N'taca', @Cecha = N'Weekendowa',     @Operator = @Operator;

EXEC SBD.ZamknijDokument
    @Id = @DokumentId,
    @Akcja = N'Zatwierdz',
    @Operator = @Operator;


/* ============================================================
   RUN 4 - PM na bufor pe³nych wózków
   MAG-PIEC / WOZKI-PELNE-01
   Dobre pod póŸniejsze MM do magazynu gotowych wyrobów
   ============================================================ */

DELETE FROM @NowyDokument;

INSERT INTO @NowyDokument
EXEC SBD.ZalozDokument
    @TypDokumentu = N'PM',
    @DataWystawienia = '2026-06-06',
    @Seria = NULL,
    @Operator = @Operator;

SELECT @DokumentId = DokumentId FROM @NowyDokument;

EXEC SBD.ZmienMagazyn
    @Id = @DokumentId,
    @Magazyn = N'MAG-PIEC',
    @Typ = N'Docelowy',
    @Operator = @Operator;

EXEC SBD.ZmienSektor
    @Id = @DokumentId,
    @Sektor = N'WOZKI-PELNE-01',
    @Typ = N'Docelowy',
    @Operator = @Operator;

EXEC SBD.DodajPozycje @TowarKod = N'CHL-ZIAR-700', @DokumentId = @DokumentId, @Ilosc = 3, @Jednostka = N'wozek', @Cecha = N'Na ekspedycjê', @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'BUL-HAMB-90',  @DokumentId = @DokumentId, @Ilosc = 2, @Jednostka = N'wozek', @Cecha = N'Hurt',         @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'PITA-90',      @DokumentId = @DokumentId, @Ilosc = 8, @Jednostka = N'kart',  @Cecha = N'Gastro',       @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'BUL-HOTDOG-80',@DokumentId = @DokumentId, @Ilosc = 4, @Jednostka = N'taca',  @Cecha = N'Gastro',       @Operator = @Operator;

EXEC SBD.ZamknijDokument
    @Id = @DokumentId,
    @Akcja = N'Zatwierdz',
    @Operator = @Operator;


/* ============================================================
   RUN 5 - PM na stó³ gotowego pieczywa
   MAG-PIEC / STOL-GOT-01
   Dobre pod testy przesuwania z ogólnego sto³u na sektory / inne magazyny
   ============================================================ */

DELETE FROM @NowyDokument;

INSERT INTO @NowyDokument
EXEC SBD.ZalozDokument
    @TypDokumentu = N'PM',
    @DataWystawienia = '2026-06-06',
    @Seria = NULL,
    @Operator = @Operator;

SELECT @DokumentId = DokumentId FROM @NowyDokument;

EXEC SBD.ZmienMagazyn
    @Id = @DokumentId,
    @Magazyn = N'MAG-PIEC',
    @Typ = N'Docelowy',
    @Operator = @Operator;

EXEC SBD.ZmienSektor
    @Id = @DokumentId,
    @Sektor = N'STOL-GOT-01',
    @Typ = N'Docelowy',
    @Operator = @Operator;

EXEC SBD.DodajPozycje @TowarKod = N'OPK-TOR-PAP-D',  @DokumentId = @DokumentId, @Ilosc = 5,  @Jednostka = N'pak', @Cecha = N'',              @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'OPK-KART-CATER', @DokumentId = @DokumentId, @Ilosc = 4,  @Jednostka = N'pak', @Cecha = N'',              @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'FOC-ROZM-400',   @DokumentId = @DokumentId, @Ilosc = 10, @Jednostka = N'taca', @Cecha = N'Rozmaryn',      @Operator = @Operator;
EXEC SBD.DodajPozycje @TowarKod = N'CIAB-KLAS-120',  @DokumentId = @DokumentId, @Ilosc = 8,  @Jednostka = N'taca', @Cecha = N'Premium',       @Operator = @Operator;

EXEC SBD.ZamknijDokument
    @Id = @DokumentId,
    @Akcja = N'Zatwierdz',
    @Operator = @Operator;