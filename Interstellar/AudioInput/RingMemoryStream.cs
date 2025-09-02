using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.AudioInput;

internal class RingMemoryStream : Stream
{
    private readonly byte[] _buffer;
    private int _writePosition;
    private int _readPosition;
    private int _bytesInBuffer;
    private bool _isEof;
    private readonly object _lockObject = new object();

    public RingMemoryStream(int bufferSize)
    {
        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "バッファサイズは正の値である必要があります。");

        _buffer = new byte[bufferSize];
        Clear();
    }

    public override bool CanWrite => true;
    public override bool CanRead => true;
    public override bool CanSeek => false;

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Length => _bytesInBuffer;
    public int Capacity => _buffer.Length;
    public int BytesAvailable => _bytesInBuffer;
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public void Clear()
    {
        lock (_lockObject)
        {
            _writePosition = 0;
            _readPosition = 0;
            _bytesInBuffer = 0;
            _isEof = false;
        }
    }
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (offset + count > buffer.Length)
            throw new ArgumentException();

        lock (_lockObject)
        {
            if (_bytesInBuffer == 0 || _isEof)
                return 0;

            int bytesToRead = Math.Min(count, _bytesInBuffer);
            int bytesRead = 0;

            // バッファの末尾までまず読み取る
            int bytesToReadUntilEnd = Math.Min(bytesToRead, _buffer.Length - _readPosition);
            Array.Copy(_buffer, _readPosition, buffer, offset, bytesToReadUntilEnd);
            bytesRead += bytesToReadUntilEnd;
            _readPosition = (_readPosition + bytesToReadUntilEnd) % _buffer.Length;

            // 必要に応じてバッファの先頭から残りを読み取る
            if (bytesRead < bytesToRead)
            {
                Array.Copy(_buffer, _readPosition, buffer, offset + bytesRead, bytesToRead - bytesRead);
                _readPosition = (_readPosition + bytesToRead - bytesRead) % _buffer.Length;
                bytesRead = bytesToRead;
            }

            _bytesInBuffer -= bytesRead;
            return bytesRead;
        }
    }

    /// <summary>
    /// 指定したバイト配列からバイト シーケンスを現在のストリームに書き込みます。
    /// </summary>
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (offset + count > buffer.Length)
            throw new ArgumentException("オフセットとカウントの合計がバッファサイズを超えています。");

        if (count == 0)
            return;

        lock (_lockObject)
        {
            // バッファの末尾まで書き込む
            int bytesToWriteUntilEnd = Math.Min(count, _buffer.Length - _writePosition);
            Array.Copy(buffer, offset, _buffer, _writePosition, bytesToWriteUntilEnd);
            
            // 必要に応じてバッファの先頭に残りを書き込む
            if (bytesToWriteUntilEnd < count)
            {
                Array.Copy(buffer, offset + bytesToWriteUntilEnd, _buffer, 0, count - bytesToWriteUntilEnd);
            }

            int oldWritePosition = _writePosition;
            _writePosition = (_writePosition + count) % _buffer.Length;
            _bytesInBuffer += count;

            // バッファがオーバーフローした場合、読み取り位置を調整
            if (_bytesInBuffer > _buffer.Length)
            {
                int overflow = _bytesInBuffer - _buffer.Length;
                _readPosition = (_readPosition + overflow) % _buffer.Length;
                _bytesInBuffer = _buffer.Length;
            }
        }
    }

    public override void Flush() {}
}
