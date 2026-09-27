namespace DreamTeam.MatchEngine.Core;

/// <summary>Setup doğrulamasında üretilebilecek hata sınıfları.</summary>
public enum MatchSetupErrorCode
{
    None = 0,

    MatchIdMissing,
    TeamMissing,
    TeamIdMissing,
    SameTeamOnBothSides,
    RosterMissing,
    RosterPlayerMissing,
    RosterPlayerIdMissing,
    DuplicateRosterPlayerId,
    RatingsMissing,
    RatingOutOfRange,
    LineupMissing,
    LineupPlayerIdMissing,
    DuplicateLineupPlayerId,
    LineupSizeInvalid,
    LineupPlayerNotInRoster,
    EngineIdentityIncomplete,
    RngIdentityMismatch,
}

/// <summary>
/// Tek bir doğrulama hatası. <see cref="Field"/> reddedilen girdi yoludur
/// (örnegin <c>Home.Roster[3].Ratings.ThreePoint</c>) ve M7'de API yanıtında
/// kullanıcıya gösterilebilir.
/// </summary>
public sealed record MatchSetupValidationError(MatchSetupErrorCode Code, string Field, string Message);

/// <summary>
/// Doğrulama sonucu. Tüm hatalar toplanır; ilk hatada durulmaz, böylece çağıran
/// taraf tek geçişte tüm reddedilen alanları görebilir.
/// </summary>
public sealed class MatchSetupValidationResult
{
    public static MatchSetupValidationResult Valid { get; } = new([]);

    public MatchSetupValidationResult(IReadOnlyList<MatchSetupValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors;
    }

    public bool IsValid => Errors.Count == 0;

    public IReadOnlyList<MatchSetupValidationError> Errors { get; }

    public bool Contains(MatchSetupErrorCode code) =>
        Errors.Any(error => error.Code == code);
}
