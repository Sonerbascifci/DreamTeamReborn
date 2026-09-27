using System.Collections.Immutable;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Commands;

/// <summary>
/// Uygulanamayan komutlari tasiran kuyruk. <c>MatchState</c>'in bir alanidir.
///
/// <para><b>Determinism sozlesmesi.</b> Kuyruk:
/// <list type="number">
///   <item><description><b>FIFO</b>: <see cref="ScheduledManagerCommand.AcceptedOrder"/>'a gore siralanir (D86).</description></item>
///   <item><description>Siralama <b>ToplamOrder</b> (stable) kullanir: ayni sirali iki komut kayit sirasiyla kalir, siralamasi kararsizlasmaz.</description></item>
///   <item><description>Sira <b>asla</b> degismez; kuyruga ekleme sonraki bosaltmalari da degistirmez.</description></item>
///   <item><description>Hiçbir islem <c>HashSet</c>/<c>ImmutableDictionary</c> yineleme sirasina bagli degildir.</description></item>
/// </list></para>
///
/// <para><b>Neden kuyruk state'te?</b> Uygulanamayan bir komut bir sonraki
/// mantiksal sinira kadar korunur. Kuyruk state'in parcasi olmazsa (ornegin
/// yalniz <c>Advance</c> parametresi olursa) kaybolur; bu da replay'i
/// bozuk yapar. 07 §5 "kabul/uygulama sirasi kaydi" der.</para>
/// </summary>
public sealed record CommandQueue
{
    public static CommandQueue Empty { get; } = new()
    {
        Pending = [],
        NextOrder = 0,
        Settled = [],
    };

    /// <summary>
    /// Sirali bekleyen komutlar. <b>Degismez</b>; her islem yeni bir ornek
    /// dondurur, boylece <c>MatchState</c> degismezlik ilkesi bozulmaz.
    /// </summary>
    public required ImmutableArray<ScheduledManagerCommand> Pending { get; init; }

    /// <summary>Motorun attigi siradaki deger. Sonraki <c>AcceptedOrder</c> budur.</summary>
    public required long NextOrder { get; init; }

    /// <summary>
    /// Zaten uygulanmis (veya reddedilmis) <c>CommandId</c>'ler. Idempotency
    /// icin gereklidir (07 §5, T14). Kuyrukta degil, kalici olarak tutulur.
    /// </summary>
    public required ImmutableHashSet<Guid> Settled { get; init; }

    public bool IsEmpty => Pending.IsEmpty;

    public int PendingCount => Pending.Length;

    /// <summary>
    /// Yeni komutlari kabul eder ve numaralandirir. <b>Numaralandirma burada
    /// olur</b>, cunku <c>AcceptedOrder</c>'i motor atar (07 §5) ve siranin
    /// kaynak disindan gelmemesi gerekir.
    /// </summary>
    /// <param name="incoming">
    /// Aday komutlar. Zaten <c>AcceptedOrder</c> tasimazlar; atanir.
    /// </param>
    /// <param name="nowSequence">Mevcut <c>NextSequence</c>; stale kontrolu icin.</param>
    public (CommandQueue Queue, ImmutableArray<CommandResult> Results) Accept(
        ImmutableArray<ScheduledManagerCommand> incoming,
        long nowSequence)
    {
        var queue = this;
        var results = ImmutableArray.CreateBuilder<CommandResult>();
        var pending = Pending.ToBuilder();

        foreach (var candidate in incoming)
        {
            if (queue.Settled.Contains(candidate.CommandId))
            {
                // T14: duplicate komut etkiyi tekrarlamaz. Ayrica ikinci bir
                // "applied" sonuc da uretilmez; ayni sonuc doner.
                results.Add(CommandResult.Rejected1(
                    candidate,
                    CommandRejectionReason.DuplicateCommand,
                    nowSequence,
                    "Bu CommandId daha once islendi."));
                continue;
            }

            if (candidate.ExpectedSequence is { } expected && expected < nowSequence)
            {
                // Gecmis bir gorulen state'e gore yapilmis istek: reddet.
                queue = queue with
                {
                    Settled = queue.Settled.Add(candidate.CommandId),
                };

                results.Add(CommandResult.Rejected1(
                    candidate,
                    CommandRejectionReason.StaleSequence,
                    nowSequence,
                    $"ExpectedSequence={expected} < mevcut {nowSequence}."));
                continue;
            }

            var accepted = candidate.WithOrder(queue.NextOrder);

            queue = queue with
            {
                NextOrder = queue.NextOrder + 1,
            };

            pending.Add(accepted);
        }

        return (queue with { Pending = Sort(pending.ToImmutable()) }, results.ToImmutable());
    }

    /// <summary>
    /// Zarf dogrulamasinda basarisiz olan bir komutu kalici olarak isaretler.
    /// Boylece tekrar gonderilirse yeniden denenmez (T14) ve kuyruga hic girmez.
    /// </summary>
    public CommandQueue MarkSettled(Guid commandId) => this with
    {
        Settled = Settled.Add(commandId),
    };

