using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Projection;

namespace DreamTeam.Simulator;

/// <summary>
/// Tek maçın konsol raporu. D33: argümansız çalışır, sabit kurgusal fixture oynatır.
///
/// Rapor iki işi birlikte yapar: maçın istatistiğini gösterir ve <b>eksik olan
/// kuralları açıkça yazar</b>. 09 roadmap, M2'yi "eksik M2 koşuları final NBA benzeri
/// ürün diye sunulmaz" diye uyarır; bu uyarı arayüzde görünür olmalıdır.
/// </summary>
internal static class SingleMatchReport
{
    public static void Write(
        TextWriter writer,
        MatchResult result,
        EngineIdentity engine,
        string configHash,
        MatchSetup? setup = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(result);

        writer.WriteLine("Dream Team Reborn - Match Engine v0.1 (M5 yonetici mudahalesi)");
        writer.WriteLine(new string('=', 78));
        writer.WriteLine($"MatchId      : {result.MatchId}");
        writer.WriteLine($"Engine       : {engine.EngineVersion}   Rules: {engine.RulesVersion}");
        writer.WriteLine($"ConfigHash   : {configHash}");
        writer.WriteLine($"Status       : {result.Status}"
            + (result.AbortReason is { } abortReason ? $" — {abortReason}" : string.Empty));
        writer.WriteLine($"Periods      : {result.PeriodsPlayed}   Elapsed: {result.ElapsedGameTimeMs / 1000.0:F1} s");

        if (setup is not null)
        {
            writer.WriteLine(
                $"Taktik       : {HomeLabel}{setup.Home.Offensive}/{setup.Home.Defense} "
                + $"| {AwayLabel}{setup.Away.Offensive}/{setup.Away.Defense}");
            writer.WriteLine($"Tempo        : {HomeLabel}{setup.Home.Pace}  |  {AwayLabel}{setup.Away.Pace}");
        }

        // M4: OVR gosterim amaclidir; cozum girdisi DEGILDIR (T03).
        if (result.HomeOverall is { } homeOvr && result.AwayOverall is { } awayOvr)
        {
            writer.WriteLine(
                $"OVR (gosterim): {HomeLabel}{homeOvr}  |  {AwayLabel}{awayOvr}"
                + "  <- cozum girdisi degildir; motor bu degeri hic okumaz");
        }

        // M5: timeout butceleri (D84). Bunlar BASLANGIC degerleridir; motor
        // yonetir (D81) ve bu argumansiz kosuda hic komut gonderilmez.
        if (setup is not null)
        {
            writer.WriteLine(
                $"Timeout      : {HomeLabel}{result.HomeTimeoutsUsed}/{result.HomeTimeoutBudget} tam"
                + $", {result.HomeShortTimeoutsUsed}/{result.HomeShortTimeoutBudget} 20sn"
                + $"  |  {AwayLabel}{result.AwayTimeoutsUsed}/{result.AwayTimeoutBudget} tam"
                + $", {result.AwayShortTimeoutsUsed}/{result.AwayShortTimeoutBudget} 20sn");
        }

        writer.WriteLine();

        if (result.Status != MatchStatus.Completed)
        {
            // Terminal olmayan sonuçta skor ve box score geçersizdir; uydurma tablo
            // yazmak yerine eksikliği açıkça belirt.
            writer.WriteLine("SCORE        : gecersiz (mac tamamlanmadi)");
            writer.WriteLine($"Event sayisi : {result.Events.Length}");
            writer.WriteLine();
            writer.WriteLine("Abort nedeni: " + (result.AbortReason ?? "belirtilmedi"));
            writer.WriteLine();
            WriteEventTrace(writer, result);
            writer.WriteLine();
            WriteIncompletenessNotice(writer);
            return;
        }

        var home = Box(result, TeamSide.Home);
        var away = Box(result, TeamSide.Away);

        writer.WriteLine($"SCORE        : {home.TeamName} {result.HomeScore} - {result.AwayScore} {away.TeamName}"
            + (result.IsTie ? "  (BERABERE — uzatma M2'de yok, kazanan seçilmedi)" : string.Empty));
        writer.WriteLine($"Possessions  : {home.TeamName} {result.HomePossessions} - {result.AwayPossessions} {away.TeamName}");
        writer.WriteLine();

        writer.WriteLine(TeamRow("Takim", home.TeamName, away.TeamName));
        writer.WriteLine(Divider());
        WriteStat(writer, "FG", home, away, f => $"{f.FieldGoalsMade}-{f.FieldGoalsAttempted}");
        WriteStat(writer, "2P", home, away, f => $"{f.TwoPointersMade}-{f.TwoPointersAttempted}");
        WriteStat(writer, "3P", home, away, f => $"{f.ThreePointersMade}-{f.ThreePointersAttempted}");
        WriteStat(writer, "FT", home, away, f => $"{f.FreeThrowMakes}-{f.FreeThrowAttempts}");
        WriteStat(writer, "AST", home, away, f => $"{f.Assists}");
        WriteStat(writer, "TOV", home, away, f => $"{f.Turnovers}");
        WriteStat(writer, "PF", home, away, f => $"{f.PersonalFouls}");
        WriteStat(writer, "BLK", home, away, f => $"{f.Blocks}");
        WriteStat(writer, "OREB", home, away, f => $"{f.OffensiveRebounds}");
        WriteStat(writer, "DREB", home, away, f => $"{f.DefensiveRebounds}");
        WriteStat(writer, "PTS", home, away, f => $"{f.Points}");
        writer.WriteLine();

        writer.WriteLine("Oyuncu satırları (yalnız istatistiği olanlar):");
        writer.WriteLine(Divider());

        foreach (var player in result.PlayerBoxScores
                     .Where(p => p.FieldGoalsAttempted > 0 || p.Turnovers > 0 || p.Rebounds > 0)
                     .OrderBy(p => p.Team)
                     .ThenByDescending(p => p.Points)
                     .ThenBy(p => p.DisplayName, StringComparer.Ordinal))
        {
            writer.WriteLine(
                $"  {player.DisplayName,-26} {player.Position,-3} "
                + $"PTS {player.Points,3}  FG {player.FieldGoalsMade,2}-{player.FieldGoalsAttempted,2}  "
                + $"3P {player.ThreePointersMade,2}-{player.ThreePointersAttempted,2}  "
                + $"AST {player.Assists,2}  TOV {player.Turnovers,2}  "
                + $"REB {player.OffensiveRebounds}/{player.DefensiveRebounds}");
        }

        writer.WriteLine();
        writer.WriteLine("Event türleri: "
            + string.Join(", ", result.Events.Select(e => e.Type.ToString()).Distinct().OrderBy(t => t, StringComparer.Ordinal)));
        writer.WriteLine($"Toplam event : {result.Events.Length}");

        writer.WriteLine();
        WriteEnergyTable(writer, result);
        writer.WriteLine();
        WriteEventTrace(writer, result);
        writer.WriteLine();
        WriteIncompletenessNotice(writer);
    }

