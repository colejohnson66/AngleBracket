/* ============================================================================
 * File:   LineCountingReader.cs
 * Author: Cole Johnson
 * ============================================================================
 * Purpose:
 *
 * TODO
 * ============================================================================
 * Copyright (c) 2021 Cole Johnson
 *
 * This file is part of AngleBracket.
 *
 * AngleBracket is free software: you can redistribute it and/or modify it
 *   under the terms of the GNU General Public License as published by the Free
 *   Software Foundation, either version 3 of the License, or (at your option)
 *   any later version.
 *
 * AngleBracket is distributed in the hope that it will be useful, but WITHOUT
 *   ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
 *   FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 *   more details.
 *
 * You should have received a copy of the GNU General Public License along with
 *   AngleBracket. If not, see <http://www.gnu.org/licenses/>.
 * ============================================================================
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AngleBracket.IO
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class LineCountingReader : Stream, IDisposable
    {
        private const char LF = '\n';

        private Stream _stream;
        private bool _disposed;

        // TODO: should reaching EOF add an entry?
        private List<int> _lineLengths;

        public int Line { get; private set; } = 0;
        public int Offset { get; private set; } = 0;

        public LineCountingReader(Stream stream)
        {
            Contract.Requires(stream.CanRead);
            Contract.Requires(stream.CanSeek);

            _stream = stream;
            _lineLengths = new List<int>(1024);
        }

        private string GetDebuggerDisplay()
        {
            return string.Format(
                "LineCountingReader(Line={0},Offset={1})",
                Line, Offset
            );
        }

        // for testing
        internal List<int> GetLineLengths() => _lineLengths;
        internal (int, int) LineOffsetTuple => (Line, Offset);

        private bool SaveLineLength(int line, int length)
        {
            Contract.Requires(line >= 0);
            Contract.Requires(length >= 0);

            // save the length, but only if it's never been seen
            if (_lineLengths.Count == line)
            {
                _lineLengths.Add(length);
                return true;
            }

            return false;
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _stream.Length;
        public override long Position
        {
            // TODO: `Seek(Position - value, SeekOrigin.Current)`?
            get => _stream.Position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override bool CanTimeout => base.CanTimeout;

        public override int ReadTimeout { get => base.ReadTimeout; set => base.ReadTimeout = value; }
        public override int WriteTimeout { get => base.WriteTimeout; set => base.WriteTimeout = value; }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override void Close()
        {
            _stream.Close();
            base.Close();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int result = _stream.Read(buffer, offset, count);

            // count line feeds
            for (int i = 0; i < result; i++)
            {
                if (buffer[i] == LF)
                {
                    SaveLineLength(Line, Offset);
                    Line++;
                    Offset = 0;
                    continue;
                }
                Offset++;
            }

            return result;
        }

        public override int Read(Span<byte> buffer)
        {
            // TODO
            throw new NotImplementedException();
        }

        public override int ReadByte()
        {
            int result = _stream.ReadByte();
            if (result == -1)
                return -1;

            if (result == LF)
            {
                SaveLineLength(Line, Offset);
                Line++;
                Offset = 0;
                return LF;
            }

            Offset++;
            return result;
        }

        public int Peek()
        {
            // avoid needless changing of `Line` and `Offset` by using `_stream` directly
            int result = _stream.ReadByte();
            if (result == -1)
                return -1;

            _stream.Seek(-1, SeekOrigin.Current);
            return result;
        }

        public int Peek(byte[] buffer, int offset, int count)
        {
            // avoid needless changing of `Line` and `Offset` by using `_stream` directly
            int result = _stream.Read(buffer, offset, count);
            _stream.Seek(-result, SeekOrigin.Current);
            return result;
        }

        public void Backtrack()
        {
            // avoid needless changing of `Line` and `Offset` by using `_stream` directly
            if (_stream.Position == 0)
                return;

            _stream.Seek(-1, SeekOrigin.Current);

            if (Offset == 0)
            {
                Line--;
                Offset = _lineLengths[Line];
                return;
            }
            Offset--;
        }

        public int Backtrack(int count)
        {
            Contract.Requires(count >= 0);

            // avoid needless changing of `Line` and `Offset` by using `_stream` directly
            if (count >= _stream.Position)
            {
                // short circuit
                Line = 0;
                Offset = 0;
                int oldPosition = (int)_stream.Position;
                _stream.Seek(0, SeekOrigin.Begin);
                return oldPosition;
            }

            // TODO: use `_lineLengths` to quickly backtrack
            int counted = count;
            while ((counted--) >= 0)
                Backtrack();
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            byte[] buffer;

            if (origin == SeekOrigin.Begin)
            {
                Line = 0;
                Offset = 0;

                // TODO: read in chunks, not all at once
                buffer = new byte[offset];
                _stream.Seek(0, SeekOrigin.Begin);
                Read(buffer, 0, buffer.Length);
                return _stream.Position;
            }

            if (origin == SeekOrigin.Current)
            {
                if (offset == 0)
                    return _stream.Position;

                if (offset < 0)
                {
                    // TODO: read in chunks, not all at once
                    buffer = new byte[-offset];
                    int start = (int)_stream.Seek(offset, SeekOrigin.Current);
                    int count = _stream.Read(buffer, 0, buffer.Length);
                    for (int i = count - start; i >= 0; i--)
                    {
                        if (buffer[i] == LF)
                        {
                            Line--;
                            Offset = _lineLengths[Line];
                            continue;
                        }
                        Offset--;
                    }

                    // seek back to where we need
                    _stream.Seek(offset, SeekOrigin.Current);
                    return _stream.Position;
                }

                // offset > 0
                // TODO: read in chunks, not all at once
                buffer = new byte[offset];
                Read(buffer, 0, buffer.Length);
                return _stream.Position;
            }

            // SeekOrigin.End
            throw new NotImplementedException();
        }

        public override void SetLength(long value) => throw new NotImplementedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
                _stream.Dispose();

            _disposed = true;
        }

        public override void Write(ReadOnlySpan<byte> buffer) => throw new NotImplementedException();
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotImplementedException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override void WriteByte(byte value) => throw new NotImplementedException();
    }
}
