using NSubstitute;
using Scrat.Core.Abstractions;
using Scrat.Core.Models;
using Scrat.Core.Transfer;

namespace Scrat.Core.Tests.Transfer;

public class TransferStrategySelectorTests
{
    [Fact]
    public void Selects_the_strategy_registered_for_the_category()
    {
        var small = Substitute.For<ITransferStrategy>();
        small.Handles.Returns(SizeCategory.Small);
        var large = Substitute.For<ITransferStrategy>();
        large.Handles.Returns(SizeCategory.Large);

        var selector = new TransferStrategySelector([small, large]);

        Assert.Same(large, selector.Select(SizeCategory.Large));
        Assert.Same(small, selector.Select(SizeCategory.Small));
    }

    [Fact]
    public void Unregistered_category_throws()
    {
        var selector = new TransferStrategySelector([]);

        Assert.Throws<NotSupportedException>(() => selector.Select(SizeCategory.Medium));
    }
}
