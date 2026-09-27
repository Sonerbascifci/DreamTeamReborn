using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Ratings;

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
/// Faul türü. 04_DOMAIN_AND_DATA_MODEL.md sözlüğü. Teknik ve flagrant faul
/// v0.1 dışındadır (06 §1 "İleri kural istisnaları").
/// </summary>
public enum FoulType
{
    Shooting,
    NonShooting,
    Offensive,
}

/// <summary>
/// Faul kuralları. 06 §1'in basit profili (D40):
/// periyot içinde 5. sayılan savunma faulünden itibaren 2 FT, kişisel sınır 6.
///
/// Bu bir **sade** profildir. NBA'nın 3 saniye kuralı, son iki dakika
/// istisnaları ve frontcourt/backcourt ayrımı kapsam dışıdır; tam NBA
/// sadakati iddiası taşımaz.
/// </summary>
public sealed record FoulModel
{
    /// <summary>Bir aksiyon başına faul olma olasılığı. Kalibre edilmemiş.</summary>
    public required double FoulProbabilityPerAction { get; init; }

    /// <summary>Fauller içinde hücum faulü payı. Kalibre edilmemiş.</summary>
    public required double OffensiveFoulShare { get; init; }

    /// <summary>Savunma faulleri içinde shooting faulü payı. Kalibre edilmemiş.</summary>
    public required double ShootingFoulShare { get; init; }

    /// <summary>Bu sayıdan sonraki savunma faulü bonusa girer (D40: 5).</summary>
    public required int BonusTeamFoulThreshold { get; init; }

    /// <summary>Bonus serbest atışı sayısı (D40: 2).</summary>
    public required int BonusFreeThrowCount { get; init; }

    /// <summary>Kişisel faul sınırı; bu sayıda oyuncu sahadan çıkar (D40: 6).</summary>
    public required int PersonalFoulLimit { get; init; }

    public static FoulModel Baseline { get; } = new()
    {
        // Bu deger, 400 aksiyonluk bir mac takim basina ~18 savunma faulu
        // uretecek sekilde secildi. KALIBRE EDILMEMISTIR; hedef araligi M6
        // olcumune aittir.
        FoulProbabilityPerAction = 0.12,
        OffensiveFoulShare = 0.25,
        ShootingFoulShare = 0.45,
        BonusTeamFoulThreshold = 5,
        BonusFreeThrowCount = 2,
        PersonalFoulLimit = 6,
    };
}

/// <summary>
/// Serbest atış isabet modeli. M2'de FT kuralı yoktu; burada taban olasılık
/// oyuncu FreeThrow attribute'ünden okunur. Kalibre edilmemiştir.
/// </summary>
public sealed record FreeThrowModel
{
    public required double BaseMakeProbability { get; init; }

    public required double SkillScale { get; init; }

    public static FreeThrowModel Baseline { get; } = new()
    {
        BaseMakeProbability = 0.78,
        SkillScale = 0.8,
    };
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

    /// <summary>
    /// Hucre ribaundu sonrasi hucre saati (06 section 6, D31 profili). Yalnizca
    /// cembere degen miss icin uygulanir; cembere degmeyen miss'te otomatik
    /// reset yok.
    /// </summary>
    public required long OffensiveReboundShotClockMs { get; init; }

    public static RulesProfile SimpleNbaInspired { get; } = new()
    {
        PeriodCount = 4,
        PeriodDurationMs = 12 * 60 * 1000,
        ShotClockMs = 24 * 1000,
        OvertimeDurationMs = 5 * 60 * 1000,
        OffensiveReboundShotClockMs = 14 * 1000,
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

    /// <summary>
    /// M4: şut kalitesi ile logit arasındaki ölçek (05 §7 <c>betaQuality</c>).
    /// Savunma ve taktik etkisinin <b>tek</b> kanalıdır.
    /// </summary>
    public required double QualityScale { get; init; }

    /// <summary>
    /// Şutun çembere değme olasılığı. 06 §6'ya göre yalnız çembere değen miss
    /// sonrası hücum ribaundu hücum saatini 14 saniyeye çeker. Kalibre edilmemiş.
    /// </summary>
    public required double RimContactProbability { get; init; }

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
        QualityScale = 0.9,
        RimContactProbability = 0.70,
    };
}

