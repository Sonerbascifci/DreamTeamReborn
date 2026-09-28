using System.Collections.Immutable;
using DreamTeam.Application.Ports;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.Application.Tests;

/// <summary>
/// M7 test verisi. <b>SAhte motor, sahte rastgelelik, sabitlenmis cikti YOK.</b>
/// Buradaki her sahte yalnizca <i>dis dunya</i> yerine gecer (saat, bekleme,
/// depo). Motor gercek motorun kendisidir ve her zaman ayni kod calisir.
///
/// <para><b>Neden sahte?</b> Canli eslesme testinin amaci "pacer domain
/// sonucunu degistirmiyor" demek. Bunu olcmek icin duvar saati <i>kontrollu</i>
/// olmalidir: gercek saniyeler gecse test 8 dakika surerdi ve ne kadar
/// bekleme oldugu olculmezdi. Sahte saat + sahte bekleme ikisini de
/// <b>belirlenimci</b> kilar.</para>
/// </summary>
internal static class M7TestData
{
    public const int RosterSize = 10;
    public const int LineupSize = 5;

    /// <summary>
    /// M6'nin kalibre belgesi. Testler fabrika varsayilanini degil <b>belgeyi</b>
    /// kullanir; aksi halde testler kalibre edilmemis bir motoru olcer ve
    /// raporlar baska bir motoru olcer (M6 D103).
    /// </summary>
    public static EngineConfig Config()
    {
        var path = Path.Combine(RepositoryRoot, "config", "engine", "baseline.v0.1.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Balance belgesi bulunamadi: {path}. Testler belgeye baglidir.", path);
        }

        return BalanceConfigStore.Load(path).ToEngineConfig();
    }

    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DreamTeam.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Repo koku bulunamadi (DreamTeam.slnx).");
        }
    }

    public static MatchSetup Mirror(ulong seed = 12_345)
    {
        return new MatchSetup
        {
            // MatchId SEED'DEN TURETILIR. Sabit bir kimlik kullanmak iki
            // farkli maci ayni mac yapardi; yeniden baslatma ve depo testleri
            // boyle bir durumda yanlis sayilarla gecerdi.
            MatchId = MatchIdFor(seed),
            Home = TeamSetup(1, "Kuzey"),
            Away = TeamSetup(2, "Guney"),
            Seed = seed,
            Engine = new EngineIdentity
            {
                EngineVersion = EngineVersion.Current,
                RulesVersion = RulesIdentity.Current,
                BalanceConfigHash = "test",
                RngAlgorithm = RngIdentity.Algorithm,
                RngVersion = RngIdentity.Version,
            },
        };
    }

    /// <summary>Seed'den deterministik, calistirmadan calistirmaya degismeyen mac kimligi.</summary>
    public static Guid MatchIdFor(ulong seed)
    {
        Span<byte> bytes = stackalloc byte[8];

        for (var i = 0; i < 8; i++)
        {
            bytes[i] = (byte)(seed >> (i * 8));
        }

        var hash = System.Security.Cryptography.SHA256.HashData(bytes);

        return new Guid(hash.AsSpan(0, 16));
    }

    public static TeamMatchSetup TeamSetup(int teamIndex, string name) => TeamMatchSetup
        .Default(Team(teamIndex, name), Lineup(teamIndex))
        .WithTactics(OffensiveTactic.Balanced, DefensiveTactic.ManToMan, Pace.Normal);

    public static Team Team(int teamIndex, string name)
    {
        var players = new List<Player>();

        for (var slot = 0; slot < RosterSize; slot++)
        {
            var rating = 74 - (slot * 3);

            players.Add(new Player
            {
                Id = PlayerId(teamIndex, slot),
                DisplayName = $"Oyuncu {teamIndex}-{slot}",
                Position = (Position)(slot % LineupSize),
                Ratings = new PlayerRatings
                {
                    Speed = rating,
                    Strength = rating,
                    Vertical = rating,
                    Stamina = rating,
                    Inside = rating,
                    MidRange = rating,
                    ThreePoint = rating,
                    FreeThrow = rating,
                    BallHandling = rating,
                    Passing = rating,
                    OffBall = rating,
                    PostOffense = rating,
                    PerimeterDefense = rating,
                    InteriorDefense = rating,
                    Steal = rating,
                    Block = rating,
                    Rebounding = rating,
                    BasketballIQ = rating,
                },
            });
        }

        return new Team { Id = TeamId(teamIndex), Name = name, Roster = [.. players] };
    }

    public static Lineup Lineup(int teamIndex)
    {
        var ids = new List<Guid>(LineupSize);

        for (var slot = 0; slot < LineupSize; slot++)
        {
            ids.Add(PlayerId(teamIndex, slot));
        }

        return new Lineup { PlayerIds = [.. ids] };
    }

    public static Guid TeamId(int index) => Id("11a0b001", index);

    public static Guid PlayerId(int teamIndex, int slot) => Id("12a0b001", (teamIndex * 100) + slot);

    public static Guid Id(string prefix, int index) => new($"{prefix}{index:X8}0000000000000000");

    public static ScheduledManagerCommand ChangePace(TeamSide side) => new()
    {
        CommandId = Id("23a0b001", 77),
        Side = side,
        Kind = ManagerCommandKind.ChangePace,
        Payload = new CommandPayload { Pace = Pace.Fast },
        AcceptedOrder = 1,
        TargetBoundary = CommandBoundary.ActionDecision,
    };

    public static ScheduledManagerCommand Timeout(TeamSide side) => new()
    {
        CommandId = Id("23a0b001", 78),
        Side = side,
        Kind = ManagerCommandKind.RequestTimeout,
        Payload = new CommandPayload { TimeoutKind = TimeoutKind.Short20 },
        AcceptedOrder = 1,
        TargetBoundary = CommandBoundary.DeadBall,
    };
}

/// <summary>
/// <b>Her okumada ilerleyen</b> sahte saat.
///
/// <para><b>Neden?</b> Pacer duvar saatini yalniz <i>bastan sona</i> okur.
/// Gercek bir kosuda motor adimlari duvar saatinde ilerler ve pacer bu ilerlemeyi
/// gorur. Elde tutulan bir saat (<see cref="ManualClock"/>) ilerlemezse hedef
/// hep 0 kalir, motor hep "onde" sayilir ve bekleme HIC olusmaz. O zaman
/// canli/simulator esitligi testi hicbir sey olcmez: pacer hic calismamis olur.</para>
///
/// <para><b>Bu bir sahte midir? Evet, ve olmesi gerekir.</b> Deger
/// <b>sabit</b>dir: her okumada ayni miktarda ilerler, gercek saat veya
/// is parcacigi zamanlamasina bagli degildir. Ayni test her calistiginda ayni
/// bekleme dizisini uretir.</para>
///
/// <param name="millisecondsPerRead">
/// Bir motor adiminin duvar saatinde ne kadar surdugunu modeller. Buyuk
/// secilir ki duvar saati motoru YAVASLATSIN ve pacer beklemek zorunda kalsin;
/// boylece testin "pacer calisti ve sonucu degistirmedi" iddiasi olculur.
/// </param>
internal sealed class ReadAdvancingClock : IMonotonicClock
{
    private readonly long _millisecondsPerRead;
    private long _elapsed;

    public ReadAdvancingClock(long millisecondsPerRead)
    {
        _millisecondsPerRead = millisecondsPerRead;
    }

    public int ReadCount { get; private set; }

    public long ElapsedMilliseconds
    {
        get
        {
            ReadCount += 1;
            _elapsed += _millisecondsPerRead;
            return _elapsed;
        }
    }
}
