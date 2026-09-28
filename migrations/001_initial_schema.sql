-- =====================================================================
-- M7: ilk kalici sema.
--
-- KAPSAM (bilincli olarak dar): yalniz M7'nin ihtiyaci olan yedi tablo.
-- Ekonomi, rating gecmisi, scout, transfer, lig tablosu YOKTUR. 03
-- "Gelecekteki veri modellerinin tamamina ilk milestone'da tablo acma"
-- diyor; bir tablo acmak, onu doldurmak ve sonra degistirmek maliyetlidir.
--
-- DAPPER, EF CORE DEGIL. 03 generic repository'yi yasakliyor ve 04 tekil
-- kisitlarin semada gorunur olmasini istiyor. Burada UNIQUE ve CHECK
-- kisitlari SQL'de ACIKCA yazili; bir ORM bunlari gizli tutardi.
--
-- MIGRASYON ARACI YOK. EF Core yok, DbUp yok, Flyway yok. Tek dosya,
-- elle uygulanir, `schema_migrations` tablosuyla kaydedilir. Bu M7 icin
-- yeterli; M11'de gercek bir arac degerlendirilebilir.
-- =====================================================================

CREATE TABLE IF NOT EXISTS schema_migrations (
    version     text        PRIMARY KEY,
    applied_at  timestamptz NOT NULL DEFAULT now()
);

-- ---------------------------------------------------------------------
-- users
-- 04: "kimlik saglayicisi ayrica secilir". Burada JWT konusuyoruz;
-- parola/hesap yonetimi M8+ kapsaminda.
-- ---------------------------------------------------------------------
CREATE TABLE users (
    id            uuid        PRIMARY KEY,
    display_name  varchar(80) NOT NULL CHECK (length(btrim(display_name)) > 0),
    created_at    timestamptz NOT NULL
);

COMMENT ON TABLE users IS 'Hesap. Sifre/oturum yonetimi M8 kapsaminda.';

-- ---------------------------------------------------------------------
-- players
-- D111: oyuncu KISI BASINA bir kopyadir. owner_user_id, satiri kimin
-- kontrol ettigini belirler; baska bir kullanici bu satiri guncelleyemez
-- (yetki denetimi use case katmaninda, bu yalniz bütünlük).
--
-- ratings JSONB: 18 alan duz bir tablo olsaydi 18 sutun ve 18 migration
-- gerekirdi. M7'de rating'ler DEGISMEZ (degisim M11+). Tek sutun + sabit
-- alan sirasi hem yeterli hem kanonik. Alan sirasi RatingsCodec'de tek
-- kaynaktir; JSONB anahtar sirasini korumaz, ama biz yazarken sabit
-- sirayla yaziyoruz ve test bunu dogruluyor.
-- ---------------------------------------------------------------------
CREATE TABLE players (
    id             uuid         PRIMARY KEY,
    owner_user_id  uuid         NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    display_name   varchar(80)  NOT NULL CHECK (length(btrim(display_name)) > 0),
    position       smallint      NOT NULL CHECK (position BETWEEN 0 AND 4),
    ratings        jsonb        NOT NULL CHECK (jsonb_typeof(ratings) = 'object'),
    created_at     timestamptz  NOT NULL
);

CREATE INDEX players_owner_idx ON players (owner_user_id);

COMMENT ON COLUMN players.ratings IS
    'PlayerRatings: 18 alan. Alan adlari ve sirasi RatingsCodec ile sabittir.';

-- ---------------------------------------------------------------------
-- teams
-- ---------------------------------------------------------------------
CREATE TABLE teams (
    id            uuid        PRIMARY KEY,
    owner_user_id uuid        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    name          varchar(80) NOT NULL CHECK (length(btrim(name)) > 0),
    created_at    timestamptz NOT NULL
);

CREATE INDEX teams_owner_idx ON teams (owner_user_id);