/// <summary>
/// Bir aksiyonun hangi şut türüne gittiği ve beceriyi hangi attribute'ten okuduğu.
///
/// M4'te bu tip yalnız **kimlik** taşır: aksiyon, şut türü ve beceri okuma
/// kanalı. Ağırlık kaldırıldı çünkü 05 §5 dağılımın <b>taktiğe bağlı</b> olduğunu
/// söyler; tek bir düz vektör dört taktığı ifade edemez. Ağırlıklar artık
/// <see cref="TacticsModel"/> içindedir ve <c>OffensivePolicy</c> tarafından
/// normalize edilerek uygulanır.
///
/// <see cref="Skill"/> ham bir attribute okur. Oyuncu seçiminde kullanılan
/// ağırlık bu değil, <c>PlayerRatingCalculator</c>'ın bounded composite'idir
/// (D65); buradaki değer yalnız <b>isabet logitsine</b> girer.
/// </summary>
public sealed record ActionProfile
{
    public required OffensiveAction Action { get; init; }

    public required ShotType ShotType { get; init; }

    /// <summary>İsabet bölümünde okunacak ham beceri attribute'ü (0-100).</summary>
    public required Func<PlayerRatings, int> Skill { get; init; }

    /// <summary>
    /// Oyuncu seçiminde kullanılacak bounded composite. 05 §76 ham rating
    /// çarpanının yoğunlaştırma ürettiğini söyler; bu yüzden seçim ağırlığı
    /// composite üzerinden hesaplanır, bu alandan okunmaz.
    /// </summary>
    public required Func<PlayerRatings, int> SelectionComposite { get; init; }

