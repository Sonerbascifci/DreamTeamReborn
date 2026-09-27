namespace DreamTeam.MatchEngine.Core;

/// <summary>Maçta iki tarafı temsil eden sabit etiket.</summary>
public enum TeamSide
{
    Home = 0,
    Away = 1,
}

public static class TeamSideExtensions
{
    /// <summary>Rakip taraf. Iki taraflı maç modelinde tek anlamlıdır.</summary>
    public static TeamSide Opponent(this TeamSide side) =>
        side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
}
