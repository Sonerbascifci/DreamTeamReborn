using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DreamTeam.Domain.Players;

namespace DreamTeam.MatchEngine.Config;

/// <summary>Şut türü. 04_DOMAIN_AND_DATA_MODEL.md sözlüğü. FT ayrı çözülür, listede yoktur.</summary>
public enum ShotType
{
    AtRim,
    ClosePost,
    MidRange,
    ThreePoint,
}

/// <summary>Hücum aksiyon ailesi. 04_DOMAIN_AND_DATA_MODEL.md sözlüğü.</summary>
public enum OffensiveAction
{
    PickAndRoll,
    Drive,
    SpotUp,
    Isolation,
    Cut,
    PostUp,

    /// <summary>M2'de ağırlığı sıfırdır; 04 sözlüğünde vardır, M4 dağıtımı bekler.</summary>
    OffBallScreen,
}

/// <summary>Top kaybı nedeni. 04_DOMAIN_AND_DATA_MODEL.md sözlüğü.</summary>
public enum TurnoverKind
{
    /// <summary>M2'de kullanılmaz; geçmiş taslaktan gelen ayrım.</summary>
    BadPass,

    LostBall,

    /// <summary>M3 konusu.</summary>
    OffensiveFoul,

    /// <summary>M3 konusu.</summary>
    Travel,

    ShotClockViolation,
}

/// <summary>
/// Saat ve periyot kuralları. 06_RULES_AND_STATE_MACHINE.md §1'deki sade profil
/// (D31). Tam NBA sadakati iddiası taşımaz: bonus, foul-out ve timeout yoktur.
/// </summary>
public sealed record RulesProfile
{
    public required int PeriodCount { get; init; }

    public required long PeriodDurationMs { get; init; }

    public required long ShotClockMs { get; init; }

    /// <summary>M3'te uzatmayı açacak. M2'de tanımlı ama kullanılmaz.</summary>
    public required long OvertimeDurationMs { get; init; }

    public static RulesProfile SimpleNbaInspired { get; } = new()
    {
        PeriodCount = 4,
        PeriodDurationMs = 12 * 60 * 1000,
        ShotClockMs = 24 * 1000,
        OvertimeDurationMs = 5 * 60 * 1000,
    };
}

/// <summary>
/// Şut isabet modeli. 05_MATCH_ENGINE_SPEC.md §7'nin boyutsal şablonu:
/// <c>z = logit(base) + SkillScale * skill</c>, <c>skill = (rating - 50) / 50</c>.
///
/// Buradaki <c>Base*</c> değerleri 05'teki *başlangıç aralıklarının* orta noktasıdır.
/// Bunlar aggregate fixture hipotezidir, NBA ortalaması iddiası değildir ve
/// kalibre edilmemiştir. Ölçüm M6'nın işidir.
/// </summary>
public sealed record ShotModel
{
    public required double AtRimBase { get; init; }

    public required double ClosePostBase { get; init; }

    public required double MidRangeBase { get; init; }

    public required double ThreePointBase { get; init; }

    /// <summary>Ham rating ile logit arasındaki ölçek. Kalibre edilmemiş.</summary>
    public required double SkillScale { get; init; }

    public double BaseFor(ShotType shotType) => shotType switch
    {
        ShotType.AtRim => AtRimBase,
        ShotType.ClosePost => ClosePostBase,
        ShotType.MidRange => MidRangeBase,
        ShotType.ThreePoint => ThreePointBase,
        _ => throw new ArgumentOutOfRangeException(nameof(shotType), shotType, "Bilinmeyen şut türü."),
    };

    public static ShotModel Baseline { get; } = new()
    {
        // 05 §7 aralıklarının orta noktaları: AtRim %60-68 -> .64,
        // ClosePost %48-58 -> .53, MidRange %38-45 -> .415, ThreePoint %33-39 -> .36
        AtRimBase = 0.64,
        ClosePostBase = 0.53,
        MidRangeBase = 0.415,
        ThreePointBase = 0.36,
        SkillScale = 0.6,
    };
}

/// <summary>
/// Bir aksiyonun hangi şut türüne gittiği ve beceriyi hangi attribute'ten okuduğu.
///
/// M2'de bu eşleme basittir ve <b>yer tutucudur</b>: 05 §5, seçim fonksiyonunun
/// M2 planında kilitlenmesini istiyor. Dört hücum taktiğinin ayrı dağılımları ve
/// composite rating'ler M4'te gelir. Burada 18 attribute'dan var olanlar kullanılır;
/// hiçbir yeni attribute veya gizli ceza uydurulmamıştır.
/// </summary>
public sealed record ActionProfile
{
    public required OffensiveAction Action { get; init; }

    public required double Weight { get; init; }

    public required ShotType ShotType { get; init; }

    public required Func<PlayerRatings, int> Skill { get; init; }

