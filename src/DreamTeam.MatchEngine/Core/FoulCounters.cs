using System.Collections.Immutable;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Faul sayaçları. 04'teki <c>PlayerMatchState</c>'in faul kısmı; M3'te enerji ve
/// oynanan süre gelmediği için yalnız sayaçlar tutulur.
///
/// İki tane ayrı sayaç vardır ve karıştırılmamalıdır:
/// - <see cref="TeamFoulsThisPeriod"/> periyot sayacıdır; her periyot başında
///   sıfırlanır (D42: uzatmada da sıfırlanır) ve bonusu belirler.
/// - Kişisel sayaçlar maç boyunca birikir ve foul-out'u belirler.
/// </summary>
public sealed record FoulCounters
{
    public required int TeamFoulsThisPeriod { get; init; }

    public required ImmutableArray<FoulTally> Personal { get; init; }

    public static FoulCounters Empty { get; } = new()
    {
        TeamFoulsThisPeriod = 0,
        Personal = [],
    };

    public int PersonalOf(Guid playerId)
    {
        foreach (var tally in Personal)
        {
            if (tally.PlayerId == playerId)
            {
                return tally.Count;
            }
        }

        return 0;
    }

    public FoulCounters WithPersonalFoul(Guid playerId)
    {
        var updated = Personal.ToBuilder();
        var index = -1;

        for (var scan = 0; scan < updated.Count; scan++)
        {
            if (updated[scan].PlayerId == playerId)
            {
                index = scan;
                break;
            }
        }

        if (index < 0)
        {
            updated.Add(new FoulTally(playerId, 1));
        }
        else
        {
            updated[index] = updated[index] with { Count = updated[index].Count + 1 };
        }

        return this with { Personal = updated.ToImmutable() };
    }

    public FoulCounters WithTeamFoulThisPeriod() =>
        this with { TeamFoulsThisPeriod = TeamFoulsThisPeriod + 1 };

    public FoulCounters ResetTeamFouls() => this with { TeamFoulsThisPeriod = 0 };
}

public readonly record struct FoulTally(Guid PlayerId, int Count);