-- ---------------------------------------------------------------------
-- roster_entries
-- D111: kadro boyutu SERBESTIR; lineup 5 olmasi motor kuralidir (D31) ve
-- burada hiçbir kisit yoktur.
-- Bilesik anahtar ayni oyuncunun iki kez eklenmesini engeller.
-- ---------------------------------------------------------------------
CREATE TABLE roster_entries (
    team_id    uuid        NOT NULL REFERENCES teams (id)   ON DELETE CASCADE,
    player_id  uuid        NOT NULL REFERENCES players (id) ON DELETE CASCADE,
    added_at   timestamptz NOT NULL,
    PRIMARY KEY (team_id, player_id)
);

CREATE INDEX roster_entries_player_idx ON roster_entries (player_id);

-- ---------------------------------------------------------------------
-- matches
--
-- lifecycle ile motorun MatchStatus'U AYRI kavramlardir:
--   lifecycle = sunucuda macin YASAMI  (Running / Completed / Aborted)
--   MatchStatus = motorun bitis SONUCU (Completed / Aborted)
-- Motor tarafi NotStarted'ta iken sunucu tarafi 'Running' olabilir.
--
-- D113: sunucu yeniden baslarsa Running maclar Aborted olur; sebep
-- abort_reason'a yazilir ve odul VERILMEZ. Burada 'odul' sutunu
-- YOKTUR, cunku M7'de ekonomi yok (Q15, M10).
-- ---------------------------------------------------------------------
CREATE TABLE matches (
    id                   uuid        PRIMARY KEY,
    owner_user_id        uuid        NOT NULL REFERENCES users (id) ON DELETE CASCADE,

    lifecycle            text        NOT NULL
                                 CHECK (lifecycle IN ('Running', 'Completed', 'Aborted')),

    -- 03: surum ve tekr uretilebilirlik. Bu dort sutun olmadan bir mac
    -- kaydi "hangi motorla, hangi kuralla, hangi dengeyle oynandi"
    -- sorusuna CEVAP VEREMEZ.
    seed                 numeric(20, 0) NOT NULL CHECK (seed >= 0),
    config_hash          char(16)    NOT NULL,
    engine_version       text        NOT NULL,
    rules_version        text        NOT NULL,

    -- D87: MatchSetup icin kanonik SHA-256. record.Equals CALISMAZ
    -- (ImmutableArray referans esitligi); bu bizim gecer gecimiz.
    setup_digest         char(64)    NOT NULL,

    home_team_id         uuid        NOT NULL REFERENCES teams (id),
    away_team_id         uuid        NOT NULL REFERENCES teams (id),
    started_at           timestamptz NOT NULL,

    completed_at         timestamptz,
    home_score           integer,
    away_score           integer,
    is_tie               boolean,
    home_possessions     integer,
    away_possessions     integer,
    elapsed_game_time_ms bigint,
    periods_played       smallint,
    abort_reason         text,

    -- Terminal bir macin skoru olmali; Running'in olmamali.
    CONSTRAINT matches_terminal_shape CHECK (
        (lifecycle = 'Running' AND completed_at IS NULL)
        OR
        (lifecycle IN ('Completed', 'Aborted') AND completed_at IS NOT NULL)
    ),

    -- Iki takim ayni olamaz.
    CONSTRAINT matches_distinct_teams CHECK (home_team_id <> away_team_id)
);

CREATE INDEX matches_owner_idx ON matches (owner_user_id);
CREATE INDEX matches_lifecycle_idx ON matches (lifecycle) WHERE lifecycle = 'Running';

COMMENT ON COLUMN matches.seed IS
    'unsigned 64-bit. bigint OLABILMEZ (isaretli); bu yuzden numeric(20,0).';
COMMENT ON COLUMN matches.setup_digest IS
    'MatchSetup kanonik SHA-256 (D87). Tekil esitlik yerine gecer.';