    /// <summary>
    /// 05 §5'teki PickAndRoll örnek dağılımı, tacticsiz M2 için başlangıç karması
    /// olarak kullanılır. 05, diğer taktiklerin dağılımlarını henüz vermedi.
    /// </summary>
    public static ImmutableArray<ActionProfile> Baseline { get; } =
    [
        new()
        {
            Action = OffensiveAction.PickAndRoll,
            Weight = 0.45,
            ShotType = ShotType.MidRange,
            Skill = ratings => ratings.BallHandling,
        },
        new()
        {
            Action = OffensiveAction.Drive,
            Weight = 0.15,
            ShotType = ShotType.ClosePost,
            Skill = ratings => ratings.Vertical,
        },
        new()
        {
            Action = OffensiveAction.SpotUp,
            Weight = 0.15,
            ShotType = ShotType.ThreePoint,
            Skill = ratings => ratings.ThreePoint,
        },
        new()
        {
            Action = OffensiveAction.Isolation,
            Weight = 0.10,
            ShotType = ShotType.MidRange,
            Skill = ratings => ratings.BallHandling,
        },
        new()
        {
            Action = OffensiveAction.Cut,
            Weight = 0.10,
            ShotType = ShotType.AtRim,
            Skill = ratings => ratings.OffBall,
        },
        new()
        {
            Action = OffensiveAction.PostUp,
            Weight = 0.05,
            ShotType = ShotType.ClosePost,
            Skill = ratings => ratings.PostOffense,
        },
        new()
        {
            Action = OffensiveAction.OffBallScreen,
            Weight = 0.0,
            ShotType = ShotType.MidRange,
            Skill = ratings => ratings.OffBall,
        },
    ];
}

/// <summary>Aksiyon süreleri ve olasılık katsayıları. Hepsi kalibre edilmemiş başlangıçtır.</summary>
public sealed record ActionModel
{
    /// <summary>Bir aksiyonun canlı oyun süresinde tükettiği zaman (pas, sürüş, kurulum).</summary>
    public required long SetupActionMs { get; init; }

    /// <summary>Şutun bırakılması ile sonucunun çözülmesi arasındaki uçuş süresi.</summary>
    public required long ShotFlightMs { get; init; }

    /// <summary>Bir aksiyonun şut denemesine dönüşme olasılığı. Dönüşmezse possession devam eder.</summary>
    public required double ShotCompletionProbability { get; init; }

    /// <summary>Aksiyon başına top kaybı olasılığı.</summary>
    public required double TurnoverProbability { get; init; }

    /// <summary>Canlı miss sonrası hücum ribaundu olasılığı.</summary>
    public required double OffensiveReboundProbability { get; init; }

    public static ActionModel Baseline { get; } = new()
    {
        SetupActionMs = 8_000,
        ShotFlightMs = 1_500,
        ShotCompletionProbability = 0.65,
        TurnoverProbability = 0.05,
        OffensiveReboundProbability = 0.26,
    };
}

/// <summary>
/// Motorun değişken girdileri. 05 §16 gereği katsayılar config'te, algoritma
/// semantiği koddadır. Config değişimi yalnız yeni maçlara uygulanır; oynayan maç
/// dondurulmuş snapshot'ı kullanmaya devam eder.
///
/// Bu tip <b>kayıttır</b>: değer eşitliği testlerde kullanılır, hash içeriğe bağlıdır.
/// </summary>
public sealed record EngineConfig
{
    public required RulesProfile Rules { get; init; }

    public required ShotModel Shot { get; init; }

    public required ActionModel Actions { get; init; }

    public required ImmutableArray<ActionProfile> ActionProfiles { get; init; }

    /// <summary>
    /// Sonlanma güvenliği. Aşılırsa maç <c>Aborted</c> olur; skor uydurulmaz
    /// (06 §2). Bu bir denge parametresi değil, sonsuz döngü emniyetidir.
    /// </summary>
    public required int MaxActionsPerMatch { get; init; }

    public static EngineConfig Baseline { get; } = new()
    {
        Rules = RulesProfile.SimpleNbaInspired,
        Shot = ShotModel.Baseline,
        Actions = ActionModel.Baseline,
        ActionProfiles = ActionProfile.Baseline,
        MaxActionsPerMatch = 20_000,
    };

    /// <summary>
    /// Config içeriğinin kararlı kimliği. Sabit sıralı bir metin kurulur ve SHA-256
    /// alınır. JSON canonicalization kullanılmaz: 08 §4 byte equality beklemez, ama
    /// aynı config her zaman aynı hash'i vermelidir. Aynı seed'in farklı config ile
    /// oynanması farklı sonuç vereceği için bu hash setup'a yazılır.
    /// </summary>
    public string ComputeConfigHash()
    {
        var builder = new StringBuilder();

        void Append(string key, object value) =>
            builder.Append(key).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('\n');

        void AppendReal(string key, double value) =>
            builder.Append(key).Append('=').Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');

        Append("periodCount", Rules.PeriodCount);
        Append("periodDurationMs", Rules.PeriodDurationMs);
        Append("shotClockMs", Rules.ShotClockMs);
        Append("overtimeDurationMs", Rules.OvertimeDurationMs);

        AppendReal("atRimBase", Shot.AtRimBase);
        AppendReal("closePostBase", Shot.ClosePostBase);
        AppendReal("midRangeBase", Shot.MidRangeBase);
        AppendReal("threePointBase", Shot.ThreePointBase);
        AppendReal("skillScale", Shot.SkillScale);

        Append("setupActionMs", Actions.SetupActionMs);
        Append("shotFlightMs", Actions.ShotFlightMs);
        AppendReal("shotCompletionProbability", Actions.ShotCompletionProbability);
        AppendReal("turnoverProbability", Actions.TurnoverProbability);
        AppendReal("offensiveReboundProbability", Actions.OffensiveReboundProbability);

        foreach (var profile in ActionProfiles)
        {
            Append("action." + profile.Action, profile.Weight);
            Append("shotType." + profile.Action, profile.ShotType);
        }

        Append("maxActionsPerMatch", MaxActionsPerMatch);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));

        return Convert.ToHexStringLower(digest)[..16];
    }
}
