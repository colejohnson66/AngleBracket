/* ============================================================================
 * File:   Tag.cs
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
using System.Text;

namespace AngleBracket.Tokenizer
{
    internal class Tag
    {
        private StringBuilder _name = new StringBuilder();
        private bool _selfClosing = false;
        private bool _endTag = false;
        private List<Attribute> _attributes = new List<Attribute>(16);

        internal Tag() { }

        internal string Name => _name.ToString();
        internal bool IsSelfClosing => _selfClosing;
        internal bool IsEndTag => _endTag;
        internal List<Attribute> Attributes => _attributes;

        internal void AppendToName(char c) => _name.Append(c);
        internal void AppendToName(int c) => _name.Append(Char.ConvertFromUtf32(c));
        internal void AppendToName(string s) => _name.Append(s);
        internal void SetSelfClosingFlag() => _selfClosing = true;
        internal void SetEndTagFlag() => _endTag = true;
        internal void AddAttribute(Attribute attribute) => _attributes.Add(attribute);

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat(
                "\"{0}\",SelfClosing:{1},EndTag:{2},Attributes:{",
                Name, IsSelfClosing, IsEndTag);
            builder.AppendJoin(',', Attributes);
            builder.Append('}');
            return builder.ToString();
        }
    }
}
