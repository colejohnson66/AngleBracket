/* ============================================================================
 * File:   Strings.cs
 * Author: Cole Johnson
 * ============================================================================
 * Purpose:
 *
 * Implements WHATWG Infra 4.6 "Strings"
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;

namespace AngleBracket.Infra
{
    internal static class Strings
    {
        internal static byte[] IsomorphicEncode(string s)
        {
            byte[] result = new byte[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                Debug.Assert(s[i] <= 0xFF);
                result[i] = (byte)s[i];
            }
            return result;
        }

        internal static bool IsAsciiString(string s)
        {
            foreach (char c in s)
            {
                if (!CodePoints.IsAsciiCodePoint(c))
                    return false;
            }
            return true;
        }

        // TODO: "ASCII lowercase"

        // TODO: "ASCII uppercase"

        // TODO: "ASCII encode"

        internal static string StripNewlines(string s)
        {
            StringBuilder str = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c != '\n' && c != '\r')
                    str.Append(c);
            }
            return str.ToString();
        }

        internal static string NormalizeNewlines(string s)
        {
            return s.Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }
}
