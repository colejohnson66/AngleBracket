/* ============================================================================
 * File:   CharHelpers.cs
 * Author: Cole Johnson
 * ============================================================================
 * Purpose:
 *
 * Implements WHATWG Infra 4.5 "Code points"
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

namespace AngleBracket
{
    internal static class CharHelpers
    {
        internal static bool IsSurrogate(int c)
        {
            // A surrogate is a code point that is in the range U+D800 to U+DFFF,
            //   inclusive.
            return c >= 0xD800 && c <= 0xDFFF;
        }
        internal static bool IsScalar(int c)
        {
            // A scalar value is a code point that is not a surrogate.

            // EOF is not a code point
            if (c == -1)
                return false;
            return !IsSurrogate(c);
        }
        internal static bool IsNoncharacter(int c)
        {
            // A noncharacter is a code point that is in the range U+FDD0 to U+FDEF,
            //   inclusive, or U+FFFE, U+FFFF, U+1FFFE, U+1FFFF, U+2FFFE, U+2FFFF,
            //   U+3FFFE, U+3FFFF, U+4FFFE, U+4FFFF, U+5FFFE, U+5FFFF, U+6FFFE,
            //   U+6FFFF, U+7FFFE, U+7FFFF, U+8FFFE, U+8FFFF, U+9FFFE, U+9FFFF,
            //   U+AFFFE, U+AFFFF, U+BFFFE, U+BFFFF, U+CFFFE, U+CFFFF, U+DFFFE,
            //   U+DFFFF, U+EFFFE, U+EFFFF, U+FFFFE, U+FFFFF, U+10FFFE, or U+10FFFF.

            // EOF is not a code point
            if (c == -1)
                return false;

            if (c >= 0xFDD0 && c <= 0xFDEF)
                return true;

            // everything else if of the form U+xxFFFE and U+xxFFFF
            uint lower16 = (uint)c & 0xFFFFu;
            uint upper = ((uint)c) >> 16;
            if (upper >= 0 && upper <= 0x10)
                return lower16 == 0xFFFEu || lower16 == 0xFFFF;

            return false;
        }
        internal static bool IsAsciiCodePoint(int c)
        {
            // An ASCII code point is a code point in the range U+0000 NULL to
            //   U+007F DELETE, inclusive.
            return c >= '\0' && c <= 0x7F;
        }
        internal static bool IsAsciiTabOrNewline(int c)
        {
            // An ASCII tab or newline is U+0009 TAB, U+000A LF, or U+000D CR.
            return c == '\t' || c == '\n' || c == '\r';
        }
        internal static bool IsAsciiWhitespace(int c)
        {
            // ASCII whitespace is U+0009 TAB, U+000A LF, U+000C FF, U+000D CR,
            //   or U+0020 SPACE.

            // Don't use Char.IsWhitespace as that counts (among others) U+2028
            //   and U+2029 as whitespace
            return c == '\t' || c == '\n' || c == '\f' || c == '\r' || c == ' ';
        }
        internal static bool IsC0Control(int c)
        {
            // A C0 control is a code point in the range U+0000 NULL to
            //    U+001F INFORMATION SEPARATOR ONE, inclusive.
            return c >= '\0' && c <= 0x1F;
        }
        internal static bool IsC0ControlOrSpace(int c)
        {
            // A C0 control or space is a C0 control or U+0020 SPACE.
            return IsC0Control(c) || c == ' ';
        }
        internal static bool IsControl(int c)
        {
            // A control is a C0 control or a code point in the range U+007F DELETE
            //   to U+009F APPLICATION PROGRAM COMMAND, inclusive.
            return IsC0Control(c) || (c >= 0x7F && c <= 0x9F);
        }
        internal static bool IsAsciiDigit(int c)
        {
            // An ASCII digit is a code point in the range U+0030 (0) to
            //   U+0039 (9), inclusive.
            return c >= '0' && c <= '9';
        }
        internal static bool IsAsciiUpperHexDigit(int c)
        {
            // An ASCII upper hex digit is an ASCII digit or a code point in the
            //   range U+0041 (A) to U+0046 (F), inclusive.
            return IsAsciiDigit(c) || (c >= 'A' && c <= 'F');
        }
        internal static bool IsAsciiLowerHexDigit(int c)
        {
            // An ASCII lower hex digit is an ASCII digit or a code point in the
            //   range U+0061 (a) to U+0066 (f), inclusive.
            return IsAsciiDigit(c) || (c >= 'a' && c <= 'f');
        }
        internal static bool IsAsciiHexDigit(int c)
        {
            // An ASCII hex digit is an ASCII upper hex digit or ASCII lower hex
            //   digit.
            return IsAsciiUpperHexDigit(c) || IsAsciiLowerHexDigit(c);
        }
        internal static bool IsAsciiAlphaUpper(int c)
        {
            // An ASCII upper alpha is a code point in the range U+0041 (A) to
            //   U+005A (Z), inclusive.
            return c >= 'A' && c <= 'Z';
        }
        internal static bool IsAsciiAlphaLower(int c)
        {
            // An ASCII lower alpha is a code point in the range U+0061 (a) to
            //   U+007A (z), inclusive.
            return c >= 'a' && c <= 'z';
        }
        internal static bool IsAsciiAlpha(int c)
        {
            // An ASCII alpha is an ASCII upper alpha or ASCII lower alpha.
            return IsAsciiAlphaUpper(c) || IsAsciiAlphaLower(c);
        }
        internal static bool IsAsciiAlphanumeric(int c)
        {
            // An ASCII alphanumeric is an ASCII digit or ASCII alpha.
            return IsAsciiDigit(c) || IsAsciiAlpha(c);
        }
    }
}
