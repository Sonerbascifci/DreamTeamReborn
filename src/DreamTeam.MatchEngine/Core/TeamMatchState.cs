using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Config;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Takımın maç içi durumu. 04_DOMAIN_AND_DATA_MODEL.md'in <c>TeamMatchState</c>
/// kaydının M4'e kadar olan alt kümesi.
///
/// <para><b>Sıra kanoniktir.</b> <see cref="Roster"/> ve <see cref="PlayerStates"/>
/// kadro sırasına göre dizilidir; aday listeleri bu sırayla gezilir, böylece
/// sonuç giriş sırasına bağlı olmaz. <see cref="PlayerStates"/> araması
/// <see cref="StateFor"/> ile yapılır ve <c>ImmutableArray</c> indeksleme
/// kullanılır — hash set'in yineleme sırasına bağımlı bir sonuç üretmesi
/// determinizm riskidir.</para>
///
/// <para><b>04 §28'in "bir oyuncu aynı anda bench ve sahada olmaz" kuralı</b>
/// M4'te uygulanmaya başlar: <see cref="OnCourt"/> her zaman
/// <see cref="PlayerStates"/>'in bir alt kümesidir ve foul-out yedeklemesi
/// ikisini birlikte günceller.</para>
///
/// <para><b>Taktik ve tempo burada da tutulur</b> (D61 gerekçesi). M4'te bunlar
/// <see cref="MatchSetup"/>'tan gelir ve maç boyunca değişmez; canlı taktik
/// değişimi M5'te bu alanları güncelleyecek. Aynı alanın iki kaynakta
/// (setup ve state) tutulması hangisinin yetkili olduğunu belirsizleştirirdi.</para>
///
/// <para><b>Bench henüz ayrı bir liste değildir.</b> 04 §28'de <c>Bench</c> ayrı bir
/// alan olarak sayılır; M3'te yedekler "sahada olmayan ve foul-out olmayan"
/// olarak türetiliyordu. Ayrı liste M5'in substitution işidir ve bu milestone'da
/// eklenmez; <see cref="Bench"/> türetilmiş bir erişimcidir.</para>
/// </summary>
public sealed record TeamMatchState
{
    public required TeamSide Side { get; init; }

    public required Team Team { get; init; }

    /// <summary>M4: bu maç için seçilen hücum taktiği.</summary>
    public required OffensiveTactic OffensiveTactic { get; init; }

    /// <summary>M4: bu maç için seçilen savunma policy'si.</summary>
    public required DefensiveTactic DefensiveTactic { get; init; }

    /// <summary>M4: bu maç için seçilen tempo.</summary>
    public required Pace Pace { get; init; }

    /// <summary>Kanonik sıralanmış kadro.</summary>
    public required ImmutableArray<Player> Roster { get; init; }

    /// <summary>Sahadaki beş, lineup slot sırasında.</summary>
    public required ImmutableArray<Player> OnCourt { get; init; }

    /// <summary>
    /// M4: kadro sırasına birebir eşlenen oyuncu durumları. <c>PlayerStates</c>
    /// içindeki <c>PlayerId</c> sırası <see cref="Roster"/> ile aynıdır.
    /// </summary>
    public required ImmutableArray<PlayerMatchState> PlayerStates { get; init; }

    /// <summary>M3: faul sayaçları.</summary>
    public required FoulCounters Fouls { get; init; }

    /// <summary>
    /// M3: faulden çıkmış oyuncular. Sahada kalamaz ve yedek seçiminde elenir.
    /// Sıra kararlıdır (çıkış sırası), yalnız üyelik sorgusu yapılır.
    /// </summary>
    public required ImmutableArray<Guid> FoulOutPlayerIds { get; init; }

    /// <summary>
    /// M5 (D84): kullanılmış **tam** timeout sayısı. Kümülatiftir; uzatma bonusu
    /// bütçeyi büyütür, kullanılmış sayıyı silmez.
    ///
    /// <para><b>Ayrı alan, <c>FoulCounters</c>'ın parçası DEĞİL.</b> D55'in
    /// "roster sınırı" disiplini: her sayaç tek bir soruyu yanıtlar. Faul
    /// sayacı periyotta sıfırlanır, timeout sayacı maç boyunca birikir.</para>
    /// </summary>
    public int FullTimeoutsUsed { get; init; }

    /// <summary>M5 (D83, D89): kullanılmış 20 saniyelik timeout sayısı.</summary>
    public int ShortTimeoutsUsed { get; init; }

    /// <summary>
    /// Sahada olmayan ve foul-out olmayan oyuncular: yedek havuzu (M5'in
    /// değişiklik adayları). Türetilmiştir; ayrı bir <c>Bench</c> alanı tutulmaz.
    /// </summary>
    public ImmutableArray<Player> Bench
    {
        get
        {
            var onCourt = new HashSet<Guid>();

            foreach (var player in OnCourt)
            {
                onCourt.Add(player.Id);
            }

            return [.. Roster.Where(player => !onCourt.Contains(player.Id))];
        }
    }

    public TeamMatchState WithOnCourt(ImmutableArray<Player> onCourt) => this with { OnCourt = onCourt };

    public TeamMatchState WithFouls(FoulCounters fouls) => this with { Fouls = fouls };

    public TeamMatchState WithPlayerStates(ImmutableArray<PlayerMatchState> states) =>
        this with { PlayerStates = states };

    public TeamMatchState MarkFoulOut(Guid playerId) =>
        this with
        {
            FoulOutPlayerIds = FoulOutPlayerIds.Contains(playerId)
                ? FoulOutPlayerIds
                : [.. FoulOutPlayerIds, playerId],
        };

    /// <summary>Bir oyuncunun maç içi durumu. Kadro dışıysa <c>null</c>.</summary>
    public PlayerMatchState? StateFor(Guid playerId)
    {
        foreach (var state in PlayerStates)
        {
            if (state.PlayerId == playerId)
            {
                return state;
            }
        }

        return null;
    }

    /// <summary>Varsayılan olarak <see cref="FatigueModel.StartingEnergy"/> ile başlar.</summary>
    public static ImmutableArray<PlayerMatchState> InitialStates(
        ImmutableArray<Player> roster,
        FatigueModel fatigue) =>
    [
        .. roster.Select(player => new PlayerMatchState
        {
            PlayerId = player.Id,
            Energy = fatigue.StartingEnergy,
            SecondsOnCourt = 0,
        })
    ];
}
