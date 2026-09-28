using DreamTeam.Application.Ids;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.Setup;
using DreamTeam.Application.UseCases;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.Application.Tests;

/// <summary>
/// M7, D111: "oyuncu kisi basina kopya, kadro boyutu serbest, lineup 5".
///
/// <para><b>Bu ayrim NEDEN onemli?</b> Motor lineup'i 5 oyuncu ister (D31)
/// ve bu degistirilemez. Ama urun karari kadro boyutunu sinirlamaz. Ikisini
/// birlestirip "kadro 5 olmali" demek, motor kuralini urun kuralina
/// cevirirdi. Testler bu ayrimi ayri ayri olcer.</para>
/// </summary>
public class RosterAndLineupTests
{
    private static readonly Guid Owner = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Stranger = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private static PlayerRatings Ratings(int value = 74) => new()
    {
        Speed = value, Strength = value, Vertical = value, Stamina = value,
        Inside = value, MidRange = value, ThreePoint = value, FreeThrow = value,
        BallHandling = value, Passing = value, OffBall = value, PostOffense = value,
        PerimeterDefense = value, InteriorDefense = value, Steal = value,
        Block = value, Rebounding = value, BasketballIQ = value,
    };

    private static (CreatePlayer Player, InMemoryPlayerRepository Players, InMemoryUserRepository Users, FixedCurrentUser Current)
        BuildPlayerUseCase()
    {
        var users = new InMemoryUserRepository();
        var players = new InMemoryPlayerRepository();
        var current = new FixedCurrentUser(Owner);

        users.Seed(new UserRecord(Owner, "Oyuncu1", DateTimeOffset.UnixEpoch));

        return (new CreatePlayer(users, players, current), players, users, current);
    }

    [Fact]
    public async Task AUserCanCreateManyPlayersWithNoRosterLimit()
    {
        // D111: kadro boyutu serbest. Burada 25 oyuncu olusturulur; motor 5'ini
        // hatirlatir ve hicbiri reddetmez.
        var (useCase, players, _, _) = BuildPlayerUseCase();

        for (var i = 0; i < 25; i++)
        {
            var result = await useCase.ExecuteAsync(
                $"Oyuncu{i}", (Position)(i % 5), Ratings(70 + (i % 10)), CancellationToken.None);

            Assert.True(result.Succeeded, result.Error);
        }

        Assert.Equal(25, players.CountFor(Owner));
    }

