using Scrat.Core.Abstractions;

namespace Scrat.Core.Models;

/// <summary>The cluster that holds a key, together with the resolved bucket name.</summary>
public sealed record S3EndpointMatch(IS3Endpoint Endpoint, string Bucket);
