using System.Collections.Immutable;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// <c>Advance</c>'in tek adımda ürettiği çıktı: yeni state, bu adımda üretilen
/// event'ler, <b>komut sonuçları</b> ve terminal olup olmadığı.
///
/// <para><b>M5'te neden büyüdü?</b> 03 §"Offline ile canlı yürütme": "StepResult
/// yeni state, events, <b>command sonuçları</b> ve durum taşır." Komut sonucu
/// ayrı bir dönüş değerine değil, <b>bu yapının içine</b> konur; böylece çağıran
/// taraf (M7 runner) state + event + komut sonucunu tek yerden alır ve
/// "ACK mi, Applied mi" karışıklığı (07 §5) oluşmaz.</para>
///
/// <para><b>Reddedilen komut <c>IsTerminal</c> yapmaz.</b> Yönetici hata
/// yaptığında maç devam eder (07 §5).</para>
/// </summary>
public readonly record struct StepResult(
    MatchState State,
    ImmutableArray<MatchEvent> Events,
    ImmutableArray<CommandResult> CommandResults,
    bool IsTerminal)
{
    public static StepResult NonTerminal(
        MatchState state,
        ImmutableArray<MatchEvent> events,
        ImmutableArray<CommandResult> commandResults) =>
        new(state, events, commandResults, state.IsTerminal);

    public static StepResult Terminal(
        MatchState state,
        ImmutableArray<MatchEvent> events,
        ImmutableArray<CommandResult> commandResults) =>
        new(state, events, commandResults, true);

    /// <summary>M5: komut sonucu olmayan bir adım için kısayol.</summary>
    public static StepResult NonTerminal(MatchState state, ImmutableArray<MatchEvent> events) =>
        NonTerminal(state, events, CommandResultHelpers.Empty);
}
