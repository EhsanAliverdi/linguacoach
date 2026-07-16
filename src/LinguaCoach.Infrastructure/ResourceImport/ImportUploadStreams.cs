using System.Security.Cryptography;
using LinguaCoach.Application.Storage;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.7 (2026-07-17 reliable large uploads) — read-only, forward-only <see cref="Stream"/>
/// wrapper that computes a running SHA-256 over every byte read while passing them straight
/// through unchanged. Used so <c>ImportUploadSessionService</c> can verify a part's (or the
/// assembled archive's) checksum in the same single streaming pass that writes it to storage,
/// without buffering the content a second time just to hash it.
/// </summary>
internal sealed class HashingPassthroughStream : Stream
{
    private readonly Stream _inner;
    private readonly HashAlgorithm _hash;
    private readonly bool _leaveOpenInner;
    private bool _finalized;

    public long BytesRead { get; private set; }

    public HashingPassthroughStream(Stream inner, HashAlgorithm hash, bool leaveOpenInner = false)
    {
        _inner = inner;
        _hash = hash;
        _leaveOpenInner = leaveOpenInner;
    }

    public byte[] GetHash()
    {
        if (!_finalized)
        {
            _hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            _finalized = true;
        }
        return _hash.Hash!;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => BytesRead;
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var n = await _inner.ReadAsync(buffer, cancellationToken);
        if (n > 0)
        {
            var segment = buffer[..n];
            var array = System.Buffers.ArrayPool<byte>.Shared.Rent(n);
            try
            {
                segment.Span.CopyTo(array);
                _hash.TransformBlock(array, 0, n, null, 0);
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(array);
            }
            BytesRead += n;
        }
        return n;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_leaveOpenInner) _inner.Dispose();
            _hash.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_leaveOpenInner) await _inner.DisposeAsync();
        _hash.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Phase 4.7 (2026-07-17 reliable large uploads) — read-only <see cref="Stream"/> that presents a
/// sequence of storage-backed parts (already-uploaded chunks, one <c>IFileStorageService</c>
/// object per part) as a single continuous stream, opening each part lazily and disposing it once
/// exhausted. Used to assemble the final archive without ever holding more than one part's bytes
/// in flight at a time — no full-archive buffer, in memory or otherwise, is created by this class.
/// </summary>
internal sealed class SequentialPartStream : Stream
{
    private readonly IReadOnlyList<string> _partStorageKeys;
    private readonly IFileStorageService _storage;
    private int _index = -1;
    private Stream? _current;

    public SequentialPartStream(IReadOnlyList<string> partStorageKeysInOrder, IFileStorageService storage)
    {
        _partStorageKeys = partStorageKeysInOrder;
        _storage = storage;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (_current is null)
            {
                _index++;
                if (_index >= _partStorageKeys.Count) return 0;
                _current = await _storage.ReadAsync(_partStorageKeys[_index], cancellationToken);
            }

            var n = await _current.ReadAsync(buffer, cancellationToken);
            if (n > 0) return n;

            await _current.DisposeAsync();
            _current = null;
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _current?.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_current is not null) await _current.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
