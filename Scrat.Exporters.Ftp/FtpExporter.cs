using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrat.Core.Exceptions;
using Scrat.Core.Exporting;
using Scrat.Core.Exporting.Abstractions;
using Scrat.Core.Models;
using Scrat.Exporters.Ftp.Abstractions;

namespace Scrat.Exporters.Ftp;

/// <summary>
/// FTP destination. Whole-file writes are idempotent (truncate on open) so a retry simply
/// rewrites. Stream sessions resume after a fault by reconnecting and appending — but only when
/// the remote size still equals the committed offset; otherwise the transfer cannot be repaired
/// by retrying and fails with <see cref="NonRetryableExportException"/>.
/// </summary>
public sealed class FtpExporter(IFtpConnectionFactory connectionFactory, IOptions<FtpOptions> options, ILogger<FtpExporter> logger) : IExporter
{
    private readonly ConcurrentDictionary<string, StreamSession> _sessions = new();

    public ExporterType Type => ExporterType.Ftp;

    public async Task WriteAsync(ExportData data, string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        await using var connection = await connectionFactory.ConnectAsync(cancellationToken).ConfigureAwait(false);
        var stream = await connection.OpenWriteAsync(RemotePath(key), cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(data.Content, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogDebug("FTP: wrote {Bytes} bytes for key {Key}", data.Content.Length, key);
    }

    public async Task OpenAsync(string key, CancellationToken cancellationToken = default)
    {
        await StartSessionAsync(key, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("FTP: opened stream session for key {Key}", key);
    }

    public async Task WriteChunkAsync(string key, ReadOnlyMemory<byte> chunk, CancellationToken cancellationToken = default)
    {
        var session = GetSession(key);
        if (session.Faulted)
        {
            await RecoverSessionAsync(session, key, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await session.Stream.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
            await session.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Only advance after the chunk is fully flushed, so a resumed session knows the true remote length.
            session.CommittedOffset += chunk.Length;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not NonRetryableExportException)
        {
            session.Faulted = true;
            throw new ExportException($"FTP stream write failed for key '{key}' at offset {session.CommittedOffset}.", ex);
        }
    }

    public async Task CloseAsync(string key, CancellationToken cancellationToken = default)
    {
        var session = GetSession(key);
        if (session.Faulted)
        {
            await RecoverSessionAsync(session, key, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await session.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            _sessions.TryRemove(key, out _);
            await session.DisposeAsync().ConfigureAwait(false);
            logger.LogDebug("FTP: closed stream session for key {Key} at {Bytes} bytes", key, session.CommittedOffset);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not NonRetryableExportException)
        {
            session.Faulted = true;
            throw new ExportException($"FTP stream close failed for key '{key}'.", ex);
        }
    }

    public async Task AbortStreamAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryRemove(key, out var session))
        {
            logger.LogWarning("FTP: aborting stream session for key {Key} at offset {Offset}", key, session.CommittedOffset);
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<StreamSession> StartSessionAsync(string key, CancellationToken cancellationToken)
    {
        // A retried first chunk lands here again: drop the stale session and truncate the remote file.
        if (_sessions.TryRemove(key, out var stale))
        {
            await stale.DisposeAsync().ConfigureAwait(false);
        }

        var connection = await connectionFactory.ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stream = await connection.OpenWriteAsync(RemotePath(key), cancellationToken).ConfigureAwait(false);
            var session = new StreamSession(connection, stream);
            _sessions[key] = session;
            return session;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private StreamSession GetSession(string key) =>
        _sessions.TryGetValue(key, out var session)
            ? session
            : throw new NonRetryableExportException($"No active FTP stream session for key '{key}'; the transfer must restart from the first chunk.");

    private async Task RecoverSessionAsync(StreamSession session, string key, CancellationToken cancellationToken)
    {
        logger.LogWarning("FTP: reconnecting stream session for key {Key}, resuming at offset {Offset}", key, session.CommittedOffset);
        await session.DisposeAsync().ConfigureAwait(false);

        var connection = await connectionFactory.ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var remotePath = RemotePath(key);
            var remoteSize = await connection.GetFileSizeAsync(remotePath, cancellationToken).ConfigureAwait(false);
            if (remoteSize != session.CommittedOffset)
            {
                throw new NonRetryableExportException(
                    $"Cannot resume FTP stream for key '{key}': remote file is {remoteSize} bytes but {session.CommittedOffset} were committed.");
            }

            session.Replace(connection, await connection.OpenAppendAsync(remotePath, cancellationToken).ConfigureAwait(false));
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private string RemotePath(string key)
    {
        var basePath = options.Value.BasePath.TrimEnd('/');
        return $"{basePath}/{ExportPath.ToFileName(key)}";
    }

    private sealed class StreamSession(IFtpConnection connection, Stream stream)
    {
        private IFtpConnection _connection = connection;

        public Stream Stream { get; private set; } = stream;

        public long CommittedOffset { get; set; }

        public bool Faulted { get; set; }

        public void Replace(IFtpConnection connection, Stream stream)
        {
            _connection = connection;
            Stream = stream;
            Faulted = false;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await Stream.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Cleanup of an already-broken stream must not mask the original failure.
            }

            try
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }
}
