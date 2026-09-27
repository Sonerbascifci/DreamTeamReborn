using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Bir <see cref="MatchSetup"/>'ın maç başlatmaya uygun olup olmadığını belirleyen
/// tek kapıdır. Model tipleri bilerek yalnız veri taşır; aralık ve tutarlılık
/// kontrolleri burada toplanır, böylece reddedilen her alan makine tarafından
/// okunabilir bir kod ve yol ile raporlanır.
///
/// Bu sürümde rating aralığı 0-100'dür ve lineup tam olarak beş oyuncu içermelidir.
/// Sınır dışı değer, yinelenen lineup üyesi, kadro dışı oyuncu, boş/tanımsız kimlik
/// ve setup ile üretim RNG'si arasındaki kimlik uyumsuzluğu reddedilir.
/// </summary>
public static class MatchSetupValidator
{
    public const int RequiredLineupSize = 5;

    public const int MinimumRating = 0;

    public const int MaximumRating = 100;

    /// <summary>
    /// 18 attribute'ün tek listesi. Doğrulama bu tabloyu kullanır; tablonun
    /// <see cref="PlayerRatings"/> ile senkron olduğu bir testle doğrulanır, böylece
    /// yeni bir attribute eklendiğinde sessizce doğrulanmamış kalmaz.
    /// </summary>
    private static readonly (string Name, Func<PlayerRatings, int> Select)[] RatingAccessors =
    [
        (nameof(PlayerRatings.Speed), ratings => ratings.Speed),
        (nameof(PlayerRatings.Strength), ratings => ratings.Strength),
        (nameof(PlayerRatings.Vertical), ratings => ratings.Vertical),
        (nameof(PlayerRatings.Stamina), ratings => ratings.Stamina),
        (nameof(PlayerRatings.Inside), ratings => ratings.Inside),
        (nameof(PlayerRatings.MidRange), ratings => ratings.MidRange),
        (nameof(PlayerRatings.ThreePoint), ratings => ratings.ThreePoint),
        (nameof(PlayerRatings.FreeThrow), ratings => ratings.FreeThrow),
        (nameof(PlayerRatings.BallHandling), ratings => ratings.BallHandling),
        (nameof(PlayerRatings.Passing), ratings => ratings.Passing),
        (nameof(PlayerRatings.OffBall), ratings => ratings.OffBall),
        (nameof(PlayerRatings.PostOffense), ratings => ratings.PostOffense),
        (nameof(PlayerRatings.PerimeterDefense), ratings => ratings.PerimeterDefense),
        (nameof(PlayerRatings.InteriorDefense), ratings => ratings.InteriorDefense),
        (nameof(PlayerRatings.Steal), ratings => ratings.Steal),
        (nameof(PlayerRatings.Block), ratings => ratings.Block),
        (nameof(PlayerRatings.Rebounding), ratings => ratings.Rebounding),
        (nameof(PlayerRatings.BasketballIQ), ratings => ratings.BasketballIQ),
    ];

    public static MatchSetupValidationResult Validate(MatchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        var errors = new List<MatchSetupValidationError>();

        if (setup.MatchId == Guid.Empty)
        {
            Add(errors, MatchSetupErrorCode.MatchIdMissing, "MatchId", "MatchId Guid.Empty olamaz.");
        }

        ValidateEngineIdentity(setup.Engine, errors);

        ValidateTeam(setup.Home, "Home", errors);
        ValidateTeam(setup.Away, "Away", errors);

        if (setup.Home is not null && setup.Away is not null
            && setup.Home.Team is not null && setup.Away.Team is not null
            && setup.Home.Team.Id == setup.Away.Team.Id)
        {
            Add(
                errors,
                MatchSetupErrorCode.SameTeamOnBothSides,
                "Away.Team.Id",
                "Home ve Away aynı takımı gösteremez.");
        }

        return errors.Count == 0
            ? MatchSetupValidationResult.Valid
            : new MatchSetupValidationResult(errors);
    }

