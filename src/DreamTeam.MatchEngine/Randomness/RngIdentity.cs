namespace DreamTeam.MatchEngine.Randomness;

/// <summary>
/// RNG algoritmasının ve sürümünün tek doğru kaynağı. <c>EngineIdentity</c> bu
/// değerleri taşır; setup ile üretim RNG'si arasındaki uyumsuzluk
/// <c>MatchSetupValidator</c> tarafından reddedilir.
/// </summary>
public static class RngIdentity
{
    public const string Algorithm = "SplitMix64";

    public const string Version = "1";
}