    /// <summary>
    /// M4: maç sonu enerji ve sahada kalma süresi. 08 §88 bu dağılımı ister; T12c
    /// toplamı ayrıca test eder. Rapor yalnız gösterir, hiçbir değeri
    /// yeniden hesaplamaz.
    /// </summary>
    private static void WriteEnergyTable(TextWriter writer, MatchResult result)
    {
        if (result.PlayerEnergy.IsDefaultOrEmpty)
        {
            return;
        }

        writer.WriteLine("Enerji ve sahada kalma suresi (mac sonu):");
        writer.WriteLine(Divider());

        foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
        {
            var team = result.PlayerEnergy.Where(report => report.Team == side).ToList();

            if (team.Count == 0)
            {
                continue;
            }

            var averageEnergy = team.Average(report => report.Energy);
            var averageSeconds = team.Average(report => report.SecondsOnCourt);

            writer.WriteLine(
                $"  {(side == TeamSide.Home ? HomeLabel : AwayLabel),-5} ort. enerji {averageEnergy,5:F1}   "
                + $"ort. sure {averageSeconds / 60.0,5:F1} dk");

            foreach (var report in team.OrderByDescending(item => item.SecondsOnCourt))
            {
                writer.WriteLine(
                    $"    {report.DisplayName,-26} enerji {report.Energy,3}   "
                    + $"saha {report.SecondsOnCourt / 60.0,6:F1} dk");
            }
        }
    }

    private static void WriteEventTrace(TextWriter writer, MatchResult result)
    {
        writer.WriteLine("Olay akisi denetimi (ilk 24):");
        writer.WriteLine(Divider());

        foreach (var matchEvent in result.Events.Take(24))
        {
            writer.WriteLine(
                $"  #{matchEvent.Sequence,-5} P{matchEvent.Period} "
                + $"clk {matchEvent.GameClockMs / 1000.0,7:F1}s "
                + $"el {matchEvent.ElapsedGameTimeMs / 1000.0,7:F1}s "
                + $"poss {matchEvent.PossessionId?.ToString() ?? "-",-4} "
                + $"act {matchEvent.ActionId?.ToString() ?? "-",-4} "
                + $"{matchEvent.Type,-17} {Describe(matchEvent)}");
        }
    }