    /// <summary>
    /// M4: taktik ve tempo enum değerlerini doğrular (D61).
    ///
    /// Bunlar enum oldukları için derleme düzeyinde geçerli değerlerdir; kontrol
    /// yine de yapılır çünkü <b>fixture dosyasından</b> veya deserialize edilmiş
    /// veriden gelen bir setup'ta tanımsız bir değer mümkündür (M6'da). Sessizce
    /// varsayılana düşmek, kullanıcıya "Balanced oynadık" derken başka bir
    /// taktik oynamak demektir.
    /// </summary>
    private static void ValidateTactics(TeamMatchSetup? setup, string side, List<MatchSetupValidationError> errors)
    {
        if (setup is null)
        {
            return;
        }

        if (!Enum.IsDefined(setup.Offensive))
        {
            Add(
                errors,
                MatchSetupErrorCode.UnknownTactic,
                $"{side}.Offensive",
                $"Tanımsız hücum taktiği: {(int)setup.Offensive}.");
        }

        if (!Enum.IsDefined(setup.Defense))
        {
            Add(
                errors,
                MatchSetupErrorCode.UnknownTactic,
                $"{side}.Defense",
                $"Tanımsız savunma policy'si: {(int)setup.Defense}.");
        }

        if (!Enum.IsDefined(setup.Pace))
        {
            Add(
                errors,
                MatchSetupErrorCode.UnknownPace,
                $"{side}.Pace",
                $"Tanımsız tempo: {(int)setup.Pace}.");
        }
    }

    private static void ValidateEngineIdentity(EngineIdentity? engine, List<MatchSetupValidationError> errors)
    {
        if (engine is null)
        {
            Add(errors, MatchSetupErrorCode.EngineIdentityIncomplete, "Engine", "Engine kimliği null olamaz.");
            return;
        }

        var engineVersionOk = RequireText(engine.EngineVersion, "Engine.EngineVersion", errors);
        RequireText(engine.RulesVersion, "Engine.RulesVersion", errors);
        RequireText(engine.BalanceConfigHash, "Engine.BalanceConfigHash", errors);
        var rngAlgorithmOk = RequireText(engine.RngAlgorithm, "Engine.RngAlgorithm", errors);
        var rngVersionOk = RequireText(engine.RngVersion, "Engine.RngVersion", errors);

        if (engineVersionOk && !string.Equals(engine.EngineVersion, EngineVersion.Current, StringComparison.Ordinal))
        {
            Add(
                errors,
                MatchSetupErrorCode.EngineIdentityIncomplete,
                "Engine.EngineVersion",
                $"Setup motorun yerleşik sürümüyle eşleşmiyor: '{engine.EngineVersion}' != '{EngineVersion.Current}'. "
                + "Aynı seed farklı motor sürümünde aynı sonucu vermez.");
        }

        if (rngAlgorithmOk
            && rngVersionOk
            && (!string.Equals(engine.RngAlgorithm, RngIdentity.Algorithm, StringComparison.Ordinal)
                || !string.Equals(engine.RngVersion, RngIdentity.Version, StringComparison.Ordinal)))
        {
            Add(
                errors,
                MatchSetupErrorCode.RngIdentityMismatch,
                "Engine.RngAlgorithm",
                $"Setup RNG kimliği '{engine.RngAlgorithm}/{engine.RngVersion}' motorun yerleşik "
                + $"RNG'siyle '{RngIdentity.Algorithm}/{RngIdentity.Version}' eşleşmiyor. "
                + "Bu setup aynı seed ile yeniden üretilemez.");
        }
    }

    private static bool RequireText(string? value, string path, List<MatchSetupValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(errors, MatchSetupErrorCode.EngineIdentityIncomplete, path, "Değer boş olamaz.");
            return false;
        }

