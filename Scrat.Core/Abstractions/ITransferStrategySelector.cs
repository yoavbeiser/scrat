using Scrat.Core.Models;

namespace Scrat.Core.Abstractions;

/// <summary>Maps a <see cref="SizeCategory"/> to the strategy that handles it.</summary>
public interface ITransferStrategySelector
{
    ITransferStrategy Select(SizeCategory category);
}
