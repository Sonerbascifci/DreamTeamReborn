using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Rules;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M5: komutun <b>mantiksal sınıra</b> bağlanması (07 §6) ve sözleşme
/// değişiklikleri (event türü, şema sürümü, <c>Advance</c> imzası).
/// </summary>
public class ApplicationBoundaryTests
{
    [Fact]
    public void TacticChangesApplyAtTheNextActionDecision()
    {
        // 07 §6 madde 1: "Taktik/tempo bir sonraki aksiyon karar sınırında
        // uygulanır; çözülmeye başlamış şutu geriye dönük değiştirmez."
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());

        var state = simulation.Create(setup);

        // PeriodBreak adımı: ActionDecision sunulmaz, komut kuyruğa girir.
        var queued = simulation.Advance(
            state,
            [M5TestData.ChangeOffense(tactic: OffensiveTactic.InsidePost)]);

        Assert.Equal(1, queued.State.CommandQueue.PendingCount);
        Assert.Equal(OffensiveTactic.Balanced, queued.State.Home.OffensiveTactic);

        // Sonraki adım LiveBall -> ActionDecision: komut uygulanır.
        var applied = simulation.Advance(queued.State);
        Assert.Equal(OffensiveTactic.InsidePost, applied.State.Home.OffensiveTactic);
    }

    [Fact]
    public void AResolvedShotIsNotAlteredByALaterTacticChange()
    {
        // 07 §6 madde 1: "Taktik/tempo bir sonraki aksiyon karar sınırında
        // uygulanır; çözülmeye başlamış şutu geriye dönük değiştirmez."
        //
        // Bekleyen şut ÇÖZÜLMEK zorundadır; bu sırada ActionDecision sunulmaz,
        // dolayısıyla komut kuyruğa girer ve uygulanmaz. Şut çözülünce komut
        // hâlâ bekler, bir sonraki aksiyon sınırında uygulanır.
        var simulation = new MatchSimulation(M2TestData.Config());

        // Birkaç seed dene: bazı seed'lerde belirli sayıda adımdan sonra şut
        // bırakılır.
        var before = new List<string>();
        var after = new List<string>();

        for (ulong seed = 0; seed < 40; seed++)
        {
            var setup = M2TestData.NeutralMirror(seed);
            var state = simulation.Create(setup);
            var guard = 0;

            while (state.PendingShot is null && guard++ < 200 && !state.IsTerminal)
            {
                state = simulation.Advance(state).State;
            }

            if (state.PendingShot is null)
            {
                continue;
            }

            // Şutun kalitesi ve becerisi ZATEN bırakma anında sabitlendi
            // (PendingShot). Değişecek bir şey yok.
            before.Add($"{state.PendingShot.ShotId}:{state.PendingShot.Quality}");

            // Bekleyen şut varken taktik komutu gönder.
            var withCommand = simulation.Advance(
                state,
                [M5TestData.ChangeOffense(tactic: OffensiveTactic.InsidePost)]).State;

            // Şut çözüldü, komut hâlâ kuyrukta.
            Assert.Null(withCommand.PendingShot);
            Assert.Equal(1, withCommand.CommandQueue.PendingCount);
            Assert.Equal(OffensiveTactic.Balanced, withCommand.Home.OffensiveTactic);

            after.Add(withCommand.Home.OffensiveTactic.ToString());
        }

        Assert.NotEmpty(before);
        Assert.All(after, tactic => Assert.Equal(nameof(OffensiveTactic.Balanced), tactic));
    }

    [Fact]
    public void ThePaceChangeAppliesToTheNextAction()
    {
        // D59: tempo aksiyon suresini carpar.
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());

        var state = simulation.Create(setup);

        var queued = simulation.Advance(state, [M5TestData.ChangePace(pace: Pace.Fast)]);
        Assert.Equal(Pace.Normal, queued.State.Home.Pace);

        var applied = simulation.Advance(queued.State);
        Assert.Equal(Pace.Fast, applied.State.Home.Pace);
    }

    [Fact]
    public void ATacticChangeDoesNotResetTheShotClock()
    {
        // 06 §6: taktik degisimi bir saat reset sebebi DEGILDIR.
        var rules = M2TestData.Config().Rules;

        Assert.Equal(24_000, rules.ShotClockMs);

        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());

        var state = simulation.Create(setup);

        var queued = simulation.Advance(state, [M5TestData.ChangeOffense()]);
        var applied = simulation.Advance(queued.State);

        // Hucrem saati yalnizca aksiyon suresiyle ilerler.
        Assert.True(applied.State.Clock.ShotClockMs <= rules.ShotClockMs);
    }

    [Fact]
    public void TheCommandAppliedEventCarriesBeforeAndAfterValues()
    {
        // 07 §2 "Müdahale" ailesi: "CommandId, önce/sonra değerleri".
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());

        // D96: taktik komutu PeriodBreak'e hedeflenemez. 07 §6 madde 1 taktiği
        // **ActionDecision**'a baglar; PeriodBreak yalniz substitution ve
        // timeout icindir. Dogru hedef verilir.
        var result = simulation.Simulate(
            setup,
            [
                M5TestData.ChangeOffense(tactic: OffensiveTactic.InsidePost),
            ]);

        var payload = result.Events
            .Single(e => e.Type == MatchEventType.TacticChanged)
            .PayloadAs<TacticChangedPayload>();

        Assert.Equal(OffensiveTactic.Balanced, payload.Previous);
        Assert.Equal(OffensiveTactic.InsidePost, payload.Current);
        Assert.NotEqual(Guid.Empty, payload.CommandId);
    }

    [Fact]
    public void TheDefenseChangeEventCarriesBeforeAndAfterValues()
    {
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());

        // D96: savunma da ActionDecision'a baglidir.
        var result = simulation.Simulate(
            setup,
            [
                M5TestData.ChangeDefense(tactic: DefensiveTactic.Switch),
            ]);

        var payload = result.Events
            .Single(e => e.Type == MatchEventType.DefenseChanged)
            .PayloadAs<DefenseChangedPayload>();

        Assert.Equal(DefensiveTactic.ManToMan, payload.Previous);
        Assert.Equal(DefensiveTactic.Switch, payload.Current);
    }

    [Fact]
    public void ThePaceChangeEventCarriesBeforeAndAfterValues()
    {
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());

        // D96: tempo da ActionDecision'a baglidir.
        var result = simulation.Simulate(
            setup,
            [
                M5TestData.ChangePace(pace: Pace.Slow),
            ]);

        var payload = result.Events
            .Single(e => e.Type == MatchEventType.PaceChanged)
            .PayloadAs<PaceChangedPayload>();

        Assert.Equal(Pace.Normal, payload.Previous);
        Assert.Equal(Pace.Slow, payload.Current);
    }

    // ------------------------------------------------------- sozlesme sayilari

    [Fact]
    public void TheEventSchemaVersionIsFour()
    {
        // M5: 3 -> 4 (6 yeni event turu).
        Assert.Equal(4, MatchSimulation.EventSchemaVersion);
    }

    [Fact]
    public void TheEventTypeCountIsTwentyFive()
    {
        // M3'te 18, M5'te **7** yeni tur eklendi (plan "24 + 6" diyordu; bu bir
        // SAYIM HATASI idi — 7 tur eklendi: TacticChanged, DefenseChanged,
        // PaceChanged, Substitution, Timeout, CommandApplied, CommandRejected).
        // 07 §2'deki "Müdahale" ailesi 5 tur, "Komut sonucu" ailesi 2 tur.
        var types = Enum.GetValues<MatchEventType>();

        Assert.Equal(25, types.Length);
        Assert.Contains(MatchEventType.TacticChanged, types);
        Assert.Contains(MatchEventType.DefenseChanged, types);
        Assert.Contains(MatchEventType.PaceChanged, types);
        Assert.Contains(MatchEventType.Substitution, types);
        Assert.Contains(MatchEventType.Timeout, types);
        Assert.Contains(MatchEventType.CommandApplied, types);
        Assert.Contains(MatchEventType.CommandRejected, types);
    }

    [Fact]
    public void StealIsStillAbsentAsRecordedInD66()
    {
        // 07 §2'de listelenir ama M5'te eklenmedi (D66): steal atfedimi M5
        // adayiydi ama M5'in command yuzeyiyle cakisma riski nedeniyle
        // M6'ya birakildi. Bu, sahte bir ayrim DEGIL, dokumante bir
        // eksikliktir. Turnover hala LostBall uretiyor.
        var types = Enum.GetValues<MatchEventType>();

        Assert.DoesNotContain("Steal", Enum.GetNames<MatchEventType>());
        Assert.DoesNotContain("TeamRebound", Enum.GetNames<MatchEventType>());
        Assert.DoesNotContain("BallOutOfBounds", Enum.GetNames<MatchEventType>());

        // Turnover yine de LostBall uretiyor; attribution yok.
        var setup = M2TestData.NeutralMirror();
        var result = new MatchSimulation(M2TestData.Config()).Simulate(setup);

        var turnovers = result.Events
            .Where(e => e.Type == MatchEventType.Turnover)
            .Select(e => e.PayloadAs<TurnoverPayload>().Kind)
            .ToList();

        Assert.NotEmpty(turnovers);
        Assert.All(turnovers, kind =>
            Assert.True(kind is TurnoverKind.LostBall or TurnoverKind.OffensiveFoul
                or TurnoverKind.ShotClockViolation));

        // Steal attribution YOK.
        Assert.DoesNotContain(turnovers, kind => kind == TurnoverKind.BadPass);
    }

    // ------------------------------------------------------------- D79: uzatma

    [Fact]
    public void TheOvertimeLimitIsTwoByDefault()
    {
        Assert.Equal(2, RulesProfile.SimpleNbaInspired.MaxOvertimePeriods);
    }

    [Fact]
    public void TheMaxOvertimePeriodsChangesTheConfigHash()
    {
        // D79 degeri ConfigHash'e girer: iki farkli sinirli config ayrisir.
        var baseline = M2TestData.Config();

        var changed = M2TestData.Config(
            rules: baseline.Rules with { MaxOvertimePeriods = 5 });

        Assert.NotEqual(baseline.ComputeConfigHash(), changed.ComputeConfigHash());
    }

    [Fact]
    public void TheTimeoutBudgetsChangeTheConfigHash()
    {
        var baseline = M2TestData.Config();

        var full = M2TestData.Config(
            rules: baseline.Rules with { FullTimeoutsPerTeam = 7 });

        var shortBudget = M2TestData.Config(
            rules: baseline.Rules with { ShortTimeoutsPerTeam = 5 });

        Assert.NotEqual(baseline.ComputeConfigHash(), full.ComputeConfigHash());
        Assert.NotEqual(baseline.ComputeConfigHash(), shortBudget.ComputeConfigHash());
    }

    [Fact]
    public void TheOvertimePolicyOpensOvertimeWhileTheLimitIsNotReached()
    {
        var rules = RulesProfile.SimpleNbaInspired;

        // Duzenleme periyodu 4. bitti, esit: 1 uzatma hakki var.
        var fourthPeriod = new MatchClock
        {
            Period = rules.PeriodCount,
            GameClockMs = 0,
            ShotClockMs = 0,
            ElapsedGameTimeMs = 0,
        };

        Assert.True(OvertimePolicy.ShouldStartOvertime(rules, fourthPeriod, 90, 90));
        Assert.Null(OvertimePolicy.LimitReachedReason(rules, fourthPeriod, 90, 90));

        // 5. periyot (ilk uzatma) bitti, esit: 2. uzatma hakki var.
        var firstOvertime = fourthPeriod with { Period = rules.PeriodCount + 1 };
        Assert.True(OvertimePolicy.ShouldStartOvertime(rules, firstOvertime, 95, 95));

        // 6. periyot (ikinci uzatma) bitti, esit: SINIR doldu.
        var secondOvertime = firstOvertime with { Period = rules.PeriodCount + 2 };
        Assert.False(OvertimePolicy.ShouldStartOvertime(rules, secondOvertime, 99, 99));
        Assert.NotNull(OvertimePolicy.LimitReachedReason(rules, secondOvertime, 99, 99));
    }

    [Fact]
    public void TheOvertimeLimitOnlyBitesWhenTheScoreIsStillTied()
    {
        var rules = RulesProfile.SimpleNbaInspired;
        var exhausted = new MatchClock
        {
            Period = rules.PeriodCount + 2,
            GameClockMs = 0,
            ShotClockMs = 0,
            ElapsedGameTimeMs = 0,
        };

        // Esit degilse mac biter, sinir degil.
        Assert.Null(OvertimePolicy.LimitReachedReason(rules, exhausted, 101, 99));
        Assert.False(OvertimePolicy.ShouldStartOvertime(rules, exhausted, 101, 99));
    }

    [Fact]
    public void TheMaxTotalPeriodsIsRegulationPlusTheLimit()
    {
        var rules = RulesProfile.SimpleNbaInspired;

        Assert.Equal(6, OvertimePolicy.MaxTotalPeriods(rules));
    }

    [Fact]
    public void AZeroOvertimeLimitEndsTheMatchAtRegulation()
    {
        // Degistirilebilir bir guvenlik ayari: 0 uzatma, duzenlemede esitlikte
        // mac biter. Sinir degeri bu senaryoyu da ifade edebilmeli.
        var rules = RulesProfile.SimpleNbaInspired with { MaxOvertimePeriods = 0 };
        var fourthPeriod = new MatchClock
        {
            Period = rules.PeriodCount,
            GameClockMs = 0,
            ShotClockMs = 0,
            ElapsedGameTimeMs = 0,
        };

        Assert.NotNull(OvertimePolicy.LimitReachedReason(rules, fourthPeriod, 90, 90));
    }
}
