/* ============================================================================
 * File:   Token.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace AngleBracket.Tokenizer
{
    internal struct Token
    {
        internal TokenType Type { get; }
        internal object? Value { get; }

        internal Token(TokenType type, object? value)
        {
            Contract.Requires(type != TokenType.Character || value is int);
            Contract.Requires(type != TokenType.Comment || value is string);
            Contract.Requires(type != TokenType.DocumentType || value is Doctype);
            Contract.Requires(type != TokenType.EndOfFile || value == null);
            Contract.Requires(type != TokenType.Tag || value is Tag);

            Type = type;
            Value = value;
        }

        internal static Token FromCharacter(int c) => new Token(TokenType.Character, c);
        internal static Token FromComment(string s) => new Token(TokenType.Comment, s);
        internal static Token FromDoctype(Doctype d) => new Token(TokenType.DocumentType, d);
        internal static Token FromEof() => new Token(TokenType.EndOfFile, null);
        internal static Token FromTag(Tag t) => new Token(TokenType.Tag, t);

        internal uint CharacterValue()
        {
            Contract.Requires(Type == TokenType.Character);
            Contract.Requires(Value is int);
            return (uint)Value!;
        }

        internal string CommentValue()
        {
            Contract.Requires(Type == TokenType.Comment);
            Contract.Requires(Value is string);
            return (string)Value!;
        }

        internal Doctype DoctypeValue()
        {
            Contract.Requires(Type == TokenType.DocumentType);
            Contract.Requires(Value is Doctype);
            return (Doctype)Value!;
        }

        internal Tag TagValue()
        {
            Contract.Requires(Type == TokenType.Tag);
            Contract.Requires(Value is Tag);
            return (Tag)Value!;
        }

        public override string ToString()
        {
            if (Type == TokenType.Character)
            {
                uint val = (uint)Value!;
                if (val == '\0')
                    return "Character{\\0}";
                if (val == '\r')
                    return "Character{\\r}";
                if (val == '\n')
                    return "Character{\\n}";
                if (val == '\t')
                    return "Character{\\t}";
                return string.Format("Character{{{0}}}", val);
            }
            if (Type == TokenType.Comment)
                return string.Format("Comment{{\"{0}\"}}", Value!);
            if (Type == TokenType.DocumentType)
                return string.Format("DocumentType{{{0}}}", Value!);
            if (Type == TokenType.EndOfFile)
                return "EndOfFile";
            Debug.Assert(Type == TokenType.Tag);
            return string.Format("Tag{{{0}}}", Value!);
        }
    }
}
