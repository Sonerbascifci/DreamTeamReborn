using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.UseCases;
using DreamTeam.Api.Contracts;
using DreamTeam.MatchEngine.Events;
using Microsoft.AspNetCore.SignalR;

namespace DreamTeam.Api.Realtime;

/// <summary>
/// M7: <c>IMatchBroadcaster</c> uygulamasi. SignalR ile.
/// <para><b>AYRIM KURALI:</b> Application katmani SignalR'yi BILMEZ; bu tip
/// o bilgiyi sinirin bu tarafinda tutar. Port olmasa burada dogrudan
/// <c>IHubContext</c> cagrilirdi ve use case testleri bir web sunucusu
/// ayaga kaldirmak zorunda kalirdi.</para>
/// </summary>
public sealed class SignalRMatchBroadcaster : IMatchBroadcaster
{
    /// <summary>Grup adi. Istemci once <c>Subscribe(matchId)</c> cagirir.</summary>
    public static string GroupFor(Guid matchId) => $"match:{matchId:D}";

    private readonly IHubContext<MatchHub> _hub;

    public SignalRMatchBroadcaster(IHubContext<MatchHub> hub) => _hub = hub;

    public async Task PublishEventsAsync(
        Guid matchId,
        IReadOnlyList<MatchEvent> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await _hub.Clients
            .Group(GroupFor(matchId))
            .SendAsync("Events", MatchEventDtoFactory.From(events), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task PublishCommandResultAsync(
        Guid matchId,
        Guid commandId,
        bool applied,
        string? reason,
        CancellationToken cancellationToken)
    {
        await _hub.Clients
            .Group(GroupFor(matchId))
            .SendAsync("CommandResult", new CommandAck
            {
                CommandId = commandId,
                Accepted = true,
                Applied = applied,
                Epoch = 0,
                Error = reason,
            }, cancellationToken).ConfigureAwait(false);
    }
}
