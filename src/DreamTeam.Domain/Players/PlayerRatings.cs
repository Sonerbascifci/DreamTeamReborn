namespace DreamTeam.Domain.Players;

/// <summary>
/// Bir oyuncunun 18 temel attribute değeri. Tüm değerler 0-100 aralığındadır ve
/// değişmez (immutable) bir snapshot'tır. Aralık doğrulaması tek kapıda,
/// <c>MatchSetupValidator</c> içinde yapılır; bu tip yalnızca veri taşır.
///
/// <c>Stamina</c> bir dayanıklılık kapasitesidir. Maç içindeki kalan enerji
/// <c>Energy</c> olarak adlandırılır ve state'te tutulur; ikisi aynı kavram değildir.
///
/// <c>OffensiveIQ</c> ayrı bir attribute değildir; karar kalitesi <c>BasketballIQ</c> olarak adlandırılır.
/// </summary>
public sealed record PlayerRatings
{
    public required int Speed { get; init; }

    public required int Strength { get; init; }

    public required int Vertical { get; init; }

    public required int Stamina { get; init; }

    public required int Inside { get; init; }

    public required int MidRange { get; init; }

    public required int ThreePoint { get; init; }

    public required int FreeThrow { get; init; }

    public required int BallHandling { get; init; }

    public required int Passing { get; init; }

    public required int OffBall { get; init; }

    public required int PostOffense { get; init; }

    public required int PerimeterDefense { get; init; }

    public required int InteriorDefense { get; init; }

    public required int Steal { get; init; }

    public required int Block { get; init; }

    public required int Rebounding { get; init; }

    public required int BasketballIQ { get; init; }
}
