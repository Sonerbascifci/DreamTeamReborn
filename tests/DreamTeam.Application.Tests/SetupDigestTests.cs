using System.Globalization;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Setup;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.Application.Tests;

/// <summary>
/// M7, D87: <c>MatchSetup</c> icin kalici, kanonik digest.
///
/// <para><b>D87'in icinden gecmek.</b> Olcum suydu: <c>ImmutableArray&lt;T&gt;</c>
/// <c>record</c> icinde referans esitligiyle karsilastirildigi icin
/// <c>record.Equals</c> ayni kadroyu farkli sirada girdigimizde FARKLI
/// sonuc veriyor. Bu test, digest'in bunu YAPMADIGINI ve bir de
/// <c>record.Equals</c>'in YAPTIGINI ayni yerde gosterir. Boylece "sira
/// bagimsiz" iddiasinin arkasinda yalniz bir yorum degil, <b>olcum</b> vardir.</para>
/// </summary>
public class SetupDigestTests
{
    private static readonly SetupDigest Digest = new();

    [Fact]
    public void TheSameSetupProducesTheSameDigest()
    {
        Assert.Equal(Digest.Of(M7TestData.Mirror(1)), Digest.Of(M7TestData.Mirror(1)));
    }

    [Fact]
    public void TheDigestIsASha256Hex()
    {
        var digest = Digest.Of(M7TestData.Mirror(1));

        Assert.Equal(64, digest.Length);
        Assert.All(digest, c => Assert.True(Uri.IsHexDigit(c), $"'{c}' hex degil."));
    }

    [Fact]
    public void ADifferentSeedProducesADifferentDigest()
    {
        // Ayni kadro, farkli seed. Seed motorun girdisidir; setup degismis
        // sayilir.
        Assert.NotEqual(Digest.Of(M7TestData.Mirror(1)), Digest.Of(M7TestData.Mirror(2)));
    }

    [Fact]
    public void RosterOrderDoesNotAffectTheDigest()
    {
        // KANONIK SIRALAMA. Ayni 10 oyuncu, iki farkli sirada.
        var forward = M7TestData.Mirror(1);
        var reversed = forward with
        {
            Home = forward.Home with
            {
                Team = forward.Home.Team with { Roster = [.. forward.Home.Team.Roster.Reverse()] },
            },
        };

        Assert.Equal(Digest.Of(forward), Digest.Of(reversed));

        // AYNI AYRI: bozuk record esitligi bunu YAPAMAZ. D87'in olcumu.
        Assert.NotEqual(forward, reversed);
        Assert.False(forward.Equals(reversed));
    }

    [Fact]
    public void AnyRatingChangeAltersTheDigest()
    {
        // 18 alanin HER BIRI icin ayri ayri denendi. Bu, "rating degisirse
        // digest degisir" iddiasinin tam kanitidir: tek bir alanla degistirip
        // kalan 17'sini ayni birakiyoruz.
        var baseSetup = M7TestData.Mirror(1);
        var baseDigest = Digest.Of(baseSetup);
        var baseRoster = baseSetup.Home.Team.Roster;
        var first = baseRoster[0];
        var baseRatings = first.Ratings;

        var fields = new (string Name, Func<PlayerRatings, PlayerRatings> Change)[]
        {
            ("Speed", r => r with { Speed = r.Speed + 1 }),
            ("Strength", r => r with { Strength = r.Strength + 1 }),
            ("Vertical", r => r with { Vertical = r.Vertical + 1 }),
            ("Stamina", r => r with { Stamina = r.Stamina + 1 }),
            ("Inside", r => r with { Inside = r.Inside + 1 }),
            ("MidRange", r => r with { MidRange = r.MidRange + 1 }),
            ("ThreePoint", r => r with { ThreePoint = r.ThreePoint + 1 }),
            ("FreeThrow", r => r with { FreeThrow = r.FreeThrow + 1 }),
            ("BallHandling", r => r with { BallHandling = r.BallHandling + 1 }),
            ("Passing", r => r with { Passing = r.Passing + 1 }),
            ("OffBall", r => r with { OffBall = r.OffBall + 1 }),
            ("PostOffense", r => r with { PostOffense = r.PostOffense + 1 }),
            ("PerimeterDefense", r => r with { PerimeterDefense = r.PerimeterDefense + 1 }),
            ("InteriorDefense", r => r with { InteriorDefense = r.InteriorDefense + 1 }),
            ("Steal", r => r with { Steal = r.Steal + 1 }),
            ("Block", r => r with { Block = r.Block + 1 }),
            ("Rebounding", r => r with { Rebounding = r.Rebounding + 1 }),
            ("BasketballIQ", r => r with { BasketballIQ = r.BasketballIQ + 1 }),
        };

        foreach (var (name, change) in fields)
        {
            var mutated = baseSetup with
            {
                Home = baseSetup.Home with
                {
                    Team = baseSetup.Home.Team with
                    {
                        Roster = [.. baseRoster.Select(p =>
                            p.Id == first.Id ? p with { Ratings = change(p.Ratings) } : p)],
                    },
                },
            };

            Assert.NotEqual(baseDigest, Digest.Of(mutated));

            // Degisikligin gerceklestigini dogrula: test, hicbir seyin
            // degismedigi bir durumda da yesil donmesin.
            Assert.NotEqual(baseRatings, change(baseRatings));
        }
    }

