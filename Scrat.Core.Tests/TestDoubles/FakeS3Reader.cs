using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.Tests.TestDoubles;

/// <summary>In-memory reader; inherits the real ReadChunksAsync composition from the interface.</summary>
internal sealed class FakeS3Reader : IS3Reader
{
    private readonly Dictionary<string, byte[]> _objects = [];

    public int ReadRangeCalls { get; private set; }

    public FakeS3Reader Put(string bucket, string key, byte[] data)
    {
        _objects[$"{bucket}/{key}"] = data;
        return this;
    }

    public Task<bool> ObjectExistsAsync(string bucketName, string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_objects.ContainsKey($"{bucketName}/{key}"));

    public Task<byte[]> ReadAllAsync(string bucketName, string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_objects[$"{bucketName}/{key}"]);

    public Task<long> GetObjectSizeAsync(string bucketName, string key, CancellationToken cancellationToken = default) =>
        Task.FromResult((long)_objects[$"{bucketName}/{key}"].Length);

    public Task<byte[]> ReadRangeAsync(string bucketName, string key, long offset, int count, CancellationToken cancellationToken = default)
    {
        ReadRangeCalls++;
        return Task.FromResult(_objects[$"{bucketName}/{key}"][(int)offset..((int)offset + count)]);
    }
}