-- ---------------------------------------------------------------------
-- match_events
--
-- 04: "(match_id, sequence) benzersizdir." Bu PRIMARY KEY tam olarak
-- bunu garanti eder. Yeniden baglanan istemci ayni araligi tekrar
-- isteyebilir; INSERT ... ON CONFLICT DO NOTHING bunu bir hata degil,
-- BEKLENEN davranis yapar.
--
-- type smallint: MatchEventType enum'unun degeri. enum SIRASI DONDURULMUSTUR
-- (M3/M5, 25 tip). MatchEventCodec bunu ACIK bir switch ile cozer ve
-- bilinmeyen bir degerde HATA verir; sessizce yanlis payload'a cevirmez.
-- ---------------------------------------------------------------------
CREATE TABLE match_events (
    match_id             uuid        NOT NULL REFERENCES matches (id) ON DELETE CASCADE,
    sequence             bigint      NOT NULL CHECK (sequence >= 1),

    schema_version       smallint    NOT NULL,
    engine_version       text        NOT NULL,
    config_hash          char(16)    NOT NULL,
    type                 smallint    NOT NULL CHECK (type BETWEEN 0 AND 24),

    period               smallint    NOT NULL,
    game_clock_ms        bigint      NOT NULL,
    elapsed_game_time_ms bigint      NOT NULL,

    possession_id        bigint,
    action_id            bigint,
    team_id              smallint,
    player_id            uuid,
    secondary_player_id  uuid,

    payload              jsonb       NOT NULL CHECK (jsonb_typeof(payload) = 'object'),

    PRIMARY KEY (match_id, sequence)
);

COMMENT ON TABLE match_events IS
    'Kalici olay akisi. 04: (match_id, sequence) benzersiz.';

-- ---------------------------------------------------------------------
-- manager_commands
--
-- 07 §5: "Ayni CommandId yeniden gelirse ayni sonuc dondurulur."
-- PRIMARY KEY (match_id, command_id) bunu VERITABANI seviyesinde garanti
-- eder. Ikinci gonderim INSERT ... ON CONFLICT DO NOTHING ile no-op olur
-- ve yeni bir komut gibi degerlendirilmez.
--
-- ACK vs AYRIK KAYIT: applied doluysa komut state'e islendi; dolu degilse
-- yalnizca kuyruga alindi. Iki basari mesaji degildir.
-- ---------------------------------------------------------------------
CREATE TABLE manager_commands (
    match_id          uuid        NOT NULL REFERENCES matches (id) ON DELETE CASCADE,
    command_id        uuid        NOT NULL,

    -- Komutu gonderen. Sahiplik bu sutundan TEYIT EDILIR; istemcinin
    -- "ben su takimim" beyanina guvenilmez (07 §5).
    owner_user_id     uuid        NOT NULL REFERENCES users (id) ON DELETE CASCADE,

    kind              smallint    NOT NULL,
    boundary          smallint    NOT NULL,

    created_at        timestamptz NOT NULL,
    applied           boolean     NOT NULL DEFAULT false,
    rejection_reason  smallint,
    message           text,
    applied_at        timestamptz,

    PRIMARY KEY (match_id, command_id)
);

CREATE INDEX manager_commands_owner_idx ON manager_commands (owner_user_id);

-- ---------------------------------------------------------------------
-- Kapanis denetimi
--
-- Bu blok, sunucu AYAĞA KALKARKEN calisir ve sozu tutmayan semayi
-- yakalar. Tablo sayisi ve adlari burada yazili; migration ile uyusmazsa
-- sunucu ACILMAZ.
-- ---------------------------------------------------------------------
DO $$
DECLARE
    Beklenen constant text[] := ARRAY[
        'users', 'players', 'teams', 'roster_entries', 'matches',
        'match_events', 'manager_commands'];
    Gercek constant text[];
BEGIN
    SELECT coalesce(array_agg(tablename ORDER BY tablename), ARRAY[]::text[])
      INTO Gercek
      FROM pg_tables
     WHERE schemaname = 'public';

    IF Gercek <> Beklenen THEN
        RAISE EXCEPTION
            'Beklenen tablolar % ama bulunanlar %. Semayi degistirmek M7 disidir.',
            Beklenen, Gercek;
    END IF;
END
$$;
