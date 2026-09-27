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
    public static void Write(TextWriter writer, MatchResult result, EngineIdentity engine, string configHash)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(result);

        writer.WriteLine("Dream Team Reborn — Match Engine v0.1 (M2 vertical slice)");
        writer.WriteLine(new string('=', 78));
        writer.WriteLine($"MatchId      : {result.MatchId}");
        writer.WriteLine($"Engine       : {engine.EngineVersion}   Rules: {engine.RulesVersion}");
        writer.WriteLine($"ConfigHash   : {configHash}");
        writer.WriteLine($"Status       : {result.Status}"
            + (result.AbortReason is { } abortReason ? $" — {abortReason}" : string.Empty));
        writer.WriteLine($"Periods      : {result.PeriodsPlayed}   Elapsed: {result.ElapsedGameTimeMs / 1000.0:F1} s");
        writer.WriteLine();

        if (result.Status != MatchStatus.Completed)
        {
            // Terminal olmayan sonuçta skor ve box score geçersizdir; uydurma tablo
            // yazmak yerine eksikliği açıkça belirt.
            writer.WriteLine("SCORE        : geçersiz (maç tamamlanmadı)");
            writer.WriteLine($"Event sayısı : {result.Events.Length}");
            writer.WriteLine();
            writer.WriteLine("Abort nedeni: " + (result.AbortReason ?? "belirtilmedi"));
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
        WriteStat(writer, "AST", home, away, f => $"{f.Assists}");
        WriteStat(writer, "TOV", home, away, f => $"{f.Turnovers}");
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
        writer.WriteLine("İlk olaylar (akış denetimi için):");
        writer.WriteLine(Divider());

        foreach (var matchEvent in result.Events.Take(20))
        {
            writer.WriteLine(
                $"  #{matchEvent.Sequence,-5} P{matchEvent.Period} "
                + $"clk {matchEvent.GameClockMs / 1000.0,7:F1}s "
                + $"el {matchEvent.ElapsedGameTimeMs / 1000.0,7:F1}s "
                + $"poss {matchEvent.PossessionId?.ToString() ?? "-",-4} "
                + $"act {matchEvent.ActionId?.ToString() ?? "-",-4} "
                + $"{matchEvent.Type,-17} {Describe(matchEvent)}");
        }

        writer.WriteLine();
        WriteIncompletenessNotice(writer);
    }

    private static string Describe(MatchEvent matchEvent) => matchEvent.Type switch
    {
        MatchEventType.ShotMade => $"{matchEvent.PayloadAs<ShotMadePayload>().ShotType} "
            + $"{matchEvent.PayloadAs<ShotMadePayload>().Points} puan",
        MatchEventType.ShotMissed => $"{matchEvent.PayloadAs<ShotMissedPayload>().ShotType} kaçtı",
        MatchEventType.Turnover => matchEvent.PayloadAs<TurnoverPayload>().Kind.ToString(),
        MatchEventType.Rebound => matchEvent.PayloadAs<ReboundPayload>().Offensive ? "OREB" : "DREB",
        MatchEventType.PossessionEnded => matchEvent.PayloadAs<PossessionEndedPayload>().Reason.ToString(),
        _ => string.Empty,
    };

    private static void WriteIncompletenessNotice(TextWriter writer)
    {
        writer.WriteLine("!! BU ÇIKTI TAMAMLANMIŞ BİR MAÇ MOTORU DEĞİLDİR");
        writer.WriteLine(new string('-', 78));

        foreach (var line in new[]
                 {
                     "Yok: faul, serbest atış, and-one, bonus, blok, top çalma.",
                     "Yok: out-of-bounds, substitution, timeout, uzatma (OT).",
                     "Yok: savunma taktiği çözümü, tempo, enerji/stamina, rol uyumu.",
                     "Yok: composite rating, OVR, taktik başarı etkisi.",
                     "",
                     "Bu sonuçlar KALİBRE EDİLMEMİŞ başlangıç katsayılarıyla üretildi.",
                     "Ortalama skor gerçekçi görünse de bu denge kanıtı değildir.",
                     "Sayısal doğrulama 10K/100K deneylerine (M6) bırakılmıştır.",
                     "Eşitlikte kazanan seçilmemiştir; rastgele kazanan üretilmez.",
                     "",
                     "Sıradaki adım: M3 kuralları (foul/FT/clock/OT).",
                 })
        {
            writer.WriteLine("   " + line);
        }
    }

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