    /// <summary>
    /// Verilen sinira gelen komutlari, sinira uygun olanlari dondurur ve
    /// kuyruktan cikarir. Siralama korunur.
    /// </summary>
    public (CommandQueue Queue, ImmutableArray<ScheduledManagerCommand> AtBoundary) TakeAt(
        CommandBoundary boundary)
    {
        var remaining = ImmutableArray.CreateBuilder<ScheduledManagerCommand>();
        var atBoundary = ImmutableArray.CreateBuilder<ScheduledManagerCommand>();

        foreach (var command in Pending)
        {
            if (command.TargetBoundary == boundary)
            {
                atBoundary.Add(command);
            }
            else
            {
                remaining.Add(command);
            }
        }

        return (this with { Pending = remaining.ToImmutable() }, atBoundary.ToImmutable());
    }

    /// <summary>
    /// Komutlari uygulandiktan/redd edildikten sonra kuyrugu kapatir ve
    /// <c>CommandId</c>'leri kalici olarak isaretler. 07 §5: "Ayni CommandId
    /// yeniden gelirse ayni sonuc doner."
    ///
    /// <para><b>D93: verilen komutlar KUYRUKTAN CIKARILIR.</b> Onceki surum
    /// yalniz <c>Settled</c> isaretini guncelliyordu; boylece bir komut hem
    /// "islendi" sayilip hem de kuyrukta kaliyordu. <c>TakeAt</c> cagrisi
    /// kuyrugu zaten bosaltiyor, ama bu yontem <b>dogrudan</b> cagrildiginda
    /// (testler, gelecekteki replay araci) kuyruk kirli kaliyordu ve ayni
    /// komut ikinci kez uygulanabiliyordu. Iki kayit da tutarli olmalidir:
    /// "islendi" demek "kuyrukta degil" demektir.</para>
    /// </summary>
    public CommandQueue Settle(ImmutableArray<ScheduledManagerCommand> processed)
    {
        // D91: hicbir sey islenmediyse kuyruk dokunulmaz.
        if (processed.IsEmpty)
        {
            return this;
        }

        var settled = Settled;
        var remove = new HashSet<Guid>();

        foreach (var command in processed)
        {
            settled = settled.Add(command.CommandId);
            remove.Add(command.CommandId);
        }

        if (Pending.IsEmpty)
        {
            return this with { Settled = settled };
        }

        var remaining = ImmutableArray.CreateBuilder<ScheduledManagerCommand>(Pending.Length);

        foreach (var command in Pending)
        {
            if (!remove.Contains(command.CommandId))
            {
                remaining.Add(command);
            }
        }

        return this with { Pending = remaining.ToImmutable(), Settled = settled };
    }

    /// <summary>
    /// Suresi dolmus komutlari kuyruktan cikarir ve reddedilmis sayar
    /// (07 §6 madde 4).
    /// </summary>
    public (CommandQueue Queue, ImmutableArray<CommandResult> Results) ExpirePast(
        long sequence)
    {
        var remaining = ImmutableArray.CreateBuilder<ScheduledManagerCommand>();
        var expired = ImmutableArray.CreateBuilder<ScheduledManagerCommand>();

        foreach (var command in Pending)
        {
            if (command.IsExpiredAfter(sequence))
            {
                expired.Add(command);
            }
            else
            {
                remaining.Add(command);
            }
        }

        if (expired.Count == 0)
        {
            return (this, []);
        }

        var results = ImmutableArray.CreateBuilder<CommandResult>();
        var settled = Settled;

        foreach (var command in expired.ToImmutable())
        {
            results.Add(CommandResult.Rejected1(
                command,
                CommandRejectionReason.Expired,
                sequence));

            settled = settled.Add(command.CommandId);
        }

        return (this with { Pending = remaining.ToImmutable(), Settled = settled }, results.ToImmutable());
    }

    /// <summary>
    /// Mac terminal duruma girdiginde tum bekleyen komutlar sonlanir
    /// (07 §6 madde 4: "Mac bittiginde bekleyen komutlar Expired/Rejected
    /// olarak sonuclanir").
    /// </summary>
    public (CommandQueue Queue, ImmutableArray<CommandResult> Results) ExpireAll(long sequence)
    {
        if (Pending.IsEmpty)
        {
            return (this, []);
        }

        var results = ImmutableArray.CreateBuilder<CommandResult>();
        var settled = Settled;

        foreach (var command in Pending)
        {
            results.Add(CommandResult.Rejected1(
                command,
                CommandRejectionReason.Expired,
                sequence,
                "Mac terminal duruma girdi."));

            settled = settled.Add(command.CommandId);
        }

        return (this with { Pending = [], Settled = settled }, results.ToImmutable());
    }

    /// <summary>
    /// D86: FIFO. <c>ToplamOrder</c> stable sort kullanir; ayni <c>AcceptedOrder</c>
    /// degerine sahip iki komut (olamaz, ama savunma olarak) kayit sirasiyla
    /// korunur ve siralama kararsizlasmaz.
    /// </summary>
    private static ImmutableArray<ScheduledManagerCommand> Sort(
        ImmutableArray<ScheduledManagerCommand> commands)
    {
        if (commands.Length < 2)
        {
            return commands;
        }

        return [.. commands
            .Select((command, index) => (command, index))
            .OrderBy(item => item.command.AcceptedOrder)
            .ThenBy(item => item.index)
            .Select(item => item.command)];
    }
}
