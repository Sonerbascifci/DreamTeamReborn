using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Actions;

public sealed record FoulOutcome(bool IsFoul, FoulType Type)
{
    public static FoulOutcome None { get; } = new(false, FoulType.NonShooting);
}

/// <summary>
/// Bir aksiyonda faul olup olmadığını ve türünü çözer (M3).
///
/// Kritik kural (05 §127): bu, <b>şuttan bağımsız bir çekiliş değildir</b>.
/// Faul kararı ve şut kararı aynı aksiyon ağacının dallarıdır; motor onları
/// ayrı ayrı örnekleyip çelişkili sonuç üretmez. Çağrı sırası:
///
/// <code>
/// 1. top kaybı çekilişi         (M2'den)
/// 2. faul olma çekilişi         bu sınıf
/// 3. hücum faulü payı çekilişi  yalnız faul varsa
/// 4. shooting faulü payı çekilişi yalnız faul ve şut varsa
/// </code>
///
/// Bonus yalnız <b>savunma</b> faullerinde sayılır. Hücum faulü hem top kaybı
/// üretir hem de hiçbir koşulda bonus tetiklemez (06 §88).
/// </summary>
public sealed class FoulResolver
{
    private readonly FoulModel _model;

    public FoulResolver(FoulModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    /// <summary>
    /// M4: savunma disiplininin eklediği faul payı çekilişe uygulanır.
    /// <paramref name="disciplinePressure"/> D57'deki dört policy'den biridir; paket
    /// içindeki agresif olanlar daha çok faul üretir.
    ///
    /// <para>Tavan <b>1.0</b>'dır, keyfî bir alt sınır değil. Bir olasılık zaten
    /// [0,1] aralığındadır ve test fixture'ları "her aksiyonda faul" gibi
    /// kuvvetlendirme senaryolarını 1.0 ile kurar; 0.95 gibi bir tavan bunları
    /// sessizce zayıflatır ve M4'te gerçek bir hataya yol açmıştı.</para>
    /// </summary>
    public FoulOutcome Occurred(IRandomSource random, double disciplinePressure)
    {
        var probability = Math.Clamp(_model.FoulProbabilityPerAction + disciplinePressure, 0.0, 1.0);

        return random.NextDouble() < probability
            ? new FoulOutcome(true, FoulType.NonShooting)
            : FoulOutcome.None;
    }

    /// <summary>M3 sözleşmesi: savunma disiplini etkisi yok.</summary>
    public FoulOutcome Occurred(IRandomSource random) => Occurred(random, 0.0);

    /// <summary>Faul hücum faulü mü? Yalnız faul varsa çağrılır.</summary>
    public bool IsOffensive(IRandomSource random) =>
        random.NextDouble() < _model.OffensiveFoulShare;

    /// <summary>Savunma faulü shooting mi? Yalnız faul ve şut varsa çağrılır.</summary>
    public bool IsShooting(IRandomSource random) =>
        random.NextDouble() < _model.ShootingFoulShare;

    /// <summary>
    /// Takım bonusunda mı? D40: periyot içinde 5. sayılan savunma faulünden
    /// itibaren. Eşik "sonraki faul bonusa girer" anlamında: 4 faulde henüz yok.
    /// </summary>
    public bool IsInBonus(int teamFoulsThisPeriod) =>
        teamFoulsThisPeriod + 1 >= _model.BonusTeamFoulThreshold;

    /// <summary>
    /// Faulün doğurduğu serbest atış sayısı (06 §87, §89).
    ///
    /// - Hücum faulü: 0 (ve tek top kaybı)
    /// - Shooting faul + isabet: 1 (and-one)
    /// - Shooting faul + kaçırma: 2, üçlük denemede 3
    /// - Non-shooting faul + isabetli şut: 1 (and-one davranışı; bonus bunu değiştirmez)
    /// - Non-shooting faul + kaçan şut: bonusta 2, bonus yoksa 0
    /// - Non-shooting faul + şut yok: bonusta 2, bonus yoksa 0
    ///
    /// Sayım settlement anında yapılır çünkü and-one şutun isabetine bağlıdır.
    /// </summary>
    public int FreeThrowCountFor(
        FoulType foulType,
        bool bonusActive,
        bool shotAttempted,
        bool shotMade,
        ShotType shotType)
    {
        if (foulType == FoulType.Offensive)
        {
            return 0;
        }

        if (foulType == FoulType.Shooting)
        {
            if (!shotAttempted)
            {
                return 0;
            }

            if (shotMade)
            {
                return 1;
            }

            return shotType == ShotType.ThreePoint ? 3 : 2;
        }

        // Non-shooting savunma faulü
        if (shotAttempted && shotMade)
        {
            return 1;
        }

        return bonusActive ? _model.BonusFreeThrowCount : 0;
    }
}