    [Fact]
    public async Task PlayersAreOwnedPerUser()
    {
        // D111: oyuncu KISI BASINA bir kopyadir. Ayni ad iki kullanicida iki
        // farkli oyuncudur.
        var (useCase, players, _, current) = BuildPlayerUseCase();

        await useCase.ExecuteAsync("Ortak", Position.PG, Ratings(), CancellationToken.None);

        current.UserId = Stranger;
        var users = new InMemoryUserRepository();
        users.Seed(new UserRecord(Stranger, "Oyuncu2", DateTimeOffset.UnixEpoch));

        var second = new CreatePlayer(users, players, current);
        var result = await second.ExecuteAsync("Ortak", Position.PG, Ratings(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, players.CountFor(Owner) + players.CountFor(Stranger));

        // Iki kayit FARKLI kimliklere sahip.
        var mine = await players.ListForOwnerAsync(Owner, CancellationToken.None);
        var yours = await players.ListForOwnerAsync(Stranger, CancellationToken.None);

        Assert.NotEqual(mine[0].Id, yours[0].Id);
    }

    [Fact]
    public async Task TheSamePlayerNameIsRejectedForTheSameUser()
    {
        // Ayni girdi ayni kimligi uretir; ikinci deneme reddedilir. Bu bir
        // hata degil, "kimlik girdiden turetiliyor" tasariminin sonucu.
        var (useCase, _, _, _) = BuildPlayerUseCase();

        Assert.True((await useCase.ExecuteAsync(
            "Ayri", Position.SF, Ratings(), CancellationToken.None)).Succeeded);

        var second = await useCase.ExecuteAsync(
            "Ayri", Position.SF, Ratings(), CancellationToken.None);

        Assert.False(second.Succeeded);
        Assert.Contains("zaten var", second.Error);
    }

    [Fact]
    public async Task CreatingAPlayerRequiresAValidUser()
    {
        var (useCase, _, _, current) = BuildPlayerUseCase();
        current.UserId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

        var result = await useCase.ExecuteAsync(
            "Yok", Position.PG, Ratings(), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ABlankNameIsRejected()
    {
        var (useCase, _, _, _) = BuildPlayerUseCase();

        Assert.False((await useCase.ExecuteAsync(
            "   ", Position.PG, Ratings(), CancellationToken.None)).Succeeded);

        Assert.False((await useCase.ExecuteAsync(
            string.Empty, Position.PG, Ratings(), CancellationToken.None)).Succeeded);
    }

    [Fact]
    public async Task AnOverlongNameIsRejected()
    {
        var (useCase, _, _, _) = BuildPlayerUseCase();

        var result = await useCase.ExecuteAsync(
            new string('a', 81), Position.PG, Ratings(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("80", result.Error);
    }

    [Fact]
    public async Task APlayerIdIsDeterministicFromItsInput()
    {
        // Ayni girdi ayni kimligi vermeli; aksi halde replay ve testler
        // tekr edilemez.
        var first = DerivedId.From("player", Owner.ToString(), "Ada", "0");
        var second = DerivedId.From("player", Owner.ToString(), "Ada", "0");
        var other = DerivedId.From("player", Owner.ToString(), "Grace", "0");

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.NotEqual(Guid.Empty, first);
    }

    [Fact]
    public async Task ATeamOfTwentyHasAValidFiveManLineup()
    {
        // D111'in olcumu: 20 kisilik kadrodan 5'li lineup kurulur ve motor
        // kabul eder.
        var teams = new InMemoryTeamRepository();
        var players = new InMemoryPlayerRepository();
        var current = new FixedCurrentUser(Owner);
        var teamId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

        var roster = new List<Guid>();

        for (var i = 0; i < 20; i++)
        {
            var id = DerivedId.From("player", Owner.ToString(), $"O{i}", (i % 5).ToString());
            roster.Add(id);
            await players.AddAsync(
                new PlayerRecord(id, Owner, $"O{i}", (Position)(i % 5), Ratings(70 + (i % 12))),
                CancellationToken.None);
        }

        teams.Seed(new TeamRecord(teamId, Owner, "Kuzey", DateTimeOffset.UnixEpoch), [.. roster]);

        var setLineup = new SetLineup(teams, current);
        var result = await setLineup.ExecuteAsync(teamId, [.. roster.Take(5)], CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);

        // Ve motor bu setup'i gercekten kabul ediyor.
        var setup = M7TestData.Mirror(1) with
        {
            Home = new TeamMatchSetup
            {
                Team = new Team
                {
                    Id = teamId,
                    Name = "Kuzey",
                    Roster = [.. roster.Select(id => new Player
                    {
                        Id = id,
                        DisplayName = "X",
                        Position = Position.PG,
                        Ratings = Ratings(),
                    })],
                },
                Lineup = new Lineup { PlayerIds = [.. roster.Take(5)] },
                Offensive = OffensiveTactic.Balanced,
                Defense = DefensiveTactic.ManToMan,
                Pace = Pace.Normal,
            },
        };

        Assert.True(MatchSetupValidator.Validate(setup).IsValid);
    }

    [Fact]
    public async Task ALineupMustHaveExactlyFivePlayers()
    {
        // D31: motor kurali. 4 veya 6 reddedilir.
        var teams = new InMemoryTeamRepository();
        var current = new FixedCurrentUser(Owner);
        var teamId = Guid.Parse("dddddddd-0000-0000-0000-000000000002");

        var roster = Enumerable.Range(0, 10)
            .Select(i => DerivedId.From("player", Owner.ToString(), $"P{i}", "0"))
            .ToList();

        teams.Seed(new TeamRecord(teamId, Owner, "Kuzey", DateTimeOffset.UnixEpoch), [.. roster]);

        var useCase = new SetLineup(teams, current);

        Assert.False((await useCase.ExecuteAsync(teamId, roster.Take(4).ToList(), CancellationToken.None)).Succeeded);
        Assert.False((await useCase.ExecuteAsync(teamId, roster.Take(6).ToList(), CancellationToken.None)).Succeeded);
        Assert.True((await useCase.ExecuteAsync(teamId, roster.Take(5).ToList(), CancellationToken.None)).Succeeded);
    }

    [Fact]
    public async Task ARepeatedPlayerInTheLineupIsRejected()
    {
        var teams = new InMemoryTeamRepository();
        var current = new FixedCurrentUser(Owner);
        var teamId = Guid.Parse("dddddddd-0000-0000-0000-000000000003");

        var roster = Enumerable.Range(0, 10)
            .Select(i => DerivedId.From("player", Owner.ToString(), $"R{i}", "0"))
            .ToList();

        teams.Seed(new TeamRecord(teamId, Owner, "Kuzey", DateTimeOffset.UnixEpoch), [.. roster]);

        var doubled = new List<Guid> { roster[0], roster[0], roster[1], roster[2], roster[3] };
        var result = await new SetLineup(teams, current)
            .ExecuteAsync(teamId, doubled, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("iki kez", result.Error);
    }

    [Fact]
    public async Task APlayerOutsideTheRosterCannotBeInTheLineup()
    {
        // D111: oyuncu kadroda degilse lineup'a giremez. Bu, "kisi basina
        // kopya" kuralinin sonucu: baskasinin oyuncusu benim kadromda olamaz.
        var teams = new InMemoryTeamRepository();
        var current = new FixedCurrentUser(Owner);
        var teamId = Guid.Parse("dddddddd-0000-0000-0000-000000000004");

        var roster = Enumerable.Range(0, 5)
            .Select(i => DerivedId.From("player", Owner.ToString(), $"S{i}", "0"))
            .ToList();

        var foreign = DerivedId.From("player", Stranger.ToString(), "Yabanci", "0");

        teams.Seed(new TeamRecord(teamId, Owner, "Kuzey", DateTimeOffset.UnixEpoch), [.. roster]);

        var lineup = new List<Guid> { roster[0], roster[1], roster[2], roster[3], foreign };
        var result = await new SetLineup(teams, current)
            .ExecuteAsync(teamId, lineup, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("kadroda degil", result.Error);
    }

    [Fact]
    public async Task AnotherUsersTeamCannotBeModified()
    {
        // T19: sahiplik sunucuda cozulur.
        var teams = new InMemoryTeamRepository();
        var current = new FixedCurrentUser(Stranger);
        var teamId = Guid.Parse("dddddddd-0000-0000-0000-000000000005");

        var roster = Enumerable.Range(0, 5)
            .Select(i => DerivedId.From("player", Owner.ToString(), $"T{i}", "0"))
            .ToList();

        teams.Seed(new TeamRecord(teamId, Owner, "Kuzey", DateTimeOffset.UnixEpoch), [.. roster]);

        var result = await new SetLineup(teams, current)
            .ExecuteAsync(teamId, roster, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("size ait degil", result.Error);
    }
}

/// <summary>
/// M7, 04: "Macta kullanilan snapshot, history'nin canli sorgusundan
/// bagimsizdir."
///
/// <para><b>Bu testin olcumu:</b> mac basladiktan sonra kadro degisse bile
/// o macin setup'i DEGISMEMEZ. Aksi halde ayni seed ayni maci vermez ve
/// replay calismaz.</para>
/// </summary>
public class SetupFreezingTests
{
    private static readonly Guid Owner = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public void ARenameDoesNotChangeTheDigest()
    {
        // KASTEN BIR DAVRANIS. Oyuncu adi domain sonucunu ETKILEMEZ, bu
        // yuzden digest'i degistirmemesi dogru. Kozmetik bir duzenleme
        // kaydi gecersizlestirmemeli.
        //
        // Bu, SetupDigest'in "motorun girdisi olan her seyi kapsar" ilkesinin
        // TERSI yonudur: motorun girdisi OLMAYAN sey kapsam disidir.
        var setup = M7TestData.Mirror(1);
        var digest = new SetupDigest();
        var frozen = digest.Of(setup);

        var renamed = setup with
        {
            Home = setup.Home with
            {
                Team = setup.Home.Team with
                {
                    Roster = [.. setup.Home.Team.Roster.Select(p => p with { DisplayName = "Yeni Ad" })],
                },
            },
        };

        Assert.NotEqual(setup.Home.Team.Roster[0].DisplayName, renamed.Home.Team.Roster[0].DisplayName);
        Assert.Equal(frozen, digest.Of(renamed));
    }

    [Fact]
    public void APositionChangeChangesTheDigest()
    {
        // Mevki domain sonucunu etkiler (motorun kullanimi var), bu yuzden
        // digest degisir. Ad degistirmenin TERSI.
        var setup = M7TestData.Mirror(1);
        var digest = new SetupDigest();

        var moved = setup with
        {
            Home = setup.Home with
            {
                Team = setup.Home.Team with
                {
                    Roster = [.. setup.Home.Team.Roster.Select((p, i) =>
                        i == 0 ? p with { Position = (Position)(((int)p.Position + 1) % 5) } : p)],
                },
            },
        };

        Assert.NotEqual(digest.Of(setup), digest.Of(moved));
    }

    [Fact]
    public void ASetupIsUnaffectedByLaterEditsBecauseItIsImmutable()
    {
        // 04: "Snapshot history'nin canli sorgusundan bagimsizdir." Orijinal
        // setup degistirilemez; "sonradan kadro degisti" durumunda elimizdeki
        // eski referans degismemis kalir.
        var setup = M7TestData.Mirror(1);
        var digest = new SetupDigest();
        var frozen = digest.Of(setup);

        var edited = setup with
        {
            Home = setup.Home with
            {
                Team = setup.Home.Team with
                {
                    Roster = [.. setup.Home.Team.Roster.Select((p, i) =>
                        i == 0 ? p with { Ratings = p.Ratings with { Speed = p.Ratings.Speed + 5 } } : p)],
                },
            },
        };

        Assert.Equal(frozen, digest.Of(setup));
        Assert.NotEqual(frozen, digest.Of(edited));
    }
    [Fact]
    public void TheFrozenSetupReproducesTheSameMatch()
    {
        // Ayni dondurulmus setup ile ayni seed -> ayni mac. Bu, 03
        // "Surum ve tekr uretilebilirlik"in motor tarafi.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(4242);

        var first = new MatchSimulation(config).Simulate(setup);
        var second = new MatchSimulation(config).Simulate(setup);

        Assert.Equal(first.HomeScore, second.HomeScore);
        Assert.Equal(first.AwayScore, second.AwayScore);
        Assert.Equal(
            DreamTeam.MatchEngine.Replay.MatchStateFingerprint.OfEvents(first.Events),
            DreamTeam.MatchEngine.Replay.MatchStateFingerprint.OfEvents(second.Events));
    }
}