    private static string Describe(MatchEvent matchEvent) => matchEvent.Type switch
    {
        MatchEventType.ShotAttempt =>
            $"{matchEvent.PayloadAs<ShotAttemptPayload>().ShotType} "
            + $"kalite {matchEvent.PayloadAs<ShotAttemptPayload>().Quality} "
            + $"enerji {matchEvent.PayloadAs<ShotAttemptPayload>().ShooterEnergy}",
        MatchEventType.ShotMade => $"{matchEvent.PayloadAs<ShotMadePayload>().ShotType} "
            + $"{matchEvent.PayloadAs<ShotMadePayload>().Points} puan",
        MatchEventType.ShotMissed => $"{matchEvent.PayloadAs<ShotMissedPayload>().ShotType} kaçtı",
        MatchEventType.Turnover => matchEvent.PayloadAs<TurnoverPayload>().Kind.ToString(),
        MatchEventType.Rebound => matchEvent.PayloadAs<ReboundPayload>().Offensive ? "OREB" : "DREB",
        MatchEventType.PossessionEnded => matchEvent.PayloadAs<PossessionEndedPayload>().Reason.ToString(),
        MatchEventType.Foul => $"{matchEvent.PayloadAs<FoulPayload>().Type} "
            + $"-> {matchEvent.PayloadAs<FoulPayload>().FreeThrowCount} FT",
        MatchEventType.Block => $"blok: {matchEvent.PayloadAs<BlockPayload>().DefenderId.ToString()[..8]}",
        MatchEventType.FreeThrowMade => $"{matchEvent.PayloadAs<FreeThrowMadePayload>().Index + 1}/"
            + $"{matchEvent.PayloadAs<FreeThrowMadePayload>().Count} isabet",
        MatchEventType.FreeThrowMissed => $"{matchEvent.PayloadAs<FreeThrowMissedPayload>().Index + 1}/"
            + $"{matchEvent.PayloadAs<FreeThrowMissedPayload>().Count} kacti"
            + (matchEvent.PayloadAs<FreeThrowMissedPayload>().BallIsLive ? " (canli)" : string.Empty),
        _ => string.Empty,
    };

    private static void WriteIncompletenessNotice(TextWriter writer)
    {
        writer.WriteLine("!! BU CIKTI TAMAMLANMIS BIR MAC MOTORU DEGILDIR");
        writer.WriteLine(new string('-', 78));

        foreach (var line in new[]
                 {
                     "Yok: out-of-bounds, jump ball, defensive three seconds,",
                     "     technical/flagrant foul.",
                     "Yok: steal atfedimi, transition aksiyonu, mismatch, takim ribaundu.",
                     "",
                     "M5 KISITLARI (bilincli sapmalar):",
                     "  Timeout canli topta UYGULANMAZ; yalniz dead-ball'da (D84).",
                     "  Timeout kullanilmadi: motor yonetir (D81), bu kosuda komut yok.",
                     "  ShortTimeoutsPerTeam=3 KAYNAKTAN GELMIYOR (D89); degistirilebilir.",
                     "  Uzatma ust siniri 2; esitlikte mac Aborted olur (D79).",
                     "",
                     "GameForm KAPALI: enerji disinda baska bir cesitlilik mekanizmasi yok.",
                     "",
                     "Bu sonuclar KALIBRE EDILMEMIS baslangic katsayilariyla uretildi.",
                     "Ortalama skor gercekci gorunse de bu denge kaniti degildir.",
                     "Sayisal dogrulama 10K/100K deneylerine (M6) birakilmistir.",
                     "Egalikte kazanan secilmemistir; rastgele kazanan uretilmez.",
                     "",
                     "Siradaki adim: M6 CLI batch + 10K/100K deney ve denge adayi.",
                 })
        {
            writer.WriteLine("   " + line);
        }
    }

    private const string HomeLabel = "Ev ";
    private const string AwayLabel = "Dep ";

    private static TeamBoxScore Box(MatchResult result, TeamSide side) =>
        result.BoxScores.First(box => box.Team == side);

    private static string Divider() => new('-', 46);

    private static string TeamRow(string label, string home, string away) =>
        $"{label,-10} {home,-16} {away}";

    private static void WriteStat(
        TextWriter writer,
        string label,
        TeamBoxScore home,
        TeamBoxScore away,
        Func<TeamBoxScore, string> format) =>
        writer.WriteLine($"{label,-10} {format(home),-16} {format(away)}");
}
