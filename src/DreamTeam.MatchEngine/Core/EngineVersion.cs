namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Motorun kendi sürümü. Setup snapshot'ına yazılır. Assembly bilgi sürümü
/// bilerek kullanılmaz: build metadata'sı hash içerdiğinden aynı kaynak aynı
/// kimliği üretmez ve setup digest'i kararsızlaşır.
/// </summary>
public static class EngineVersion
{
    public const string Current = "0.1.0";
}
