using DreamTeam.MatchEngine.Config;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Bırakılmış şut: şut sahadan ayrıldı, sonucu henüz çözülmedi.
///
/// <para>M2'de şut senkron çözülürdü. M3'te faul serisi, blok ve düdük sonrası
/// çözüm gerektiği için şut iki <c>Advance</c> adımına bölünür. M4'te buna
/// <b>kalite</b> ve <b>shooter enerjisi</b> de eklenir (D67): politika etkisi
/// event'ten <b>gözlenebilir</b> olmalıdır, aksi halde M4'ün kabul kriteri
/// ("controlled policy değişimi beklenen karışımı etkiliyor") test edilemez.</para>
///
/// <para>Kurallar:</para>
/// <list type="bullet">
///   <item><description><c>RimContact</c> bırakma anında karara bağlanır (06 §6 reset tablosu).</description></item>
///   <item><description>Serbest atış sayısı <b>bu tipte tutulmaz</b>: and-one şutun isabetine bağlıdır, ancak settlement anında bilinir (D48).</description></item>
///   <item><description>Kaçan shooting foul'da FGA <b>sayılmaz</b> (06 §87).</description></item>
/// </list>
///
/// <para>Bu tip M5'te serileştirilecektir; alanlar primitive ve <c>Guid</c>'dir.</para>
/// </summary>
public sealed record PendingShot
{
    public required long ActionId { get; init; }

    public required long ShotId { get; init; }

    /// <summary>Bu şutla birlikte çalınan faul; yoksa 0.</summary>
    public required long FoulId { get; init; }

    public required FoulType FoulType { get; init; }

    public required Guid ShooterId { get; init; }

    /// <summary>M4 (D68): bu aksiyonun primer savunmacısı. Blok, faul ve kalite aynı kişiyi kullanır.</summary>
    public required Guid PrimaryDefenderId { get; init; }

    public required ShotType ShotType { get; init; }

    public required int SkillRating { get; init; }

    /// <summary>M4: bırakma anında hesaplanmış şut kalitesi, 0-100.</summary>
    public required int Quality { get; init; }

    /// <summary>M4: şutun bırakıldığı andaki atakçının enerjisi, 0-100.</summary>
    public required int ShooterEnergy { get; init; }

    public required bool RimContact { get; init; }
}