        return true;
    }

    /// <summary>Bir takımı ve onun lineup'ını doğrular.</summary>
    /// <summary>
    /// M4'te imza değişti: takım, lineup ve taktikler artık tek
    /// <see cref="TeamMatchSetup"/> içinde (D61). Doğrulama kapsamı aynıdır —
    /// kadro, lineup, kimlik — artı taktik/tempo enum'ları.
    /// </summary>
    private static void ValidateTeam(
        TeamMatchSetup? setup,
        string side,
        List<MatchSetupValidationError> errors)
    {
        ValidateTactics(setup, side, errors);

        if (setup is null)
        {
            Add(errors, MatchSetupErrorCode.TeamMissing, side, $"{side} takımı null olamaz.");
            ValidateLineup(null, side, rosterUsable: false, rosterIds: null, errors);
            return;
        }

        var team = setup.Team;

        if (team is null)
        {
            Add(errors, MatchSetupErrorCode.TeamMissing, side, $"{side} takımı null olamaz.");
            ValidateLineup(setup.Lineup, side, rosterUsable: false, rosterIds: null, errors);
            return;
        }

        if (team.Id == Guid.Empty)
        {
            Add(errors, MatchSetupErrorCode.TeamIdMissing, $"{side}.Team.Id", $"{side}.Id Guid.Empty olamaz.");
        }

        var rosterIds = new HashSet<Guid>();
        var rosterUsable = ValidateRoster(team.Roster, side, rosterIds, errors);

        ValidateLineup(setup.Lineup, side, rosterUsable, rosterIds, errors);
    }

    private static bool ValidateRoster(
        ImmutableArray<Player> roster,
        string side,
        HashSet<Guid> rosterIds,
        List<MatchSetupValidationError> errors)
    {
        if (roster.IsDefaultOrEmpty)
        {
            Add(errors, MatchSetupErrorCode.RosterMissing, $"{side}.Roster", $"{side} kadrosu boş olamaz.");
            return false;
        }

        var usable = true;

        for (var index = 0; index < roster.Length; index++)
        {
            var player = roster[index];
            var path = $"{side}.Roster[{index}]";

            if (player is null)
            {
                Add(errors, MatchSetupErrorCode.RosterPlayerMissing, path, "Kadro girdisi null olamaz.");
                usable = false;
                continue;
            }

            if (player.Id == Guid.Empty)
            {
                Add(errors, MatchSetupErrorCode.RosterPlayerIdMissing, $"{path}.Id", "Oyuncu kimliği Guid.Empty olamaz.");
                usable = false;
            }
            else if (!rosterIds.Add(player.Id))
            {
                Add(
                    errors,
                    MatchSetupErrorCode.DuplicateRosterPlayerId,
                    $"{path}.Id",
                    "Aynı oyuncu kadroda iki kez yer alamaz.");
                usable = false;
            }

            if (player.Ratings is null)
            {
                Add(errors, MatchSetupErrorCode.RatingsMissing, $"{path}.Ratings", "Rating verisi null olamaz.");
                usable = false;
                continue;
            }

            foreach (var (name, select) in RatingAccessors)
            {
                var value = select(player.Ratings);

                if (value < MinimumRating || value > MaximumRating)
                {
                    Add(
                        errors,
                        MatchSetupErrorCode.RatingOutOfRange,
                        $"{path}.Ratings.{name}",
                        $"Rating {MinimumRating}-{MaximumRating} aralığında olmalıdır; bulunan {value}.");
                }
            }
        }

        return usable;
    }

    private static void ValidateLineup(
        Lineup? lineup,
        string side,
        bool rosterUsable,
        HashSet<Guid>? rosterIds,
        List<MatchSetupValidationError> errors)
    {
        if (lineup is null || lineup.PlayerIds.IsDefault)
        {
            Add(errors, MatchSetupErrorCode.LineupMissing, $"{side}Lineup", $"{side} lineup'ı null veya tanımsız olamaz.");
            return;
        }

        var ids = lineup.PlayerIds;

        if (ids.Length != RequiredLineupSize)
        {
            Add(
                errors,
                MatchSetupErrorCode.LineupSizeInvalid,
                $"{side}Lineup.PlayerIds",
                $"Lineup tam olarak {RequiredLineupSize} oyuncu içermelidir; bulunan {ids.Length}.");
        }

        var seen = new HashSet<Guid>();

        for (var index = 0; index < ids.Length; index++)
        {
            var id = ids[index];
            var path = $"{side}Lineup.PlayerIds[{index}]";

            if (id == Guid.Empty)
            {
                Add(errors, MatchSetupErrorCode.LineupPlayerIdMissing, path, "Lineup girdisi Guid.Empty olamaz.");
                continue;
            }

            if (!seen.Add(id))
            {
                Add(errors, MatchSetupErrorCode.DuplicateLineupPlayerId, path, "Aynı oyuncu iki slotta yer alamaz.");
                continue;
            }

            // Kadro zaten hatalıysa üyeliği denetlemek gürültü üretir; üyelik ancak
            // kadro güvenilirken denetlenir.
            if (rosterUsable && rosterIds is not null && !rosterIds.Contains(id))
            {
                Add(
                    errors,
                    MatchSetupErrorCode.LineupPlayerNotInRoster,
                    path,
                    "Lineup'taki oyuncu takım kadrosunda bulunamadı.");
            }
        }
    }

    private static void Add(
        List<MatchSetupValidationError> errors,
        MatchSetupErrorCode code,
        string field,
        string message) =>
        errors.Add(new MatchSetupValidationError(code, field, message));
}
