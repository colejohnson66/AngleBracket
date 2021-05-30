/* ============================================================================
 * File:   Doctype.cs
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
using System.Text;

namespace AngleBracket.Tokenizer
{
    internal class Doctype
    {
        internal bool _quirks = false;
        internal StringBuilder? _name;
        internal StringBuilder? _public;
        internal StringBuilder? _system;

        internal Doctype() { }

        internal bool Quirks => _quirks;
        internal string? Name => _name?.ToString();
        internal string? PublicIdentifier => _public?.ToString();
        internal string? SystemIdentifier => _system?.ToString();
        internal void SetQuirksFlag()
        {
            _quirks = true;
        }
        internal void AppendToName(char c)
        {
            if (_name == null)
                _name = new StringBuilder();
            _name.Append(c);
        }
        internal void AppendToName(string s)
        {
            if (_name == null)
                _name = new StringBuilder();
            _name.Append(s);
        }
        internal void AppendToPublicID(char c)
        {
            if (_public == null)
                _public = new StringBuilder();
            _public.Append(c);
        }
        internal void AppendToPublicID(string s)
        {
            if (_public == null)
                _public = new StringBuilder();
            _public.Append(s);
        }
        internal void AppendToSystemID(char c)
        {
            if (_system == null)
                _system = new StringBuilder();
            _system.Append(c);
        }
        internal void AppendToSystemID(string s)
        {
            if (_system == null)
                _system = new StringBuilder();
            _system.Append(s);
        }

        public override string ToString()
        {
            return string.Format(
                "Name:{0},Public:{1},System:{2},Quirks:{3}",
                _name == null ? "null" : $"\"{_name.ToString()}\"",
                _public == null ? "null" : $"\"{_public.ToString()}\"",
                _system == null ? "null" : $"\"{_system.ToString()}\"",
                _quirks);
        }
    }
}
