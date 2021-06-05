/* ============================================================================
 * File:   Comment.cs
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
using System.Text;

namespace AngleBracket.Tokenizer
{
    internal class Comment
    {
        private StringBuilder _value = new StringBuilder();

        internal Comment() { }

        internal string Value => _value.ToString();

        internal void Append(char c) => _value.Append(c);
        internal void Append(int c) => _value.Append(Char.ConvertFromUtf32(c));
        internal void Append(string s) => _value.Append(s);

        public override string ToString()
        {
            return string.Format(
                "\"{0}\"",
                Value);
        }
    }
}
