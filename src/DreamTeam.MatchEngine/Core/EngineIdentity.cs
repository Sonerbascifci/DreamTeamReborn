namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Bir maçın sonucunu belirleyen tüm sürüm ve config kimliği.
///
/// Seed tek başına maçı tanımlamaz. Aynı seed'in farklı engine, rules, balance
/// config veya roster snapshot'ı ile oynanması farklı sonuç verir. Replay ve
/// hata yeniden üretimi için bu kimlik setup'a yazılır ve raporda gösterilir.
/// </summary>
public sealed record EngineIdentity
{
    public required string EngineVersion { get; init; }

    public required string RulesVersion { get; init; }

    /// <summary>Denge (balance) config dosyasının içerik hash'i.</summary>
    public required string BalanceConfigHash { get; init; }

    public required string RngAlgorithm { get; init; }

    public required string RngVersion { get; init; }
}
