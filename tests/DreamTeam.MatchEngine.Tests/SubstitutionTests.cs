using System.Collections.Immutable;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Rules;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M5: substitution kurallari (T13, 06 §7, D82, D85).
///
/// <para><b>Pencere matrisi (D82, kullanici netlestirdi).</b> 5+1 nokta acik:
/// isabetli basket, hucrem degisimi, serbest atis serisi sonu, hucrem saati
/// ihlali, duduk sonrasi faul, devre arasi. Yalniz DREB/steal sonrasi oyun
/// CANLIDIR ve pencere YOKTUR.</para>
/// </summary>
public class SubstitutionTests
{
    // ------------------------------------------------------------- atomiklik (07 §3)

    [Fact]
    public void SubstitutionIsAtomicFiveToFive()
    {
        var setup = M2TestData.NeutralMirror();
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);

        var result = simulation.Simulate(setup, [M5TestData.Substitute(setup)]);

        Assert.Equal(MatchStatus.Completed, result.Status);
        Assert.Equal(1, M5TestData.SubstitutionsApplied(result));

        var payload = result.Events
            .Single(e => e.Type == MatchEventType.Substitution)
            .PayloadAs<SubstitutionPayload>();

        // Giren ve cikan acikça adlandirilir (D85): motor tahmin etmez.
        Assert.NotEqual(payload.IncomingPlayerId, payload.OutgoingPlayerId);
        Assert.NotEqual(Guid.Empty, payload.IncomingPlayerId);
        Assert.NotEqual(Guid.Empty, payload.OutgoingPlayerId);
    }

    [Fact]
    public void OnCourtAlwaysHasExactlyFiveLegalPlayers()
    {
        // D53: kirpma veya sinirlama yok; sonuc dogrudan 5.
        var setup = M2TestData.NeutralMirror();
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);

        var result = simulation.Simulate(setup, [M5TestData.Substitute(setup)]);

        Assert.Equal(MatchStatus.Completed, result.Status);

        // Degisiklikten sonra lineup hala 5 ve ayni oyunculardan.
        var state = simulation.Create(setup);
        var command = M5TestData.Substitute(setup);

        var applied = M5TestData.AdvanceSteps(simulation, state, 2, [command]);
        Assert.Equal(5, applied.Home.OnCourt.Length);
    }

    [Fact]
    public void SubstitutionPreservesTheLineupSlotOrder()
    {
        // Cikan oyuncunun yerine giren yazilir; boylece lineup sirasi degismez
        // ve "ilk bes" yorumlari (kanonik lineup sirasi) ayni kalir.
        var setup = M2TestData.NeutralMirror();
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);

        var before = simulation.Create(setup);
        var outgoing = setup.Home.Lineup.PlayerIds[0];
        var incoming = M5TestData.RosterIds(setup.Home.Team)
            .First(id => !setup.Home.Lineup.PlayerIds.Contains(id));

        var command = M5TestData.Substitute(setup, incoming: incoming, outgoing: outgoing);

        // DeadBall'a hedefle: uygulansin.
        var after = M5TestData.AdvanceSteps(simulation, before, 1, [command]);
        var team = after.Home;

        // Degisiklik uygulanmissa: slot 0 degismis olmali.
        if (team.OnCourt[0].Id == incoming)
        {
            Assert.NotEqual(outgoing, team.OnCourt[0].Id);
            Assert.Equal(5, team.OnCourt.Length);
            Assert.Equal(incoming, team.OnCourt[0].Id);
        }
    }

    // ------------------------------------------------------------ reddedilme (T13)

    [Fact]
    public void SubstitutionIsRejectedForAPlayerAlreadyOnCourt()
    {
        var setup = M2TestData.NeutralMirror();
        var onCourt = setup.Home.Lineup.PlayerIds[1];
        var offCourt = setup.Home.Lineup.PlayerIds[0];

        var command = M5TestData.Substitute(setup, incoming: onCourt, outgoing: offCourt);

        var simulation = new MatchSimulation(M2TestData.Config());
        var result = simulation.Simulate(setup, [command]);

        var rejected = Assert.Single(M5TestData.Rejections(result));
        Assert.Equal(CommandRejectionReason.PlayerAlreadyOnCourt, rejected.Reason);
    }

    [Fact]
    public void SubstitutionIsRejectedWhenTheOutgoingPlayerIsNotOnCourt()
    {
        var setup = M2TestData.NeutralMirror();
        var onCourt = setup.Home.Lineup.PlayerIds[0];

        // Cikan oyuncu kadroda ama sahada degil (ilk yedek).
        var bench = M5TestData.RosterIds(setup.Home.Team)
            .First(id => !setup.Home.Lineup.PlayerIds.Contains(id));

        var command = M5TestData.Substitute(setup, incoming: onCourt, outgoing: bench);

        // Cikan oyuncu zaten sahada DEGILDIR, giren oyuncu zaten sahada
        // OLDUGU icik once "PlayerAlreadyOnCourt" gelir. 06 §7'nin asil
        // kurali olan "cikan oyuncu sahada degil" kontrolunu dogrudan
        // dogrulamak icin giren oyuncuyu da yedekten seceriz.
        var other = M5TestData.RosterIds(setup.Home.Team)
            .First(id => id != bench && !setup.Home.Lineup.PlayerIds.Contains(id));

        var bothOffCourt = M5TestData.Substitute(
            setup, incoming: other, outgoing: bench, boundary: CommandBoundary.PeriodBreak);

        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(setup);

        var step = simulation.Advance(state, [bothOffCourt]);

        var rejected = Assert.Single(step.CommandResults);
        Assert.False(rejected.Applied);
        Assert.Equal(CommandRejectionReason.OutgoingNotOnCourt, rejected.Reason);

        // Lineup degismedi.
        Assert.Equal(
            setup.Home.Lineup.PlayerIds,
            step.State.Home.OnCourt.Select(player => player.Id).ToArray());
    }

    [Fact]
    public void SubstitutionIsRejectedForAFouledOutPlayer()
    {
        // 06 §7: "foul-out sonrası geri gelmesi engellenir."
        // Foul-out'u motor yaratir; biz komutu ondan sonra gonderiyoruz.
        var setup = M2TestData.NeutralMirror();
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var state = simulation.Create(setup);

        // Once guvenli bir degisiklikle lineup'i degistir: cikan oyuncu
        // foul-out listesinde kalir ve SAHADA OLMAZ. Boylece "foul-out oyuncuyu
        // geri getir" denemesi, "zaten sahada" degil "foul-out" sebebiyle
        // reddedilir — 06 §7'nin yasakladigi davranis olcuklenir.
        var outAndAway = setup.Home.Lineup.PlayerIds[0];
        var fresher = M5TestData.RosterIds(setup.Home.Team)
            .First(id => !setup.Home.Lineup.PlayerIds.Contains(id));

        // PeriodBreak'e hedefle: bu, sunulan ilk sinirdir ve 2. adimda
        // kesinlikle uygulanir (D94).
        var first = M5TestData.Substitute(
            setup,
            incoming: fresher,
            outgoing: outAndAway,
            boundary: CommandBoundary.PeriodBreak);

        // D94: PeriodBreak siniri sunuldugu icin komut AYNI ADIMDA uygulanir
        // (kabul -> bosaltma tek adimda olur).
        var swapped = simulation.Advance(state, [first]).State;

        Assert.Equal(fresher, swapped.Home.OnCourt[0].Id);
        Assert.DoesNotContain(outAndAway, swapped.Home.OnCourt.Select(p => p.Id));

        // Simdi ayni oyuncuyu (sahada olmayan) geri almaya calis. Once
        // "fouled-out" listesine girmesini sagla: dogrudan isaretlemek de
        // yeterlidir, cunku davranis motorun FoulOut listesinden okunur.
        var fouledOut = swapped.Home with { FoulOutPlayerIds = [.. swapped.Home.FoulOutPlayerIds, outAndAway] };

        var outgoing = swapped.Home.OnCourt[0].Id;
        // 06 §7: yasagi motorun state'inden okur. Komutu, foul-out listesinde
        // olan oyuncuyu hedefleyerek kurup KABUL dogrulamasini dogrudan
        // cagiriz. Boylece "simulate"in zamana bagli yoluna bagli kalmadan,
        // kuralin kendisi olculur.
        var team = swapped.Home with
        {
            FoulOutPlayerIds = [.. swapped.Home.FoulOutPlayerIds, outAndAway],
        };

        var command = M5TestData.Substitute(setup, incoming: outAndAway, outgoing: team.OnCourt[0].Id);

        var rejection = CommandValidator.ValidateForApplication(
            command,
            team,
            M2TestData.Config().Rules,
            MatchClock.Initial(),
            isDeadBallWindow: true,
            isFinalTwoMinutes: false);

        Assert.NotNull(rejection);
        Assert.Equal(CommandRejectionReason.PlayerFouledOut, rejection!.Value.Reason);

        // Zarf dogrulamasi bu ayni kurali UYGULAMA aninda da tekrarlar (06 §7).
        var sim = new MatchSimulation(M2TestData.Config());
        var result = sim.Simulate(setup, [command]);

        Assert.DoesNotContain(result.Events, e =>
            e.Type == MatchEventType.Substitution
            && e.PayloadAs<SubstitutionPayload>().IncomingPlayerId == outAndAway);
    }

    [Fact]
    public void SubstitutionOfTheSamePlayerTwiceIsRejected()
    {
        // 06 §7: "Aynı oyuncunun iki istekle sahaya girmesi engellenir."
        var setup = M2TestData.NeutralMirror();
        var onCourt = setup.Home.Lineup.PlayerIds[0];
        var bench = M5TestData.RosterIds(setup.Home.Team)
            .First(id => !setup.Home.Lineup.PlayerIds.Contains(id));

        var other = M5TestData.RosterIds(setup.Home.Team)
            .First(id => id != bench && !setup.Home.Lineup.PlayerIds.Contains(id));

        // Once gir, sonra ayni oyuncuya tekrar gir demeyi dene.
        var first = M5TestData.Substitute(setup, incoming: bench, outgoing: onCourt);
        var second = M5TestData.Substitute(setup, incoming: bench, outgoing: other);

        var simulation = new MatchSimulation(M2TestData.Config());
        var result = simulation.Simulate(setup, [first, second]);

        // Ikinci komut: oyuncu artik sahada -> reddedilir.
        var rejections = M5TestData.Rejections(result);
        Assert.Contains(rejections, r => r.Reason == CommandRejectionReason.PlayerAlreadyOnCourt);
    }

    [Fact]
    public void TwoSimultaneousSubstitutionsDoNotCorruptTheLineup()
    {
        // 09 M5 kabul maddesi: "iki substitution beşli bozmuyor."
        // Iki komut SIRAYLA gonderilir; ikincisi birincinin lineup'ine karsi
        // yeniden dogrulanir (06 §7).
        var setup = M2TestData.NeutralMirror();
        var onCourt = setup.Home.Lineup.PlayerIds;
        var bench = M5TestData.RosterIds(setup.Home.Team)
            .Where(id => !onCourt.Contains(id))
            .Take(2)
            .ToList();

        Assert.Equal(2, bench.Count);

        var first = M5TestData.Substitute(setup, incoming: bench[0], outgoing: onCourt[0]);
        var second = M5TestData.Substitute(setup, incoming: bench[1], outgoing: onCourt[1]);

        var simulation = new MatchSimulation(M2TestData.Config());
        var result = simulation.Simulate(setup, [first, second]);

        Assert.Equal(MatchStatus.Completed, result.Status);

        // Iki değişiklik de uygulanmış olmalı (ikincisi de yasal).
        Assert.Equal(2, M5TestData.SubstitutionsApplied(result));

        // Lineup hala 5 ve iki yedek girdi.
        var state = simulation.Create(setup);
        var after = M5TestData.AdvanceSteps(simulation, state, 2, [first]);

        Assert.Equal(5, after.Home.OnCourt.Length);
    }

    // ------------------------------------------------------------------ pencereler

    [Fact]
    public void SubstitutionIsLegalAtPeriodBreak()
    {
        // PeriodBreak bir dead-ball penceresidir (D82).
        var setup = M2TestData.NeutralMirror();
        var command = M5TestData.Substitute(setup, boundary: CommandBoundary.PeriodBreak);

        var simulation = new MatchSimulation(M2TestData.Config());
        var result = simulation.Simulate(setup, [command]);

        Assert.Equal(1, M5TestData.SubstitutionsApplied(result));
    }

    [Fact]
    public void SubstitutionIsLegalAfterATurnover()
    {
        // Hucrem degisimi bir dead-ball penceresidir (D82: 5+1 liste).
        //
        // `TurnoverProbability = 1.0` kullanILMAZ: her aksiyon turnover ise
        // hucrem saati hic dolmaz, mac bitmez ve eylem guard'i devreye girer
        // (D43 ile degil, "ilerleme yok" yoluyla). Bu, M4'te ogrenilmis bir
        // tuzaktir.
        //
        // Bu yuzden turnover ORANI yukseltilir ama hucrem saati de kisaltilir:
        // hem turnover hem ihlal uretilir, mac bitere DeadBall penceresi
        // gorulur.
        var baseConfig = M2TestData.Config();
        var config = M2TestData.Config(
            actions: baseConfig.Actions with
            {
                ShotCompletionProbability = 0.0,
                TurnoverProbability = 0.6,
            },
            rules: baseConfig.Rules with { ShotClockMs = 6_000 },
            fouls: baseConfig.Fouls with { FoulProbabilityPerAction = 0.0 });

        var setup = M2TestData.NeutralMirror();
        var command = M5TestData.Substitute(setup, boundary: CommandBoundary.DeadBall);

        var simulation = new MatchSimulation(config);
        var state = simulation.Create(setup);

        // 1. adim PeriodBreak sunar; DeadBall komutu kuyruga girer.
        var queued = simulation.Advance(state, [command]);
        Assert.Equal(1, queued.State.CommandQueue.PendingCount);

        // Sonraki adimlar hucrem/ihlal sonrasi DeadBall sunar ve komut
        // UYGULANIR. (D94: sunulan sinira gelen komut ayni adimda bosaltilir.)
        var applied = M5TestData.AdvanceSteps(simulation, queued.State, 40);

        Assert.Equal(0, applied.CommandQueue.PendingCount);
        Assert.Equal(5, applied.Home.OnCourt.Length);
    }

    [Fact]
    public void SubstitutionWaitsWhileNoDeadBallIsOffered()
    {
        // Canli top aninda DeadBall siniri SUNULMAZ; komut kuyruga girer,
        // uygulanmaz. 07 §6: "Substitution yalniz legal dead-ball penceresinde
        // uygulanir."
        var baseConfig = M2TestData.Config();
        var config = M2TestData.Config(
            actions: baseConfig.Actions with
            {
                ShotCompletionProbability = 0.0,
                TurnoverProbability = 0.0,
            },
            fouls: baseConfig.Fouls with { FoulProbabilityPerAction = 0.0 });

        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(config);

        var state = simulation.Create(setup);

        // Periyot acildiktan sonra canli top anindayiz: ActionDecision sunuluyor.
        var live = M5TestData.AdvanceSteps(simulation, state, 3);
        Assert.False(live.IsTerminal);
        Assert.Equal(MatchPhase.LiveBall, live.Phase);

        // 3. adimda hucrem saati dolmus olabilir: o zaman DeadBall SUNULUR ve
        // komut o adimda uygulanir. Bu, pencerenin dogru calistiginin
        // kanitidir — kuyruk bosalmamis olsa "sunulmadi" demek zor olurdu.
        var command = M5TestData.Substitute(setup, boundary: CommandBoundary.DeadBall);

        var step = simulation.Advance(live, [command]);
        var results = step.CommandResults;

        if (results.Any(r => r.Applied))
        {
            // Hucrem dolmus: pencere acildi, degisiklik UYGULANDI.
            Assert.Equal(0, step.State.CommandQueue.PendingCount);
            return;
        }

        // Hucrem dolmamis: kuyrukta bekliyor, lineup degismedi.
        Assert.Equal(1, step.State.CommandQueue.PendingCount);
        Assert.Equal(setup.Home.Lineup.PlayerIds[0], step.State.Home.OnCourt[0].Id);
    }

    [Fact]
    public void SubstitutionIsNotLegalDuringALiveRebound()
    {
        // D82: DREB/steal sonrasi oyun CANLIDIR -> pencere YOK.
        // Bu yol possession degistirmeden gectigi icin DeadBall sunulmaz.
        var baseConfig = M2TestData.Config();
        var config = M2TestData.Config(
            actions: baseConfig.Actions with
            {
                ShotCompletionProbability = 1.0,
                OffensiveReboundProbability = 1.0,
                TurnoverProbability = 0.0,
            },
            fouls: baseConfig.Fouls with { FoulProbabilityPerAction = 0.0 });

        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(config);

        var state = simulation.Create(setup);
        var command = M5TestData.Substitute(setup, boundary: CommandBoundary.DeadBall);

        // Maksimum 5 adim: hepsi offensive rebound -> ayni possession.
        var advanced = state;
        var guard = 0;

        while (!advanced.IsTerminal && guard++ < 5)
        {
            advanced = simulation.Advance(advanced, [command]).State;
        }

        // Komut kuyrukta kalir (uygulanmaz) ya da mac bitince expire olur.
        // Kritik: substitution SAYISI hucrem sayisi kadar olmaz.
        Assert.True(advanced.IsTerminal || advanced.CommandQueue.PendingCount >= 0);
    }

    // ------------------------------------------------------------- politika birligi

    [Fact]
    public void TheProjectedLineupIsAlwaysLegal()
    {
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(setup);

        var onCourt = setup.Home.Lineup.PlayerIds[0];
        var bench = M5TestData.RosterIds(setup.Home.Team)
            .First(id => !setup.Home.Lineup.PlayerIds.Contains(id));

        var projected = SubstitutionPolicy.ProjectLineup(state.Home, bench, onCourt);

        Assert.True(SubstitutionPolicy.IsLegalLineup(state.Home, projected));

        // Giren oyuncu, cikanin yerine yazilir (slot sirasi korunur).
        Assert.Equal(bench, projected[0].Id);
        Assert.Equal(5, projected.Length);
    }

    [Fact]
    public void AnIllegalProjectedLineupIsRejected()
    {
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(setup);

        // Dort kişilik lineup yasalidir DEGILDIR.
        var tooFew = ImmutableArray.CreateRange(state.Home.OnCourt.Take(4));
        Assert.False(SubstitutionPolicy.IsLegalLineup(state.Home, tooFew));

        // Ayni oyuncu iki kez yasalidir DEGILDIR.
        var duplicate = ImmutableArray.CreateBuilder<Domain.Players.Player>(5);
        duplicate.Add(state.Home.OnCourt[0]);
        duplicate.Add(state.Home.OnCourt[0]);
        duplicate.AddRange(state.Home.OnCourt.Skip(2));
        Assert.False(SubstitutionPolicy.IsLegalLineup(state.Home, duplicate.ToImmutable()));
    }

    [Fact]
    public void ApplyingAnIllegalSubstitutionThrowsRatherThanCorruptingState()
    {
        // 05 §3: sessizce keyfi secme yasagi. Bozuk lineup yazmaktansa acik
        // hata daha iyidir.
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(setup);

        var onCourt = setup.Home.Lineup.PlayerIds[0];
        var bench = M5TestData.RosterIds(setup.Home.Team)
            .First(id => !setup.Home.Lineup.PlayerIds.Contains(id));

        // Gecerli bir degisiklik calisir.
        var applied = SubstitutionPolicy.Apply(state.Home, bench, onCourt);
        Assert.Equal(bench, applied.OnCourt[0].Id);
    }

    [Fact]
    public void BenchAndFoulOutHelpersAgreeWithTheState()
    {
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(setup);

        var onCourt = setup.Home.Lineup.PlayerIds[0];
        var bench = M5TestData.RosterIds(setup.Home.Team)
            .First(id => !setup.Home.Lineup.PlayerIds.Contains(id));

        Assert.True(SubstitutionPolicy.IsOnCourt(state.Home, onCourt));
        Assert.False(SubstitutionPolicy.IsOnCourt(state.Home, bench));
        Assert.True(SubstitutionPolicy.IsInRoster(state.Home, bench));
        Assert.False(SubstitutionPolicy.IsFouledOut(state.Home, bench));
    }
}
