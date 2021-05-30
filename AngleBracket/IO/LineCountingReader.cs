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
    public class LineCountingReader : IDisposable
    {
        private const char LF = '\n';

        private BufferedStream _stream;
        private bool _disposed;

        // TODO: should reaching EOF add an entry?
        // tuple is (bytesPerLine, charsPerLine)
        private List<(int, int)> _lineLengths;

        //private int CharPosition { get; set; } = 0; // `Stream.Position` but for chars, not bytes
        public int Line { get; private set; } = 0;
        private int ByteOffset { get; set; } = 0;
        public int CharOffset { get; private set; } = 0;

        public LineCountingReader(Stream stream)
        {
            Contract.Requires(stream.CanRead);
            Contract.Requires(stream.CanSeek);

            _stream = new BufferedStream(stream);
            _lineLengths = new List<(int, int)>(1024);
        }

        private string GetDebuggerDisplay()
        {
            return string.Format(
                "LineCountingReader(Line={0},ByteOffset={1},CharOffset={2})",
                Line, ByteOffset, CharOffset
            );
        }

        // for testing
        internal List<(int, int)> GetLineLengths() => _lineLengths;
        internal (int, int, int) LineOffsetTuple => (Line, ByteOffset, CharOffset);

        private bool SaveLineLength(int line, int byteLength, int charLength)
        {
            Contract.Requires(line >= 0);
            Contract.Requires(byteLength >= 0);
            Contract.Requires(charLength >= 0);

            // save the length, but only if it's never been seen
            if (_lineLengths.Count == line)
            {
                _lineLengths.Add((byteLength, charLength));
                return true;
            }

            return false;
        }

        public long ByteLength => _stream.Length;

        public void Close()
        {
            _stream.Close();
        }

        public int Read()
        {
            // decodes UTF-8

            int byte1 = _stream.ReadByte();
            if (byte1 == -1)
                return -1;

            // one byte (ASCII)
            if ((byte1 & 0b1000_0000) == 0)
            {
                if (byte1 == LF)
                {
                    SaveLineLength(Line, ByteOffset, CharOffset);
                    Line++;
                    ByteOffset = 0;
                    CharOffset = 0;
                }
                else
                {
                    ByteOffset++;
                    CharOffset++;
                }
                return byte1;
            }

            if ((byte1 & 0b1110_0000) == 0b1100_0000)
            {
                // two bytes (U+0080 - U+07FF)
                int decoded = byte1 & 0b0001_1111;

                int byte2 = _stream.ReadByte();
                if (byte2 == -1)
                    throw new FormatException();
                if ((byte2 & 0b1100_0000) != 0b1000_0000)
                    throw new FormatException();

                decoded <<= 6;
                decoded |= byte2 & 0b0011_1111;

                // invalid sequence; must be encoded in smallest form
                if (decoded < 0x80 || decoded >= 0x7FF)
                    throw new FormatException();

                // new line will never be decoded here, so don't check for it
                ByteOffset += 2;
                CharOffset++;
                return decoded;
            }

            if ((byte1 & 0b1111_0000) == 0b1110_0000)
            {
                // three bytes (U+0800 - U+FFFF)
                int decoded = byte1 & 0b0000_1111;

                int byte2 = _stream.ReadByte();
                if (byte2 == -1)
                    throw new FormatException();
                if ((byte2 & 0b1100_0000) != 0b1000_0000)
                    throw new FormatException();

                decoded <<= 6;
                decoded |= byte2 & 0b0011_1111;

                int byte3 = _stream.ReadByte();
                if (byte3 == -1)
                    throw new FormatException();
                if ((byte3 & 0b1100_0000) != 0b1000_0000)
                    throw new FormatException();

                decoded <<= 6;
                decoded |= byte3 & 0b0011_1111;

                // invalid sequence; must be encoded in smallest form
                if (decoded < 0x800 || decoded >= 0xFFFF)
                    throw new FormatException();

                // new line will never be decoded here, so don't check for it
                ByteOffset += 3;
                CharOffset++;
                return decoded;
            }

            if ((byte1 & 0b1111_1000) == 0b1111_0000)
            {
                // four bytes (U+1'0000 - U+10'FFFF)
                int decoded = byte1 & 0b0000_1111;

                int byte2 = _stream.ReadByte();
                if (byte2 == -1)
                    throw new FormatException();
                if ((byte2 & 0b1100_0000) != 0b1000_0000)
                    throw new FormatException();

                decoded <<= 6;
                decoded |= byte2 & 0b0011_1111;

                int byte3 = _stream.ReadByte();
                if (byte3 == -1)
                    throw new FormatException();
                if ((byte3 & 0b1100_0000) != 0b1000_0000)
                    throw new FormatException();

                decoded <<= 6;
                decoded |= byte3 & 0b0011_1111;

                int byte4 = _stream.ReadByte();
                if (byte4 == -1)
                    throw new FormatException();
                if ((byte4 & 0b1100_0000) != 0b1000_0000)
                    throw new FormatException();

                decoded <<= 6;
                decoded |= byte4 & 0b0011_1111;

                // invalid sequence; must be encoded in smallest form
                if (decoded < 0x10000 || decoded > 0x10FFFF)
                    throw new FormatException();

                // new line will never be decoded here, so don't check for it
                ByteOffset += 4;
                CharOffset++;
                return decoded;
            }

            // while UTF-8 initially supported encoding up to 15
            //   bits (see RFC2279 section 2), it has since been
            //   limited to only supporting codepoints through
            //   U+10FFFF (see RFC3629 section 3)
            // TODO: should a sentinel be used (such as -2) instead?
            throw new FormatException();
        }

        public int Read(int[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                int c = Read();
                if (c == -1)
                    return i;
                buffer[i] = c;
            }
            return buffer.Length;
        }

        public int Peek()
        {
            int c = Read();
            if (c == -1)
                return -1;
            Backtrack();
            return c;
        }

        public int Peek(int[] buffer)
        {
            long oldPosition = _stream.Position;
            (int, int, int) oldTuple = LineOffsetTuple;

            int peeked = Read(buffer);

            // seek back
            _stream.Seek(oldPosition, SeekOrigin.Begin);
            Line = oldTuple.Item1;
            ByteOffset = oldTuple.Item2;
            CharOffset = oldTuple.Item3;

            return peeked;
        }

        public void Backtrack()
        {
            // avoid needless changing of `Line` and `Offset` by using `_stream` directly
            if (_stream.Position == 0)
                return;

            // to avoid multiple seeks, seek back 4 bytes, then read those 4, and work backwards
            byte[] buffer = new byte[4];
            int oldPos = (int)_stream.Position;

            // if we can't seek more than 4 back
            if (_stream.Position < 4)
            {
                // fill at the end of the buffer
                _stream.Seek(0, SeekOrigin.Begin);
                Debug.Assert(_stream.Read(buffer, 4 - oldPos, oldPos) == oldPos);
            }
            else
            {
                _stream.Seek(-4, SeekOrigin.Current);
                Debug.Assert(_stream.Read(buffer, 0, 4) == 4);
            }

            if ((buffer[3] & 0b1000_0000) == 0)
            {
                // one byte
                if (buffer[3] == LF)
                {
                    Debug.Assert(CharOffset == 0);
                    Debug.Assert(ByteOffset == 0);
                    Line--;
                    (int, int) tuple = _lineLengths[Line];
                    ByteOffset = tuple.Item1;
                    CharOffset = tuple.Item2;
                }
                else
                {
                    ByteOffset--;
                    CharOffset--;
                }

                _stream.Seek(oldPos - 1, SeekOrigin.Begin);
                return;
            }

            // should never happen - it would mean a malformed bytestream
            Debug.Assert((buffer[3] & 0b1100_0000) == 0b1000_0000);

            // new lines will never exist here, so don't check for them
            // if they did exist, `Read` would've thrown upon encounter
            if ((buffer[2] & 0b1110_0000) == 0b1100_0000)
            {
                // two byte
                ByteOffset -= 2;
                CharOffset--;

                _stream.Seek(oldPos - 2, SeekOrigin.Begin);
                return;
            }

            if ((buffer[1] & 0b1111_0000) == 0b1110_0000)
            {
                // three byte
                ByteOffset -= 3;
                CharOffset--;

                _stream.Seek(oldPos - 3, SeekOrigin.Begin);
                return;
            }

            // four byte
            Debug.Assert((buffer[0] & 0b1111_1000) == 0b1111_0000);
            ByteOffset -= 4;
            CharOffset--;

            _stream.Seek(oldPos - 4, SeekOrigin.Begin);
        }

        public void Backtrack(int count)
        {
            Contract.Requires(count >= 0);

            // avoid needless changing of `Line` and `Offset` by using `_stream` directly
            if (count >= _stream.Position)
            {
                // short circuit
                Line = 0;
                CharOffset = 0;
                ByteOffset = 0;
                _stream.Seek(0, SeekOrigin.Begin);
                return;
            }

            // TODO: use `_lineLengths` to quickly backtrack
            while (count-- > 0)
                Backtrack();
        }

        public void Seek(int charOffset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                _stream.Seek(0, SeekOrigin.Begin);
                Line = 0;
                ByteOffset = 0;
                CharOffset = 0;

                // TODO: use `_lineLengths` if possible
                while (charOffset-- > 0)
                    Read();
                return;
            }

            if (origin == SeekOrigin.Current)
            {
                if (charOffset == 0)
                    return;

                if (charOffset < 0)
                {
                    // TODO: use `_lineLengths` if possible
                    Backtrack(-charOffset);
                    return;
                }

                // offset > 0
                while (charOffset-- > 0)
                    Read();
                return;
            }

            // SeekOrigin.End
            throw new NotImplementedException();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            // managed cleanup
            if (disposing)
                _stream.Dispose();

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