    public static ImmutableArray<ActionProfile> Baseline { get; } =
    [
        new()
        {
            Action = OffensiveAction.PickAndRoll,
            ShotType = ShotType.MidRange,
            Skill = ratings => ratings.BallHandling,
            SelectionComposite = ratings => PlayerRatingTables.Handle(ratings),
        },
        new()
        {
            Action = OffensiveAction.Drive,
            ShotType = ShotType.ClosePost,
            Skill = ratings => ratings.Vertical,
            SelectionComposite = ratings => PlayerRatingTables.Athleticism(ratings),
        },
        new()
        {
            Action = OffensiveAction.SpotUp,
            ShotType = ShotType.ThreePoint,
            Skill = ratings => ratings.ThreePoint,
            SelectionComposite = ratings => PlayerRatingTables.Scoring(ratings),
        },
        new()
        {
            Action = OffensiveAction.Isolation,
            ShotType = ShotType.MidRange,
            Skill = ratings => ratings.BallHandling,
            SelectionComposite = ratings => PlayerRatingTables.Handle(ratings),
        },
        new()
        {
            Action = OffensiveAction.Cut,
            ShotType = ShotType.AtRim,
            Skill = ratings => ratings.OffBall,
            SelectionComposite = ratings => PlayerRatingTables.Cutting(ratings),
        },
        new()
        {
            Action = OffensiveAction.PostUp,
            ShotType = ShotType.ClosePost,
            Skill = ratings => ratings.PostOffense,
            SelectionComposite = ratings => PlayerRatingTables.Interior(ratings),
        },
        new()
        {
            // 04 sözlüğünde var, M4 dağılımında ağırlığı 0.00. Bileşik olarak
            // tutulur ama <c>OffensivePolicy</c> onu aday listesine almaz.
            Action = OffensiveAction.OffBallScreen,
            ShotType = ShotType.MidRange,
            Skill = ratings => ratings.OffBall,
            SelectionComposite = ratings => PlayerRatingTables.Cutting(ratings),
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

    /// <summary>M3'te eklendi: faul, bonus ve foul-out kuralları (D40).</summary>
    public required FoulModel Fouls { get; init; }

    /// <summary>M3'te eklendi: serbest atış isabet modeli.</summary>
    public required FreeThrowModel FreeThrows { get; init; }

    /// <summary>M4: dört hücum taktiğinin aksiyon dağılımları (D57/D62).</summary>
    public required TacticsModel Tactics { get; init; }

    /// <summary>M4: savunma policy katsayıları (D57).</summary>
    public required DefenseModel Defense { get; init; }

    /// <summary>M4: enerji/stamina modeli (D58, D63).</summary>
    public required FatigueModel Fatigue { get; init; }

    /// <summary>M4: tempo ayarları (D59).</summary>
    public required PaceModel Pace { get; init; }

    /// <summary>
    /// M4: seçim ağırlığı yayılımı (D65). Ham rating çarpanı yerine bounded
    /// aralık; 0.6'da taban 0.4, tepe 1.6.
    /// </summary>
    public required double SelectionSpread { get; init; }

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
        Fouls = FoulModel.Baseline,
        FreeThrows = FreeThrowModel.Baseline,
        Tactics = TacticsModel.Baseline,
        Defense = DefenseModel.Baseline,
        Fatigue = FatigueModel.Baseline,
        Pace = PaceModel.Baseline,
        SelectionSpread = 0.6,
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
        Append("offensiveReboundShotClockMs", Rules.OffensiveReboundShotClockMs);

        AppendReal("atRimBase", Shot.AtRimBase);
        AppendReal("closePostBase", Shot.ClosePostBase);
        AppendReal("midRangeBase", Shot.MidRangeBase);
        AppendReal("threePointBase", Shot.ThreePointBase);
        AppendReal("skillScale", Shot.SkillScale);
        AppendReal("qualityScale", Shot.QualityScale);
        AppendReal("rimContactProbability", Shot.RimContactProbability);

        Append("setupActionMs", Actions.SetupActionMs);
        Append("shotFlightMs", Actions.ShotFlightMs);
        AppendReal("shotCompletionProbability", Actions.ShotCompletionProbability);
        AppendReal("turnoverProbability", Actions.TurnoverProbability);
        AppendReal("offensiveReboundProbability", Actions.OffensiveReboundProbability);

        AppendReal("foulProbabilityPerAction", Fouls.FoulProbabilityPerAction);
        AppendReal("offensiveFoulShare", Fouls.OffensiveFoulShare);
        AppendReal("shootingFoulShare", Fouls.ShootingFoulShare);
        Append("bonusTeamFoulThreshold", Fouls.BonusTeamFoulThreshold);
        Append("bonusFreeThrowCount", Fouls.BonusFreeThrowCount);
        Append("personalFoulLimit", Fouls.PersonalFoulLimit);

        AppendReal("freeThrowBaseMakeProbability", FreeThrows.BaseMakeProbability);
        AppendReal("freeThrowSkillScale", FreeThrows.SkillScale);

        // M4: taktik, savunma, enerji ve tempo katsayilari. Dizi sirasi kanoniktir
        // ve enum degerlerinin sirasi degismemelidir.
        foreach (var profile in Tactics.Offensive.OrderBy(item => (int)item.Tactic))
        {
            Append($"tactic.{profile.Tactic}.shotBias", profile.ShotBias);
            Append($"tactic.{profile.Tactic}.qualityBonus", profile.QualityBonus);

            foreach (var weight in profile.Weights.OrderBy(item => (int)item.Action))
            {
                AppendReal($"tactic.{profile.Tactic}.weight.{weight.Action}", weight.Weight);
            }
        }

        AppendReal("defense.blockBase", Defense.BlockBase);
        AppendReal("defense.blockFromInteriorDefense", Defense.BlockFromInteriorDefense);
        AppendReal("defense.pressureBase", Defense.PressureBase);
        AppendReal("defense.pressureFromPerimeterDefense", Defense.PressureFromPerimeterDefense);
        AppendReal("defense.foulFromAggression", Defense.FoulFromAggression);

        Append("fatigue.startingEnergy", Fatigue.StartingEnergy);
        AppendReal("fatigue.baselineDrainPerSecond", Fatigue.BaselineDrainPerSecond);
        AppendReal("fatigue.baselineRecoveryPerSecond", Fatigue.BaselineRecoveryPerSecond);
        AppendReal("fatigue.breakRecoveryPerSecond", Fatigue.BreakRecoveryPerSecond);
        AppendReal("fatigue.fatigueLogitScale", Fatigue.FatigueLogitScale);

        foreach (var anchor in Fatigue.PerformanceCurve.OrderBy(item => item.Energy))
        {
            AppendReal($"fatigue.curve.{anchor.Energy}", anchor.Multiplier);
        }

        foreach (var tuning in Pace.Tunings.OrderBy(item => (int)item.Pace))
        {
            AppendReal($"pace.{tuning.Pace}.setup", tuning.SetupActionMultiplier);
            AppendReal($"pace.{tuning.Pace}.drain", tuning.EnergyDrainMultiplier);
        }

        AppendReal("selectionSpread", SelectionSpread);

        // M4: aksiyon agirligi artik profile degil, TacticsModel'e aittir (D62).
        // Hash yalnizca kimlik alanlarini yazar; agirligin da zaten yukarida
        // yazildigi icin burada tekrarlanmaz.
        foreach (var profile in ActionProfiles.OrderBy(item => (int)item.Action))
        {
            Append("action." + profile.Action, (int)profile.Action);
            Append("shotType." + profile.Action, (int)profile.ShotType);
        }

        Append("maxActionsPerMatch", MaxActionsPerMatch);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));

        return Convert.ToHexStringLower(digest)[..16];
    }
}
