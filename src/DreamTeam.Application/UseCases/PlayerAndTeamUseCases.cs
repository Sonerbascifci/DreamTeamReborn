using System.Collections.Immutable;
using DreamTeam.Application.Ids;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.Setup;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.Application.UseCases;

/// <summary>M7: bir use case'in basarili sonucu.</summary>
public sealed record UseCaseResult<T>(bool Succeeded, T? Value, string? Error)
{
    public static UseCaseResult<T> Ok(T value) => new(true, value, null);

    public static UseCaseResult<T> Fail(string error) => new(false, default, error);
}

/// <summary>M7: D111. Her kullanici kendi oyuncu kopyalarini uretir.</summary>
public sealed class CreatePlayer
{
    /// <summary>Kalici alan genisligi; veritabani <c>varchar(80)</c>.</summary>
    public const int MaxDisplayNameLength = 80;

    private readonly IUserRepository _users;
    private readonly IPlayerRepository _players;
    private readonly ICurrentUser _current;

    public CreatePlayer(IUserRepository users, IPlayerRepository players, ICurrentUser current)
    {
        _users = users;
        _players = players;
        _current = current;
    }

    public async Task<UseCaseResult<PlayerRecord>> ExecuteAsync(
        string displayName,
        Position position,
        PlayerRatings ratings,
        CancellationToken cancellationToken)
    {
        // Girdi hatasi bir ISTISNA DEGIL, bir basarisiz sonuctur. Istemciden
        // gelen bos bir ad sunucu hatasi (500) degil, istemci hatasi (400)
        // olmalidir; API katmani Fail'i 400'e cevirir.
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return UseCaseResult<PlayerRecord>.Fail("Oyuncu adi bos olamaz.");
        }

        if (ratings is null)
        {
            return UseCaseResult<PlayerRecord>.Fail("Oyuncu degerlendirmesi gonderilmedi.");
        }

        if (!Enum.IsDefined(position))
        {
            return UseCaseResult<PlayerRecord>.Fail($"Gecersiz mevki: {(int)position}.");
        }

        var userId = _current.UserId;

        if (await _users.FindAsync(userId, cancellationToken).ConfigureAwait(false) is null)
        {
            return UseCaseResult<PlayerRecord>.Fail($"Kullanici bulunamadi: {userId}.");
        }

        if (displayName.Length > MaxDisplayNameLength)
        {
            return UseCaseResult<PlayerRecord>.Fail(
                $"Oyuncu adi {MaxDisplayNameLength} karakteri asamaz.");
        }

        // Id UYGULAMADAN turetilir: ayni girdi ayni id verir, bu da replay ve
        // testler icin kullanislidir. Guid.NewGuid burada YASAKTIR cunku motor
        // disinda da olsa determinizm sözlesmesini delmek yerel ve izlenebilir
        // olmazdi.
        var id = DerivedId.From("player", userId.ToString(), displayName, position.ToString());

        var record = new PlayerRecord(id, userId, displayName.Trim(), position, ratings);

        if (await _players.FindAsync(id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return UseCaseResult<PlayerRecord>.Fail(
                $"Bu kimlikte bir oyuncu zaten var: {id}. Farkli bir ad deneyin.");
        }

        await _players.AddAsync(record, cancellationToken).ConfigureAwait(false);
        return UseCaseResult<PlayerRecord>.Ok(record);
    }

}

/// <summary>M7: D111. Kadro boyutu serbest; lineup 5 olmak zorundadir (D31).</summary>
public sealed class SetLineup
{
    private readonly ITeamRepository _teams;
    private readonly ICurrentUser _current;

    public SetLineup(ITeamRepository teams, ICurrentUser current)
    {
        _teams = teams;
        _current = current;
    }

    public async Task<UseCaseResult<ImmutableArray<Guid>>> ExecuteAsync(
        Guid teamId,
        IReadOnlyList<Guid> playerIds,
        CancellationToken cancellationToken)
    {
        // Ayni ilke: istemciden gelen hatali girdi basarisiz sonuc doner.
        if (teamId == Guid.Empty)
        {
            return UseCaseResult<ImmutableArray<Guid>>.Fail("Takim kimligi bos olamaz.");
        }

        if (playerIds is null)
        {
            return UseCaseResult<ImmutableArray<Guid>>.Fail("Lineup gonderilmedi.");
        }

        var team = await _teams.FindAsync(teamId, cancellationToken).ConfigureAwait(false);

        if (team is null)
        {
            return UseCaseResult<ImmutableArray<Guid>>.Fail($"Takim bulunamadi: {teamId}.");
        }

        if (team.OwnerUserId != _current.UserId)
        {
            // T19: baska bir takima yazma denemesi burada durur ve MOTORA ULAŞMAZ.
            return UseCaseResult<ImmutableArray<Guid>>.Fail("Bu takim size ait degil.");
        }

        if (playerIds.Count != 5)
        {
            return UseCaseResult<ImmutableArray<Guid>>.Fail(
                $"Lineup 5 oyuncu olmali; {playerIds.Count} verildi. " +
                "Bu bir MOTOR kuralidir (D31), degistirilemez.");
        }

        if (playerIds.Distinct().Count() != 5)
        {
            return UseCaseResult<ImmutableArray<Guid>>.Fail("Lineup'ta ayni oyuncu iki kez var.");
        }

        var roster = await _teams.RosterPlayerIdsAsync(teamId, cancellationToken).ConfigureAwait(false);
        var rosterSet = roster.ToHashSet();

        foreach (var id in playerIds)
        {
            if (!rosterSet.Contains(id))
            {
                return UseCaseResult<ImmutableArray<Guid>>.Fail(
                    $"Oyuncu kadroda degil: {id}.");
            }
        }

        return UseCaseResult<ImmutableArray<Guid>>.Ok([.. playerIds]);
    }
}
