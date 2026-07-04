using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Scrat.Core.Exceptions;
using Scrat.Core.Exporting;
using Scrat.Core.Exporting.Abstractions;
using Scrat.Core.Models;
using Scrat.Exporters.Smb.Abstractions;

namespace Scrat.Exporters.Smb;

/// <summary>
/// SMB destination. Writes are offset-addressed, so a retried call rewrites the same region and
/// stream sessions can resume after a connection fault without corrupting the file.
/// </summary>
public sealed class SmbExporter(ISmbConnectionFactory connectionFactory, ILogger<SmbExporter> logger) : IExporter
{
    private readonly ConcurrentDictionary<string, StreamSession> _sessions = new();

    public ExporterType Type => ExporterType.Smb;

    public async Task WriteAsync(ExportData data, string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        await using var connection = await connectionFactory.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await using var file = await connection.CreateFileAsync(ExportPath.ToFileName(key), cancellationToken).ConfigureAwait(false);
        await file.WriteAsync(0, data.Content, cancellationToken).ConfigureAwait(false);
        await file.FlushAsync(cancellationToken).ConfigureAwait(false);

        logger.LogDebug("SMB: wrote {Bytes} bytes for key {Key}", data.Content.Length, key);
    }

    public async Task OpenAsync(string key, CancellationToken cancellationToken = default)
    {
        await StartSessionAsync(key, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("SMB: opened stream session for key {Key}", key);
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
            await session.File.WriteAsync(session.CommittedOffset, chunk, cancellationToken).ConfigureAwait(false);

            // Only advance after the write succeeded, so a retried chunk rewrites the same region.
            session.CommittedOffset += chunk.Length;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not NonRetryableExportException)
        {
            session.Faulted = true;
            throw new ExportException($"SMB stream write failed for key '{key}' at offset {session.CommittedOffset}.", ex);
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
            await session.File.FlushAsync(cancellationToken).ConfigureAwait(false);
            _sessions.TryRemove(key, out _);
            await session.DisposeAsync().ConfigureAwait(false);
            logger.LogDebug("SMB: closed stream session for key {Key} at {Bytes} bytes", key, session.CommittedOffset);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not NonRetryableExportException)
        {
            session.Faulted = true;
            throw new ExportException($"SMB stream close failed for key '{key}'.", ex);
        }
    }

    public async Task AbortStreamAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryRemove(key, out var session))
        {
            logger.LogWarning("SMB: aborting stream session for key {Key} at offset {Offset}", key, session.CommittedOffset);
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<StreamSession> StartSessionAsync(string key, CancellationToken cancellationToken)
    {
        // A retried first chunk lands here again: drop the stale session and recreate the file from scratch.
        if (_sessions.TryRemove(key, out var stale))
        {
            await stale.DisposeAsync().ConfigureAwait(false);
        }

        var connection = await connectionFactory.ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var file = await connection.CreateFileAsync(ExportPath.ToFileName(key), cancellationToken).ConfigureAwait(false);
            var session = new StreamSession(connection, file);
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
            : throw new NonRetryableExportException($"No active SMB stream session for key '{key}'; the transfer must restart from the first chunk.");

    private async Task RecoverSessionAsync(StreamSession session, string key, CancellationToken cancellationToken)
    {
        logger.LogWarning("SMB: reconnecting stream session for key {Key}, resuming at offset {Offset}", key, session.CommittedOffset);
        await session.DisposeAsync().ConfigureAwait(false);

        var connection = await connectionFactory.ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            session.Replace(connection, await connection.OpenExistingAsync(ExportPath.ToFileName(key), cancellationToken).ConfigureAwait(false));
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class StreamSession(ISmbShareConnection connection, ISmbFileHandle file)
    {
        private ISmbShareConnection _connection = connection;

        public ISmbFileHandle File { get; private set; } = file;

        public long CommittedOffset { get; set; }

        public bool Faulted { get; set; }

        public void Replace(ISmbShareConnection connection, ISmbFileHandle file)
        {
            _connection = connection;
            File = file;
            Faulted = false;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await File.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Cleanup of an already-broken handle must not mask the original failure.
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