    [Fact]
    public void APlayerSwapAltersTheDigest()
    {
        // Lineup degisikligi setup degisikligidir. 04: "Snapshot history'nin
        // canli sorgusundan bagimsizdir"; lineup degisse o mac degismez.
        var setup = M7TestData.Mirror(1);

        var swapped = setup with
        {
            Home = setup.Home with
            {
                Lineup = new Lineup
                {
                    PlayerIds = [.. setup.Home.Lineup.PlayerIds.Reverse()],
                },
            },
        };

        Assert.NotEqual(Digest.Of(setup), Digest.Of(swapped));

        // Cevirme gercekten lineup'i degistirdi.
        Assert.NotEqual(setup.Home.Lineup.PlayerIds, swapped.Home.Lineup.PlayerIds);
    }

    [Fact]
    public void TacticsAffectTheDigest()
    {
        var setup = M7TestData.Mirror(1);
        var faster = setup with
        {
            Home = setup.Home.WithTactics(OffensiveTactic.Balanced, DefensiveTactic.ManToMan, Pace.Fast),
        };

        Assert.NotEqual(Digest.Of(setup), Digest.Of(faster));
    }

    [Fact]
    public void TheDigestDoesNotDependOnTheAmbientCulture()
    {
        // ORTAMIN KISITI: proje globalization-INVARIANT mode ile calisiyor, bu
        // yuzden "tr-TR" gibi bir kultur OLUSTURULAMAZ. Bunu gizlemek yerine
        // klonlanmis bir invariant kulturun sayi bicimini DEGISTIREREK ayni
        // seyi olcuyoruz: ondalik ayirac "," ve binlik ayirac "." yapan bir
        // kultur, uretim kodunun CurrentCulture kullandigi durumda ciktiyi
        // degistirirdi.
        //
        // <b>Simdilik dolayli risk.</b> 18 rating alaninin hepsi <c>int</c>
        // oldugu icin sayi bicimi zaten gozlenemez; bu test bugun <c>double</c>
        // alan eklendiginde veya <c>ToString()</c> kultur duyarli cagrildiginda
        // KIRMIZIYA doner. Yani su an kanitladigi sey "kod invariant yaziyor",
        // degil "ortam farki sonuc degistirmiyor".
        var original = CultureInfo.CurrentCulture;
        var hostile = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        string withHostileCulture;

        hostile.NumberFormat.NumberDecimalSeparator = ",";
        hostile.NumberFormat.NumberGroupSeparator = ".";
        hostile.NumberFormat.CurrencyDecimalSeparator = ",";

        try
        {
            CultureInfo.CurrentCulture = hostile;
            CultureInfo.CurrentUICulture = hostile;

            withHostileCulture = Digest.Of(M7TestData.Mirror(1));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
            CultureInfo.CurrentUICulture = original;
        }

        var withAmbientCulture = Digest.Of(M7TestData.Mirror(1));

        Assert.Equal(withAmbientCulture, withHostileCulture);

        // Klon gercekten farkli: test, hicbir sey degistirmedigi bir kulturle
        // yesil donmesin. Ondalikli bir sayiyla kanitlanir; 18 rating alani
        // int oldugu icin tam sayi bicimi gozlenemez.
        Assert.Equal("74.5", (74.5).ToString(CultureInfo.InvariantCulture));
        Assert.Equal("74,5", (74.5).ToString(hostile));
    }

    [Fact]
    public void ADifferentEngineVersionAltersTheDigest()
    {
        var setup = M7TestData.Mirror(1);
        var other = setup with
        {
            Engine = setup.Engine with { EngineVersion = "engine-v9.9.9" },
        };

        Assert.NotEqual(Digest.Of(setup), Digest.Of(other));
    }

    [Fact]
    public void ADifferentRulesVersionAltersTheDigest()
    {
        var setup = M7TestData.Mirror(1);
        var other = setup with
        {
            Engine = setup.Engine with { RulesVersion = "rules-v9.9.9" },
        };

        Assert.NotEqual(Digest.Of(setup), Digest.Of(other));
    }
}
