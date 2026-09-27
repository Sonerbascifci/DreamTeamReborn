using System.Collections.Immutable;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// <c>Advance</c>'in tek adımda ürettiği çıktı: yeni state, bu adımda üretilen
/// event'ler ve terminal olup olmadığı.
/// </summary>
public readonly record struct StepResult(
    MatchState State,
    ImmutableArray<MatchEvent> Events,
    bool IsTerminal)
{
    public static StepResult NonTerminal(MatchState state, ImmutableArray<MatchEvent> events) =>
        new(state, events, state.IsTerminal);

    public static StepResult Terminal(MatchState state, ImmutableArray<MatchEvent> events) =>
        new(state, events, true);
}
