using DreamTeam.MatchEngine.Config;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Bırakılmış şut: şut sahadan ayrıldı, sonucu henüz çözülmedi.
///
/// M2'de şut senkron çözülürdü. M3'te faul serisi, blok ve düdük sonrası
/// çözüm gerektiği için şut iki <c>Advance</c> adımına bölünür. Bu tip o
/// ara durumu taşır ve M5'te serileştirilecektir (replay için zorunlu).
///
/// Kurallar:
/// - <see cref="RimContact"/> şut bırakıldığı anda karara bağlanır; 06 §6'ya göre
///   hücum ribaundu reset'ini belirler.
/// - Serbest atış sayısı <b>bu tipte tutulmaz</b>: and-one sonucu şutun isabetine
///   bağlıdır, yani ancak settlement anında bilinir. Sayım
///   <c>FoulResolver.FreeThrowCountFor</c> tarafından orada yapılır.
/// - Kaçan shooting foul'da FGA <b>sayılmaz</b> (06 §87); bu karar
///   <c>CountsAsFieldGoalAttempt</c> bayrağıyla settlement'ta verilir.
/// </summary>
public sealed record PendingShot
{
    public required long ActionId { get; init; }

    public required long ShotId { get; init; }

    /// <summary>Bu şutla birlikte çalınan faul; yoksa 0.</summary>
    public required long FoulId { get; init; }

    public required FoulType FoulType { get; init; }

    public required Guid ShooterId { get; init; }

    public required ShotType ShotType { get; init; }

    public required int SkillRating { get; init; }

    public required bool RimContact { get; init; }
}
