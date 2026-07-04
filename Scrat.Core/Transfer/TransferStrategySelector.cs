using System.Collections.Frozen;
using Scrat.Core.Models;
using Scrat.Core.Transfer.Abstractions;

namespace Scrat.Core.Transfer;

/// <summary>Maps a size category to the registered strategy that handles it.</summary>
public sealed class TransferStrategySelector : ITransferStrategySelector
{
    private readonly FrozenDictionary<SizeCategory, ITransferStrategy> _strategies;

    public TransferStrategySelector(IEnumerable<ITransferStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        _strategies = strategies.ToFrozenDictionary(s => s.Handles);
    }

    public ITransferStrategy Select(SizeCategory category) =>
        _strategies.TryGetValue(category, out var strategy)
            ? strategy
            : throw new NotSupportedException($"No transfer strategy registered for size category '{category}'.");
}
