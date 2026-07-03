namespace Scrat.Core.Models;

/// <summary>Size tier of an S3 cluster. Order matters: endpoints are probed in ascending order.</summary>
public enum SizeCategory
{
    Small = 0,
    Medium = 1,
    Large = 2,
}
