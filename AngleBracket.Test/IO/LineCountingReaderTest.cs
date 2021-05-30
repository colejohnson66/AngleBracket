/* ============================================================================
 * File:   LineCountingReaderTest.cs
 * Author: Cole Johnson
 * ============================================================================
 * Purpose:
 *
 * Tests `AngleBracket.IO.LineCountingReader`.
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
using AngleBracket.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AngleBracket.Test.IO
{
    // TODO: use randomized values
    [TestClass]
    public class LineCountingReaderTest
    {
        // NOTE: Do NOT change this without modifying the tests
        // Expected results of `Read` at position (line,byte,char):
        // (0,0,0) 'a'
        // (0,1,1) '\n'
        // (1,0,0) 'b'
        // (1,1,1) 'c'
        // (1,2,2) '\r'
        // (1,3,3) '\n'
        // (2,0,0) 'd'
        // (2,1,1) 'e'
        // (2,2,2) 'f'
        // (2,3,3) EOF
        private const string TestString = "a\nbc\r\ndef";

        [TestMethod]
        public void ReadingTracksLineAndOffset()
        {
            using (LineCountingReader reader = new LineCountingReader(GetTestStream()))
            {
                int[] buffer = new int[2];

                Assert.IsTrue(reader.Read(buffer) == 2);
                Assert.IsTrue(reader.LineOffsetTuple == (1, 0, 0));

                Assert.IsTrue(reader.Read(buffer) == 2);
                Assert.IsTrue(reader.LineOffsetTuple == (1, 2, 2));

                Assert.IsTrue(reader.Read(buffer) == 2);
                Assert.IsTrue(reader.LineOffsetTuple == (2, 0, 0));

                Assert.IsTrue(reader.Read(buffer) == 2);
                Assert.IsTrue(reader.LineOffsetTuple == (2, 2, 2));

                Assert.IsTrue(reader.Read(buffer) == 1);
                Assert.IsTrue(reader.LineOffsetTuple == (2, 3, 3));

                Assert.IsTrue(reader.Read() == -1);

                List<(int, int)> lineLengths = reader.GetLineLengths();
                Assert.IsTrue(lineLengths.Count == 2);
                Assert.IsTrue(lineLengths[0] == (1, 1));
                Assert.IsTrue(lineLengths[1] == (3, 3));
            }
        }

        [TestMethod]
        public void SeekingTracksLineAndOffset()
        {
            using (LineCountingReader reader = new LineCountingReader(GetTestStream()))
            {
                reader.Seek(2, SeekOrigin.Begin);
                Assert.IsTrue(reader.LineOffsetTuple == (1, 0, 0));

                reader.Seek(3, SeekOrigin.Begin);
                Assert.IsTrue(reader.LineOffsetTuple == (1, 1, 1));

                reader.Seek(2, SeekOrigin.Current);
                Assert.IsTrue(reader.LineOffsetTuple == (1, 3, 3));

                reader.Seek(-4, SeekOrigin.Current);
                Assert.IsTrue(reader.LineOffsetTuple == (0, 1, 1));

                List<(int, int)> lineLengths = reader.GetLineLengths();
                Assert.IsTrue(lineLengths.Count == 1);
                Assert.IsTrue(lineLengths[0] == (1, 1));
            }
        }

        [TestMethod]
        public void PeekingDoesntChangeStreamPosition()
        {
            using (LineCountingReader reader = new LineCountingReader(GetTestStream()))
            {
                int[] buffer = new int[2];

                reader.Seek(5, SeekOrigin.Begin);

                Assert.IsTrue(reader.Peek() == '\n');

                Assert.IsTrue(reader.Peek(buffer) == 2);
                Assert.IsTrue(buffer[0] == '\n');
                Assert.IsTrue(buffer[1] == 'd');

                Assert.IsTrue(reader.Peek() == '\n');

                Assert.IsTrue(reader.LineOffsetTuple == (1, 3, 3));
            }
        }

        [TestMethod]
        public void BacktrackingTracksLineAndOffset()
        {
            using (LineCountingReader reader = new LineCountingReader(GetTestStream()))
            {
                int[] buffer = new int[2];

                reader.Seek(5, SeekOrigin.Begin);

                reader.Backtrack();
                Assert.IsTrue(reader.LineOffsetTuple == (1, 2, 2));

                reader.Backtrack(10);
                Assert.IsTrue(reader.LineOffsetTuple == (0, 0, 0));

                reader.Seek(5, SeekOrigin.Begin);
                reader.Backtrack(2);
                Assert.IsTrue(reader.LineOffsetTuple == (1, 1, 1));
            }
        }

        private static Stream GetTestStream()
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream, Encoding.ASCII);
            writer.Write(TestString);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}
