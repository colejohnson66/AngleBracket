/* ============================================================================
 * File:   Attribute.cs
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
    internal class Attribute
    {
        private StringBuilder _name = new StringBuilder();
        private StringBuilder _value = new StringBuilder();

        internal Attribute() { }

        internal string Name => _name.ToString();
        internal string Value => _value.ToString();

        internal void AppendToName(char c) => _name.Append(c);
        internal void AppendToName(int c) => _name.Append(Char.ConvertFromUtf32(c));
        internal void AppendToName(string s) => _name.Append(s);
        internal void AppendToValue(char c) => _value.Append(c);
        internal void AppendToValue(int c) => _value.Append(char.ConvertFromUtf32(c));
        internal void AppendToValue(string s) => _value.Append(s);

        public override string ToString()
        {
            return string.Format(
                "{0}=\"{1}\"",
                Name, Value);
        }
    }
}
