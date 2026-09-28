namespace DreamTeam.MatchEngine.Config;

/// <summary>
/// M7 (D115): kural surumunun <b>tek</b> kaynagi.
///
/// <para><b>Neden ayri dosya?</b> M6'da bu deger
/// <c>FixtureCatalog.RulesVersion</c> idi, yani Simulator'da. M7'de sunucu da
/// ayni surumu yazmak zorunda kaldi; deger iki yere kopyalaninca "simulator
/// hangi kuralla simule etti, sunucu hangisiyle?" sorusu cikarir ve
/// karsilastirilamaz kayitlar dogar. Motorun <see cref="EngineIdentity"/> tipi
/// dondurulmus bir sozlesmedir (M6'da dondurulan sekiz dosyadan biri), ona
/// <i> ozellik eklemeden yeni bir tip acmak dondurma listesini bozmaz.</para>
///
/// <para><b>Surum degisince BURASI degisir.</b> 06 §"kurallar surumu her mac
/// kaydinda saklanir"; mac kaydinin <c>rules_version</c> sutunu bu degerden
/// gelir. Iki farkli surumun maclari ayni tabloda yan yana duracaktir; hangi
/// olduklari hesaplanabilmelidir.</para>
///
/// <para><b>Bu bir uygulama sabiti degil.</b> Motor surumu
/// (<see cref="Core.EngineVersion"/>) kodda degisen bir sey; kural surumu degisen
/// bir <i>kural</i>. Ikisi ayni numarayi tasimaz.</para>
/// </summary>
public static class RulesIdentity
{
    /// <summary>M5'te kurulan, M6'da kalibre edilen, M7'de dondurulan kural surumu.</summary>
    public const string Current = "rules-v0.2-simple-nba";
}
