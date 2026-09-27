using DreamTeam.MatchEngine.Config;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Bir maçın değişmez girdi snapshot'ı. Oluşturulduktan sonra alanları
/// değiştirilemez; koleksiyon alanları dışarıdan gelen listelerin kopyasıdır.
///
/// <para><b>M4'te bu kayıt daraldı.</b> 04 §25'teki <c>TeamMatchSetup</c> tipi
/// materyalize edildi: takım, lineup, hücum taktiği, savunma policy'si ve tempo
/// artık tek bir <see cref="TeamMatchSetup"/> içinde taşınır (D61). Düz
/// <c>HomeLineup</c>/<c>AwayLineup</c> alanları kaldırıldı.</para>
///
/// <para>Gerekçe düz alanlarla devam etmenin M5'te <c>MatchSetup</c> ile
/// <c>TeamMatchState</c> arasında alan paralelliği yaratacağı ve hangisinin
/// yetkili olduğunu belirsizleştireceğidir. 04'ün sözlüğü zaten
/// <c>TeamMatchSetup</c> diyordu; M4, taktiklerin geldiği milestone'dur.</para>
/// </summary>
public sealed record MatchSetup
{
    public required Guid MatchId { get; init; }

    public required TeamMatchSetup Home { get; init; }

    public required TeamMatchSetup Away { get; init; }

    public required ulong Seed { get; init; }

    public required EngineIdentity Engine { get; init; }
}
