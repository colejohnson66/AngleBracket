/* ============================================================================
 * File:   HtmlTokenizer.cs
 * Author: Cole Johnson
 * ============================================================================
 * Purpose:
 *
 * Implements the HTML tokenization state machine as defined in WHATWG 13.2.5
 *   "Tokenization".
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
using AngleBracket.Infra;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;

namespace AngleBracket.Tokenizer
{
    internal delegate void ErrorHandler(ParseError error);

    internal class HtmlTokenizer
    {
        internal const int EOF = -1;
        internal const char REPLACEMENT_CHARACTER = '\xFFFD';

        private LineCountingReader _reader;
        private ErrorHandler? _errorHandler;

        internal TokenizerState State { get; private set; }
        private TokenizerState _returnState;
        private Tag? _lastEmittedStartTag;

        private Comment? _comment;
        private Tag? _tag;
        private List<int>? _tempBuf;
        private Attribute? _attr;
        private Doctype? _doctype;
        private int _charRefCode;

        private delegate Token[]? Handler(int c);
        private Dictionary<TokenizerState, Handler> _stateHandlerMap = new Dictionary<TokenizerState, Handler>();

        internal HtmlTokenizer(LineCountingReader reader)
        {
            _reader = reader;
            _errorHandler = null;

            // Must be added here instead of inline with the dictionary
            // For some reason, the compiler freaks out about instance delegates before the class is initialized
            // See: CS1950 and CS1921
            InitStateHandlerMap();
        }
        internal HtmlTokenizer(LineCountingReader reader, ErrorHandler errorHandler)
        {
            _reader = reader;
            _errorHandler = errorHandler;

            // Must be added here instead of inline with the dictionary
            // For some reason, the compiler freaks out about instance delegates before the class is initialized
            // See: CS1950 and CS1921
            InitStateHandlerMap();
        }

        #region Helper Functions
        private void InitStateHandlerMap()
        {
            _stateHandlerMap.Add(TokenizerState.Data, ParseData);
            _stateHandlerMap.Add(TokenizerState.RCDATA, ParseRCDATA);
            _stateHandlerMap.Add(TokenizerState.RAWTEXT, ParseRAWTEXT);
            _stateHandlerMap.Add(TokenizerState.ScriptData, ParseScriptData);
            _stateHandlerMap.Add(TokenizerState.PLAINTEXT, ParsePLAINTEXT);
            _stateHandlerMap.Add(TokenizerState.TagOpen, ParseTagOpen);
            _stateHandlerMap.Add(TokenizerState.EndTagOpen, ParseEndTagOpen);
            _stateHandlerMap.Add(TokenizerState.TagName, ParseTagName);

            _stateHandlerMap.Add(TokenizerState.RCDATALessThanSign, ParseRCDATALessThanSign);
            _stateHandlerMap.Add(TokenizerState.RCDATAEndTagOpen, ParseRCDATAEndTagOpen);
            _stateHandlerMap.Add(TokenizerState.RCDATAEndTagName, ParseRCDATAEndTagName);

            _stateHandlerMap.Add(TokenizerState.RAWTEXTLessThanSign, ParseRAWTEXTLessThanSign);
            _stateHandlerMap.Add(TokenizerState.RAWTEXTEndTagOpen, ParseRAWTEXTEndTagOpen);
            _stateHandlerMap.Add(TokenizerState.RAWTEXTEndTagName, ParseRAWTEXTEndTagName);

            _stateHandlerMap.Add(TokenizerState.ScriptDataLessThanSign, ParseScriptDataLessThanSign);
            _stateHandlerMap.Add(TokenizerState.ScriptDataEndTagOpen, ParseScriptDataEndTagOpen);
            _stateHandlerMap.Add(TokenizerState.ScriptDataEndTagName, ParseScriptDataEndTagName);
            _stateHandlerMap.Add(TokenizerState.ScriptDataEscapeStart, ParseScriptDataEscapeStart);
            _stateHandlerMap.Add(TokenizerState.ScriptDataEscapeStartDash, ParseScriptDataEscapeStartDash);
            _stateHandlerMap.Add(TokenizerState.ScriptDataEscaped, ParseScriptDataEscaped);
            _stateHandlerMap.Add(TokenizerState.ScriptDataEscapedDash, ParseScriptDataEscapedDash);
            _stateHandlerMap.Add(TokenizerState.ScriptDataEscapedDashDash, ParseScriptDataEscapedDashDash);
            _stateHandlerMap.Add(TokenizerState.ScriptDataEscapedLessThanSign, ParseScriptDataEscapedLessThanSign);
            _stateHandlerMap.Add(TokenizerState.ScriptDataEscapedEndTagOpen, ParseScriptDataEscapedEndTagOpen);
            _stateHandlerMap.Add(TokenizerState.ScriptDataEscapedEndTagName, ParseScriptDataEscapedEndTagName);

            _stateHandlerMap.Add(TokenizerState.ScriptDataDoubleEscapeStart, ParseScriptDataDoubleEscapeStart);
            _stateHandlerMap.Add(TokenizerState.ScriptDataDoubleEscaped, ParseScriptDataDoubleEscaped);
            _stateHandlerMap.Add(TokenizerState.ScriptDataDoubleEscapedDash, ParseScriptDataDoubleEscapedDash);
            _stateHandlerMap.Add(TokenizerState.ScriptDataDoubleEscapedDashDash, ParseScriptDataDoubleEscapedDashDash);
            _stateHandlerMap.Add(TokenizerState.ScriptDataDoubleEscapedLessThanSign, ParseScriptDataDoubleEscapedLessThanSign);
            _stateHandlerMap.Add(TokenizerState.ScriptDataDoubleEscapeEnd, ParseScriptDataDoubleEscapeEnd);

            _stateHandlerMap.Add(TokenizerState.BeforeAttributeName, ParseBeforeAttributeName);
            _stateHandlerMap.Add(TokenizerState.AttributeName, ParseAttributeName);
            _stateHandlerMap.Add(TokenizerState.AfterAttributeName, ParseAfterAttributeName);
            _stateHandlerMap.Add(TokenizerState.BeforeAttributeValue, ParseBeforeAttributeValue);
            _stateHandlerMap.Add(TokenizerState.AttributeValueDoubleQuoted, ParseAttributeValueDoubleQuoted);
            _stateHandlerMap.Add(TokenizerState.AttributeValueSingleQuoted, ParseAttributeValueSingleQuoted);
            _stateHandlerMap.Add(TokenizerState.AttributeValueUnquoted, ParseAttributeValueUnquoted);
            _stateHandlerMap.Add(TokenizerState.AfterAttributeValueQuoted, ParseAfterAttributeValueQuoted);

            _stateHandlerMap.Add(TokenizerState.SelfClosingStartTag, ParseSelfClosingStartTag);
            _stateHandlerMap.Add(TokenizerState.BogusComment, ParseBogusComment);
            _stateHandlerMap.Add(TokenizerState.MarkupDeclarationOpen, ParseMarkupDeclarationOpen);

            _stateHandlerMap.Add(TokenizerState.CommentStart, ParseCommentStart);
            _stateHandlerMap.Add(TokenizerState.CommentStartDash, ParseCommentStartDash);
            _stateHandlerMap.Add(TokenizerState.Comment, ParseComment);
            _stateHandlerMap.Add(TokenizerState.CommentLessThanSign, ParseCommentLessThanSign);
            _stateHandlerMap.Add(TokenizerState.CommentLessThanSignBang, ParseCommentLessThanSignBang);
            _stateHandlerMap.Add(TokenizerState.CommentLessThanSignBangDash, ParseCommentLessThanSignBangDash);
            _stateHandlerMap.Add(TokenizerState.CommentLessThanSignBangDashDash, ParseCommentLessThanSignBangDashDash);
            _stateHandlerMap.Add(TokenizerState.CommentEndDash, ParseCommentEndDash);
            _stateHandlerMap.Add(TokenizerState.CommentEnd, ParseCommentEnd);
            _stateHandlerMap.Add(TokenizerState.CommentEndBang, ParseCommentEndBang);

            _stateHandlerMap.Add(TokenizerState.DOCTYPE, ParseDOCTYPE);
            _stateHandlerMap.Add(TokenizerState.BeforeDOCTYPEName, ParseBeforeDOCTYPEName);
            _stateHandlerMap.Add(TokenizerState.DOCTYPEName, ParseDOCTYPEName);
            _stateHandlerMap.Add(TokenizerState.AfterDOCTYPEName, ParseAfterDOCTYPEName);
            _stateHandlerMap.Add(TokenizerState.AfterDOCTYPEPublicKeyword, ParseAfterDOCTYPEPublicKeyword);
            _stateHandlerMap.Add(TokenizerState.BeforeDOCTYPEPublicIdentifier, ParseBeforeDOCTYPEPublicIdentifier);
            _stateHandlerMap.Add(TokenizerState.DOCTYPEPublicIdentifierDoubleQuoted, ParseDOCTYPEPublicIdentifierDoubleQuoted);
            _stateHandlerMap.Add(TokenizerState.DOCTYPEPublicIdentifierSingleQuoted, ParseDOCTYPEPublicIdentifierSingleQuoted);
            _stateHandlerMap.Add(TokenizerState.AfterDOCTYPEPublicIdentifier, ParseAfterDOCTYPEPublicIdentifier);
            _stateHandlerMap.Add(TokenizerState.BetweenDOCTYPEPublicAndSystemIdentifiers, ParseBetweenDOCTYPEPublicAndSystemIdentifiers);
            _stateHandlerMap.Add(TokenizerState.AfterDOCTYPESystemKeyword, ParseAfterDOCTYPESystemKeyword);
            _stateHandlerMap.Add(TokenizerState.BeforeDOCTYPESystemIdentifier, ParseBeforeDOCTYPESystemIdentifier);
            _stateHandlerMap.Add(TokenizerState.DOCTYPESystemIdentifierDoubleQuoted, ParseDOCTYPESystemIdentifierDoubleQuoted);
            _stateHandlerMap.Add(TokenizerState.DOCTYPESystemIdentifierSingleQuoted, ParseDOCTYPESystemIdentifierSingleQuoted);
            _stateHandlerMap.Add(TokenizerState.AfterDOCTYPESystemIdentifier, ParseAfterDOCTYPESystemIdentifier);
            _stateHandlerMap.Add(TokenizerState.BogusDOCTYPE, ParseBogusDOCTYPE);

            _stateHandlerMap.Add(TokenizerState.CDATASection, ParseCDATASection);
            _stateHandlerMap.Add(TokenizerState.CDATASectionBracket, ParseCDATASectionBracket);
            _stateHandlerMap.Add(TokenizerState.CDATASectionEnd, ParseCDATASectionEnd);

            _stateHandlerMap.Add(TokenizerState.CharacterReference, ParseCharacterReference);
            _stateHandlerMap.Add(TokenizerState.NamedCharacterReference, ParseNamedCharacterReference);
            _stateHandlerMap.Add(TokenizerState.AmbiguousAmpersand, ParseAmbiguousAmpersand);
            _stateHandlerMap.Add(TokenizerState.NumericCharacterReference, ParseNumericCharacterReference);
            _stateHandlerMap.Add(TokenizerState.HexadecimalCharacterReferenceStart, ParseHexadecimalCharacterReferenceStart);
            _stateHandlerMap.Add(TokenizerState.DecimalCharacterReferenceStart, ParseDecimalCharacterReferenceStart);
            _stateHandlerMap.Add(TokenizerState.HexadecimalCharacterReference, ParseHexadecimalCharacterReference);
            _stateHandlerMap.Add(TokenizerState.DecimalCharacterReference, ParseDecimalCharacterReference);
            _stateHandlerMap.Add(TokenizerState.NumericCharacterReferenceEnd, ParseNumericCharacterReferenceEnd);
        }

        private string ReadString(int length)
        {
            Contract.Requires(length >= 0);

            int[] buffer = new int[length];
            int read = _reader.Read(buffer);
            Debug.Assert(length == read); // TODO: is this needed?
            // TODO: convert to string
            throw new NotImplementedException();
        }

        private Token GetCharacterToken(int c)
        {
            Contract.Requires(c >= 0 && c <= 0x10FFFF);
            return Token.FromCharacter(c);
        }
        private Token GetNullCharacterToken() => Token.FromCharacter('\0');
        private Token GetReplacementCharacterToken() => Token.FromCharacter(REPLACEMENT_CHARACTER);
        private Token GetEndOfFileToken() => Token.FromEof();

        private int GetLowercaseCharFromAsciiUpper(int c) => c + 0x20;
        private int GetUppercaseCharFromAsciiLower(int c) => c - 0x20;

        private void Error(ParseError error)
        {
            if (_errorHandler != null)
                _errorHandler(error);
        }

        private void ReconsumeAndAppend(List<Token> tokens, Token[]? reconsumedTokens)
        {
            if (reconsumedTokens != null)
                tokens.AddRange(reconsumedTokens);
        }

        private bool IsTabNewlineOrSpace(int c) => c == '\t' || c == '\n' || c == '\r' || c == ' ';

        private bool IsInAttributeState(TokenizerState returnState)
        {
            return returnState == TokenizerState.AttributeValueDoubleQuoted ||
                returnState == TokenizerState.AttributeValueSingleQuoted ||
                returnState == TokenizerState.AttributeValueUnquoted;
        }

        private bool IsAppropriateEndTagToken()
        {
            Debug.Assert(_tag != null);
            if (_lastEmittedStartTag == null)
                return false;
            return _tag.Name == _lastEmittedStartTag.Name;
        }

        private Token GetTagToken()
        {
            // If `_tag` is a start tag, save it in `_lastEmittedTag`
            Debug.Assert(_tag != null);

            Tag tag = _tag;
            _tag = null;
            if (!tag.IsEndTag)
                _lastEmittedStartTag = tag;
            return Token.FromTag(tag);
        }

        private Token GetCommentToken()
        {
            Debug.Assert(_comment != null);

            Comment comment = _comment;
            _comment = null;
            return Token.FromComment(comment);
        }

        private void AddTokensForTempBuffer(List<Token> tokens)
        {
            Debug.Assert(_tempBuf != null);
            foreach (int c in _tempBuf)
                tokens.Add(GetCharacterToken(c));
            _tempBuf = null;
        }

        private string TempBufferAsString()
        {
            return CodePoints.StringFromCodePoints(_tempBuf!.ToArray());
        }
        #endregion

        #region Tokenization Functions
        internal IEnumerable<Token> Tokenize()
        {
            // WHATWG 13.2.5 "Tokenization"
            State = TokenizerState.Data;
            _returnState = TokenizerState.Data;
            _comment = null;
            _tag = null;
            _tempBuf = null;
            _attr = null;
            _doctype = null;
            _charRefCode = 0;

            while (true)
            {
                int c = _reader.Read();
                Token[]? result = _stateHandlerMap[State](c);
                if (result == null)
                    continue;
                foreach (Token tok in result!)
                {
                    yield return tok;
                    if (tok.Type == TokenType.EndOfFile)
                        yield break;
                }
            }
        }

        private Token[]? ParseData(int c)
        {
            if (c == '&')
            {
                _returnState = TokenizerState.Data;
                State = TokenizerState.CharacterReference;
                return null;
            }

            if (c == '<')
            {
                State = TokenizerState.TagOpen;
                return null;
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                return new Token[] { GetCharacterToken(c) };
            }

            if (c == EOF)
                return new Token[] { GetEndOfFileToken() };

            return new Token[] { GetCharacterToken(c) };
        }

        private Token[]? ParseRCDATA(int c)
        {
            if (c == '&')
            {
                _returnState = TokenizerState.RCDATA;
                State = TokenizerState.CharacterReference;
                return null;
            }

            if (c == '<')
            {
                _returnState = TokenizerState.RCDATALessThanSign;
                return null;
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                return new Token[] { GetReplacementCharacterToken() };
            }

            if (c == EOF)
                return new Token[] { GetEndOfFileToken() };

            return new Token[] { GetCharacterToken(c) };
        }

        private Token[]? ParseRAWTEXT(int c)
        {
            if (c == '<')
            {
                State = TokenizerState.RAWTEXT;
                return null;
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                return new Token[] { GetReplacementCharacterToken() };
            }

            if (c == EOF)
                return new Token[] { GetEndOfFileToken() };

            return new Token[] { GetCharacterToken(c) };
        }

        private Token[]? ParseScriptData(int c)
        {
            if (c == '<')
            {
                State = TokenizerState.ScriptDataLessThanSign;
                return null;
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                return new Token[] { GetReplacementCharacterToken() };
            }

            if (c == EOF)
                return new Token[] { GetEndOfFileToken() };

            return new Token[] { GetCharacterToken(c) };
        }

        private Token[]? ParsePLAINTEXT(int c)
        {
            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                return new Token[] { GetReplacementCharacterToken() };
            }

            if (c == EOF)
                return new Token[] { GetEndOfFileToken() };

            return new Token[] { GetCharacterToken(c) };
        }

        private Token[]? ParseTagOpen(int c)
        {
            if (c == '!')
            {
                State = TokenizerState.MarkupDeclarationOpen;
                return null;
            }

            if (c == '/')
            {
                State = TokenizerState.EndTagOpen;
                return null;
            }

            if (CodePoints.IsAsciiAlpha(c))
            {
                _tag = new Tag();
                return ParseTagName(c);
            }

            if (c == '?')
            {
                Error(ParseError.UnexpectedQuestionMarkInsteadOfTagName);
                _comment = new Comment();
                return ParseBogusComment(c);
            }

            if (c == EOF)
            {
                Error(ParseError.EofBeforeTagName);
                return new Token[] {
                    GetCharacterToken('<'),
                    GetEndOfFileToken(),
                };
            }

            Error(ParseError.InvalidFirstCharacterOfTagName);
            List<Token> tokens = new List<Token>() { GetCharacterToken('<') };
            ReconsumeAndAppend(tokens, ParseData(c));
            return tokens.ToArray();
        }

        private Token[]? ParseEndTagOpen(int c)
        {
            if (CodePoints.IsAsciiAlpha(c))
            {
                _tag = new Tag();
                _tag.SetEndTagFlag();
                return ParseTagName(c);
            }

            if (c == '>')
            {
                Error(ParseError.MissingEndTagName);
                State = TokenizerState.Data;
                return null;
            }

            if (c == EOF)
            {
                Error(ParseError.EofBeforeTagName);
                return new Token[] {
                    GetCharacterToken('<'),
                    GetCharacterToken('/'),
                    GetEndOfFileToken(),
                };
            }

            Error(ParseError.InvalidFirstCharacterOfTagName);
            _comment = new Comment();
            return ParseBogusComment(c);
        }

        private Token[]? ParseTagName(int c)
        {
            if (IsTabNewlineOrSpace(c))
            {
                State = TokenizerState.BeforeAttributeName;
                return null;
            }

            if (c == '/')
            {
                State = TokenizerState.SelfClosingStartTag;
                return null;
            }

            if (c == '>')
            {
                State = TokenizerState.Data;
                return new Token[] { GetTagToken() };
            }

            if (CodePoints.IsAsciiAlphaUpper(c))
            {
                _tag!.AppendToName(GetLowercaseCharFromAsciiUpper(c));
                return null;
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                _tag!.AppendToName(REPLACEMENT_CHARACTER);
                return null;
            }

            if (c == EOF)
            {
                Error(ParseError.EofInTag);
                return new Token[] { GetEndOfFileToken() };
            }

            _tag!.AppendToName(c);
            return null;
        }

        private Token[]? ParseRCDATALessThanSign(int c)
        {
            if (c == '/')
            {
                _tempBuf = new List<int>();
                State = TokenizerState.RCDATAEndTagOpen;
                return null;
            }

            List<Token> tokens = new List<Token>() { GetCharacterToken('<') };
            ReconsumeAndAppend(tokens, ParseRCDATA(c));
            return tokens.ToArray();
        }

        private Token[]? ParseRCDATAEndTagOpen(int c)
        {
            if (CodePoints.IsAsciiAlpha(c))
            {
                _tag = new Tag();
                _tag.SetEndTagFlag();
                return ParseRCDATAEndTagName(c);
            }

            List<Token> tokens = new List<Token>() {
                GetCharacterToken('<'),
                GetCharacterToken('/'),
            };
            ReconsumeAndAppend(tokens, ParseRCDATA(c));
            return tokens.ToArray();
        }

        private Token[]? ParseRCDATAEndTagName(int c)
        {
            if (IsTabNewlineOrSpace(c))
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.BeforeAttributeName;
                    return null;
                }
            }

            if (c == '/')
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.SelfClosingStartTag;
                    return null;
                }
            }

            if (c == '>')
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.Data;
                    return new Token[] { GetTagToken() };
                }
            }

            if (CodePoints.IsAsciiAlphaUpper(c))
            {
                _tag!.AppendToName(GetLowercaseCharFromAsciiUpper(c));
                _tempBuf!.Add(c);
                return null;
            }

            if (CodePoints.IsAsciiAlphaLower(c))
            {
                _tag!.AppendToName(c);
                _tempBuf!.Add(c);
                return null;
            }

            List<Token> tokens = new List<Token>() {
                GetCharacterToken('<'),
                GetCharacterToken('/'),
            };
            AddTokensForTempBuffer(tokens);
            ReconsumeAndAppend(tokens, ParseRCDATA(c));
            return tokens.ToArray();
        }

        private Token[]? ParseRAWTEXTLessThanSign(int c)
        {
            if (c == '/')
            {
                _tempBuf = new List<int>();
                State = TokenizerState.RAWTEXTEndTagOpen;
                return null;
            }

            List<Token> tokens = new List<Token>() { GetCharacterToken('<') };
            ReconsumeAndAppend(tokens, ParseRAWTEXT(c));
            return tokens.ToArray();
        }

        private Token[]? ParseRAWTEXTEndTagOpen(int c)
        {
            if (CodePoints.IsAsciiAlpha(c))
            {
                _tag = new Tag();
                _tag.SetEndTagFlag();
                return ParseRAWTEXTEndTagName(c);
            }

            List<Token> tokens = new List<Token>() {
                GetCharacterToken('<'),
                GetCharacterToken('/'),
            };
            ReconsumeAndAppend(tokens, ParseRAWTEXT(c));
            return tokens.ToArray();
        }

        private Token[]? ParseRAWTEXTEndTagName(int c)
        {
            if (IsTabNewlineOrSpace(c))
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.BeforeAttributeName;
                    return null;
                }
            }

            if (c == '/')
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.SelfClosingStartTag;
                    return null;
                }
            }

            if (c == '>')
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.Data;
                    return new Token[] { GetTagToken() };
                }
            }

            if (CodePoints.IsAsciiAlphaUpper(c))
            {
                _tag!.AppendToName(GetLowercaseCharFromAsciiUpper(c));
                _tempBuf!.Add(c);
                return null;
            }

            if (CodePoints.IsAsciiAlphaLower(c))
            {
                _tag!.AppendToName(c);
                _tempBuf!.Add(c);
                return null;
            }

            List<Token> tokens = new List<Token>() {
                GetCharacterToken('<'),
                GetCharacterToken('/'),
            };
            AddTokensForTempBuffer(tokens);
            ReconsumeAndAppend(tokens, ParseRAWTEXT(c));
            return tokens.ToArray();
        }

        private Token[]? ParseScriptDataLessThanSign(int c)
        {
            if (c == '/')
            {
                _tempBuf = new List<int>();
                State = TokenizerState.ScriptDataEndTagOpen;
                return null;
            }

            if (c == '!')
            {
                State = TokenizerState.ScriptDataEscapeStart;
                return new Token[] {
                    GetCharacterToken('<'),
                    GetCharacterToken('!'),
                };
            }

            List<Token> tokens = new List<Token>() { GetCharacterToken('<') };
            ReconsumeAndAppend(tokens, ParseScriptData(c));
            return tokens.ToArray();
        }

        private Token[]? ParseScriptDataEndTagOpen(int c)
        {
            if (CodePoints.IsAsciiAlpha(c))
            {
                _tag = new Tag();
                _tag.SetEndTagFlag();
                return ParseScriptDataEndTagName(c);
            }

            List<Token> tokens = new List<Token>() {
                GetCharacterToken('<'),
                GetCharacterToken('/'),
            };
            ReconsumeAndAppend(tokens, ParseScriptData(c));
            return tokens.ToArray();
        }

        private Token[]? ParseScriptDataEndTagName(int c)
        {
            if (IsTabNewlineOrSpace(c))
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.BeforeAttributeName;
                    return null;
                }
            }

            if (c == '/')
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.SelfClosingStartTag;
                    return null;
                }
            }

            if (c == '>')
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.Data;
                    return null;
                }
            }

            if (CodePoints.IsAsciiAlphaUpper(c))
            {
                _tag!.AppendToName(GetLowercaseCharFromAsciiUpper(c));
                _tempBuf!.Add(c);
                return null;
            }

            if (CodePoints.IsAsciiAlphaLower(c))
            {
                _tag!.AppendToName(c);
                _tempBuf!.Add(c);
                return null;
            }

            List<Token> tokens = new List<Token>() {
                GetCharacterToken('<'),
                GetCharacterToken('/'),
            };
            AddTokensForTempBuffer(tokens);
            ReconsumeAndAppend(tokens, ParseScriptData(c));
            return tokens.ToArray();
        }

        private Token[]? ParseScriptDataEscapeStart(int c)
        {
            if (c == '-')
            {
                State = TokenizerState.ScriptDataEscapeStartDash;
                return new Token[] { GetCharacterToken('-') };
            }

            return ParseScriptData(c);
        }

        private Token[]? ParseScriptDataEscapeStartDash(int c)
        {
            if (c == '-')
            {
                State = TokenizerState.ScriptDataEscapedDashDash;
                return new Token[] { GetCharacterToken('-') };
            }

            return ParseScriptData(c);
        }

        private Token[]? ParseScriptDataEscaped(int c)
        {
            if (c == '-')
            {
                State = TokenizerState.ScriptDataEscapedDash;
                return new Token[] { GetCharacterToken('-') };
            }

            if (c == '<')
            {
                State = TokenizerState.ScriptDataEscapedLessThanSign;
                return null;
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                return new Token[] { GetReplacementCharacterToken() };
            }

            if (c == EOF)
            {
                Error(ParseError.EofInScriptHtmlCommentLikeText);
                return new Token[] { GetEndOfFileToken() };
            }

            return new Token[] { GetCharacterToken(c) };
        }

        private Token[]? ParseScriptDataEscapedDash(int c)
        {
            if (c == '-')
            {
                State = TokenizerState.ScriptDataEscapedDashDash;
                return new Token[] { GetCharacterToken('-') };
            }

            if (c == '<')
            {
                State = TokenizerState.ScriptDataEscapedLessThanSign;
                return null;
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                State = TokenizerState.ScriptDataEscaped;
                return new Token[] { GetReplacementCharacterToken() };
            }

            if (c == EOF)
            {
                Error(ParseError.EofInScriptHtmlCommentLikeText);
                return new Token[] { GetEndOfFileToken() };
            }

            State = TokenizerState.ScriptDataEscaped;
            return new Token[] { GetCharacterToken(c) };
        }

        private Token[]? ParseScriptDataEscapedDashDash(int c)
        {
            if (c == '-')
                return new Token[] { GetCharacterToken('-') };

            if (c == '<')
            {
                State = TokenizerState.ScriptDataEscapedLessThanSign;
                return null;
            }

            if (c == '>')
            {
                State = TokenizerState.ScriptData;
                return new Token[] { GetCharacterToken('>') };
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                State = TokenizerState.ScriptDataEscaped;
                return new Token[] { GetReplacementCharacterToken() };
            }

            if (c == EOF)
            {
                Error(ParseError.EofInScriptHtmlCommentLikeText);
                return new Token[] { GetEndOfFileToken() };
            }

            State = TokenizerState.ScriptDataEscaped;
            return new Token[] { GetCharacterToken(c) };
        }

        private Token[]? ParseScriptDataEscapedLessThanSign(int c)
        {
            if (c == '/')
            {
                _tempBuf = new List<int>();
                State = TokenizerState.ScriptDataEscapedEndTagOpen;
                return null;
            }

            if (CodePoints.IsAsciiAlpha(c))
            {
                _tempBuf = new List<int>();
                List<Token> tokensInner = new List<Token>() { GetCharacterToken('<') };
                ReconsumeAndAppend(tokensInner, ParseScriptDataDoubleEscapeStart(c));
                return tokensInner.ToArray();
            }

            List<Token> tokens = new List<Token>() { GetCharacterToken('<') };
            ReconsumeAndAppend(tokens, ParseScriptDataEscaped(c));
            return tokens.ToArray();
        }

        private Token[]? ParseScriptDataEscapedEndTagOpen(int c)
        {
            if (CodePoints.IsAsciiAlpha(c))
            {
                _tag = new Tag();
                _tag.SetEndTagFlag();
                return ParseScriptDataEscapedEndTagName(c);
            }

            List<Token> tokens = new List<Token>() {
                GetCharacterToken('<'),
                GetCharacterToken('/'),
            };
            ReconsumeAndAppend(tokens, ParseScriptDataEscaped(c));
            return tokens.ToArray();
        }

        private Token[]? ParseScriptDataEscapedEndTagName(int c)
        {
            if (IsTabNewlineOrSpace(c))
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.BeforeAttributeName;
                    return null;
                }
            }

            if (c == '/')
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.SelfClosingStartTag;
                    return null;
                }
            }

            if (c == '>')
            {
                if (IsAppropriateEndTagToken())
                {
                    State = TokenizerState.Data;
                    return new Token[] { GetTagToken() };
                }
            }

            if (CodePoints.IsAsciiAlphaUpper(c))
            {
                _tag!.AppendToName(GetLowercaseCharFromAsciiUpper(c));
                _tempBuf!.Add(c);
                return null;
            }

            if (CodePoints.IsAsciiAlphaLower(c))
            {
                _tag!.AppendToName(c);
                _tempBuf!.Add(c);
                return null;
            }

            List<Token> tokens = new List<Token>() {
                GetCharacterToken('<'),
                GetCharacterToken('/'),
            };
            AddTokensForTempBuffer(tokens);
            ReconsumeAndAppend(tokens, ParseScriptDataEscaped(c));
            return tokens.ToArray();
        }

        private Token[]? ParseScriptDataDoubleEscapeStart(int c)
        {
            if (IsTabNewlineOrSpace(c) || c == '/' || c == '>')
            {
                if (TempBufferAsString() == "script")
                    State = TokenizerState.ScriptDataDoubleEscaped;
                else
                    State = TokenizerState.ScriptDataEscaped;
                return new Token[] { GetCharacterToken(c) };
            }

            if (CodePoints.IsAsciiAlphaUpper(c))
            {
                _tempBuf!.Add(GetLowercaseCharFromAsciiUpper(c));
                return new Token[] { GetCharacterToken(c) };
            }

            if (CodePoints.IsAsciiAlphaLower(c))
            {
                _tempBuf!.Add(c);
                return new Token[] { GetCharacterToken(c) };
            }

            return ParseScriptDataEscaped(c);
        }

        private Token[]? ParseScriptDataDoubleEscaped(int c)
        {
            if (c == '-')
            {
                State = TokenizerState.ScriptDataDoubleEscapedDash;
                return new Token[] { GetCharacterToken('-') };
            }

            if (c == '<')
            {
                State = TokenizerState.ScriptDataDoubleEscapedLessThanSign;
                return new Token[] { GetCharacterToken('<') };
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                return new Token[] { GetReplacementCharacterToken() };
            }

            if (c == EOF)
            {
                Error(ParseError.EofInScriptHtmlCommentLikeText);
                return new Token[] { GetEndOfFileToken() };
            }

            return new Token[] { GetCharacterToken(c) };
        }

        private Token[]? ParseScriptDataDoubleEscapedDash(int c)
        {
            if (c == '-')
            {
                State = TokenizerState.ScriptDataDoubleEscapedDashDash;
                return new Token[] { GetCharacterToken('-') };
            }

            if (c == '<')
            {
                State = TokenizerState.ScriptDataDoubleEscapedLessThanSign;
                return new Token[] { GetCharacterToken('<') };
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                State = TokenizerState.ScriptDataDoubleEscaped;
                return new Token[] { GetReplacementCharacterToken() };
            }

            if (c == EOF)
            {
                Error(ParseError.EofInScriptHtmlCommentLikeText);
                return new Token[] { GetEndOfFileToken() };
            }

            State = TokenizerState.ScriptDataDoubleEscaped;
            return new Token[] { GetCharacterToken(c) };
        }

        private Token[]? ParseScriptDataDoubleEscapedDashDash(int c)
        {
            if (c == '-')
                return new Token[] { GetCharacterToken('-') };

            if (c == '<')
            {
                State = TokenizerState.ScriptDataDoubleEscapedLessThanSign;
                return new Token[] { GetCharacterToken('<') };
            }

            if (c == '>')
            {
                State = TokenizerState.ScriptData;
                return new Token[] { GetCharacterToken('>') };
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                State = TokenizerState.ScriptDataDoubleEscaped;
                return new Token[] { GetReplacementCharacterToken() };
            }

            if (c == EOF)
            {
                Error(ParseError.EofInScriptHtmlCommentLikeText);
                return new Token[] { GetEndOfFileToken() };
            }

            State = TokenizerState.ScriptDataDoubleEscaped;
            return new Token[] { GetCharacterToken(c) };
        }

        private Token[]? ParseScriptDataDoubleEscapedLessThanSign(int c)
        {
            if (c == '/')
            {
                _tempBuf = new List<int>();
                State = TokenizerState.ScriptDataDoubleEscapeEnd;
                return new Token[] { GetCharacterToken('/') };
            }

            return ParseScriptDataDoubleEscaped(c);
        }

        private Token[]? ParseScriptDataDoubleEscapeEnd(int c)
        {
            if (IsTabNewlineOrSpace(c) || c == '/' || c == '>')
            {
                if (TempBufferAsString() == "script")
                    State = TokenizerState.ScriptDataEscaped;
                else
                    State = TokenizerState.ScriptDataDoubleEscaped;
                return new Token[] { GetCharacterToken(c) };
            }

            if (CodePoints.IsAsciiAlphaUpper(c))
            {
                _tempBuf!.Add(GetLowercaseCharFromAsciiUpper(c));
                return new Token[] { GetCharacterToken(c) };
            }

            if (CodePoints.IsAsciiAlphaLower(c))
            {
                _tempBuf!.Add(c);
                return new Token[] { GetCharacterToken(c) };
            }

            return ParseScriptDataDoubleEscaped(c);
        }

        private Token[]? ParseBeforeAttributeName(int c)
        {
            if (IsTabNewlineOrSpace(c))
                return null;

            if (c == '/' || c == '>' || c == EOF)
                return ParseAfterAttributeName(c);

            if (c == '=')
            {
                Error(ParseError.UnexpectedEqualsSignBeforeAttributeName);
                _attr = new Attribute();
                _tag!.AddAttribute(_attr);
                _attr.AppendToName(c);
                State = TokenizerState.AttributeName;
                return null;
            }

            _attr = new Attribute();
            _tag!.AddAttribute(_attr);
            return ParseAttributeName(c);
        }

        private Token[]? ParseAttributeName(int c)
        {
            if (IsTabNewlineOrSpace(c) || c == '/' || c == '>')
                return ParseAfterAttributeName(c);

            if (c == '=')
            {
                State = TokenizerState.BeforeAttributeValue;
                return null;
            }

            if (CodePoints.IsAsciiAlphaUpper(c))
            {
                _attr!.AppendToName(GetLowercaseCharFromAsciiUpper(c));
                return null;
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                _attr!.AppendToName(REPLACEMENT_CHARACTER);
                return null;
            }

            if (c == '"' || c == '\'' || c == '<')
            {
                Error(ParseError.UnexpectedCharacterInAttributeName);
                // "Treat is as per the "anything else" entry below."
            }

            _attr!.AppendToName(c);
            return null;
            // TODO: when leaving this state, and before emitting the tag,
            // duplicate attributes MUST be handled. Attempting to add a
            // duplicate attribute is a `ParseError.DuplicateAttribute`.
            // Drop the NEW duplicate attribute (first one survives)
        }

        private Token[]? ParseAfterAttributeName(int c)
        {
            if (IsTabNewlineOrSpace(c))
                return null;

            if (c == '/')
            {
                State = TokenizerState.SelfClosingStartTag;
                return null;
            }

            if (c == '=')
            {
                State = TokenizerState.BeforeAttributeValue;
                return null;
            }

            if (c == '>')
            {
                State = TokenizerState.Data;
                return new Token[] { GetTagToken() };
            }

            if (c == EOF)
            {
                Error(ParseError.EofInTag);
                return new Token[] { GetTagToken() };
            }

            _attr = new Attribute();
            _tag!.AddAttribute(_attr);
            return ParseAttributeName(c);
        }

        private Token[]? ParseBeforeAttributeValue(int c)
        {
            if (IsTabNewlineOrSpace(c))
                return null;

            if (c == '"')
            {
                State = TokenizerState.AttributeValueDoubleQuoted;
                return null;
            }

            if (c == '\'')
            {
                State = TokenizerState.AttributeValueSingleQuoted;
                return null;
            }

            if (c == '>')
            {
                Error(ParseError.MissingAttributeValue);
                State = TokenizerState.Data;
                return new Token[] { GetTagToken() };
            }

            return ParseAttributeValueUnquoted(c);
        }

        private Token[]? ParseAttributeValueDoubleQuoted(int c)
        {
            if (c == '"')
            {
                State = TokenizerState.AfterAttributeValueQuoted;
                return null;
            }

            if (c == '&')
            {
                _returnState = TokenizerState.AttributeValueDoubleQuoted;
                State = TokenizerState.CharacterReference;
                return null;
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                _attr!.AppendToValue(REPLACEMENT_CHARACTER);
                return null;
            }

            if (c == EOF)
            {
                Error(ParseError.EofInTag);
                return new Token[] { GetEndOfFileToken() };
            }

            _attr!.AppendToValue(c);
            return null;
        }

        private Token[]? ParseAttributeValueSingleQuoted(int c)
        {
            if (c == '\'')
            {
                State = TokenizerState.AfterAttributeValueQuoted;
                return null;
            }

            if (c == '&')
            {
                _returnState = TokenizerState.AttributeValueSingleQuoted;
                State = TokenizerState.CharacterReference;
                return null;
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                _attr!.AppendToValue(REPLACEMENT_CHARACTER);
                return null;
            }

            if (c == EOF)
            {
                Error(ParseError.EofInTag);
                return new Token[] { GetEndOfFileToken() };
            }

            _attr!.AppendToValue(c);
            return null;
        }

        private Token[]? ParseAttributeValueUnquoted(int c)
        {
            if (IsTabNewlineOrSpace(c))
            {
                State = TokenizerState.BeforeAttributeName;
                return null;
            }

            if (c == '&')
            {
                _returnState = TokenizerState.AttributeValueUnquoted;
                State = TokenizerState.CharacterReference;
                return null;
            }

            if (c == '>')
            {
                State = TokenizerState.Data;
                return new Token[] { GetTagToken() };
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                _attr!.AppendToValue(REPLACEMENT_CHARACTER);
                return null;
            }

            if (c == '"' || c == '\'' || c == '<' || c == '=' || c == '`')
            {
                Error(ParseError.UnexpectedCharacterInUnquotedAttributeValue);
                // "Treat is as per the "anything else" entry below."
            }

            if (c == EOF)
            {
                Error(ParseError.EofInTag);
                return new Token[] { GetEndOfFileToken() };
            }

            _attr!.AppendToValue(c);
            return null;
        }

        private Token[]? ParseAfterAttributeValueQuoted(int c)
        {
            if (IsTabNewlineOrSpace(c))
            {
                State = TokenizerState.BeforeAttributeName;
                return null;
            }

            if (c == '/')
            {
                State = TokenizerState.SelfClosingStartTag;
                return null;
            }

            if (c == '>')
            {
                State = TokenizerState.Data;
                return new Token[] { GetTagToken() };
            }

            if (c == EOF)
            {
                Error(ParseError.EofInTag);
                return new Token[] { GetEndOfFileToken() };
            }

            Error(ParseError.MissingWhitespaceBetweenAttributes);
            return ParseBeforeAttributeName(c);
        }

        private Token[]? ParseSelfClosingStartTag(int c)
        {
            if (c == '>')
            {
                _tag!.SetSelfClosingFlag();
                State = TokenizerState.Data;
                return new Token[] { GetTagToken() };
            }

            if (c == EOF)
            {
                Error(ParseError.EofInTag);
                return new Token[] { GetEndOfFileToken() };
            }

            Error(ParseError.UnexpectedSolidusInTag);
            return ParseBeforeAttributeName(c);
        }

        private Token[]? ParseBogusComment(int c)
        {
            if (c == '>')
            {
                State = TokenizerState.Data;
                return new Token[] { GetCommentToken() };
            }

            if (c == EOF)
            {
                return new Token[] {
                    GetCommentToken(),
                    GetEndOfFileToken(),
                };
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                _comment!.Append(REPLACEMENT_CHARACTER);
                return null;
            }

            _comment!.Append(c);
            return null;
        }

        private Token[]? ParseMarkupDeclarationOpen(int c)
        {
            if (c == '-' && _reader.Peek() == '-')
            {
                _reader.Consume();
                _comment = new Comment();
                State = TokenizerState.CommentStart;
                return null;
            }

            int[] peeked = new int[6];
            if (_reader.Peek(peeked) == 6)
            {
                if (c == 'D' && peeked[0] == 'O' && peeked[1] == 'C' &&
                    peeked[2] == 'T' && peeked[3] == 'Y' &&
                    peeked[4] == 'P' && peeked[5] == 'E')
                {
                    _reader.Consume(6);
                    State = TokenizerState.DOCTYPE;
                    return null;
                }

                if (c == '[' && peeked[0] == 'C' && peeked[1] == 'D' &&
                    peeked[2] == 'A' && peeked[3] == 'T' &&
                    peeked[4] == 'A' && peeked[5] == '[')
                {
                    _reader.Consume(6);
                    // TODO: If there is an "adjusted current node" and it
                    // is not an element in the HTML namespace, then switch
                    // to the CDATA section state. Otherwise, this is a
                    // `ParseError.CDataInHtmlContent`. Create a comment
                    // token whose data is "[CDATA[" and switch to
                    // `TokenizerState.BogusComment`.
                    throw new NotImplementedException();
                }
            }

            Error(ParseError.IncorrectlyOpenedComment);
            _comment = new Comment();
            State = TokenizerState.BogusComment;
            // NOTE: standard says to not consume anything in this state,
            //   so rather than `Backtrack()`, only for it to be read again,
            //   just reconsume it in the new state
            return ParseBogusComment(c);
        }

        private Token[]? ParseCommentStart(int c)
        {
            if (c == '-')
            {
                State = TokenizerState.CommentStartDash;
                return null;
            }

            if (c == '>')
            {
                Error(ParseError.AbruptClosingOfEmptyComment);
                State = TokenizerState.Data;
                return new Token[] { GetCommentToken() };
            }

            return ParseComment(c);
        }

        private Token[]? ParseCommentStartDash(int c)
        {
            if (c == '-')
            {
                State = TokenizerState.CommentEnd;
                return null;
            }

            if (c == '>')
            {
                Error(ParseError.AbruptClosingOfEmptyComment);
                State = TokenizerState.Data;
                return new Token[] { GetCommentToken() };
            }

            if (c == EOF)
            {
                Error(ParseError.EofInComment);
                return new Token[] {
                    GetCommentToken(),
                    GetEndOfFileToken(),
                };
            }

            _comment!.Append('-');
            return ParseComment(c);
        }

        private Token[]? ParseComment(int c)
        {
            if (c == '<')
            {
                _comment!.Append(c);
                State = TokenizerState.CommentLessThanSign;
                return null;
            }

            if (c == '-')
            {
                State = TokenizerState.CommentEndDash;
                return null;
            }

            if (c == '\0')
            {
                Error(ParseError.UnexpectedNullCharacter);
                _comment!.Append(REPLACEMENT_CHARACTER);
                return null;
            }

            if (c == EOF)
            {
                Error(ParseError.EofInComment);
                return new Token[] {
                    GetCommentToken(),
                    GetEndOfFileToken(),
                };
            }

            _comment!.Append(c);
            return null;
        }

        private Token[]? ParseCommentLessThanSign(int c)
        {
            if (c == '!')
            {
                _comment!.Append(c);
                State = TokenizerState.CommentLessThanSignBang;
                return null;
            }

            if (c == '<')
            {
                _comment!.Append(c);
                return null;
            }

            return ParseComment(c);
        }

        private Token[]? ParseCommentLessThanSignBang(int c)
        {
            if (c == '-')
            {
                State = TokenizerState.CommentLessThanSignBangDash;
                return null;
            }

            return ParseComment(c);
        }

        private Token[]? ParseCommentLessThanSignBangDash(int c)
        {
            if (c == '-')
            {
                State = TokenizerState.CommentLessThanSignBangDashDash;
                return null;
            }

            return ParseCommentEndDash(c);
        }

        private Token[]? ParseCommentLessThanSignBangDashDash(int c)
        {
            if (c == '>' || c == EOF)
                return ParseCommentEnd(c);

            Error(ParseError.NestedComment);
            return ParseCommentEnd(c);
        }

        private Token[]? ParseCommentEndDash(int c)
        {
            if (c == '-')
            {
                State = TokenizerState.CommentEnd;
                return null;
            }

            if (c == EOF)
            {
                Error(ParseError.EofInComment);
                return new Token[] {
                    GetCommentToken(),
                    GetEndOfFileToken(),
                };
            }

            _comment!.Append('-');
            return ParseComment(c);
        }

        private Token[]? ParseCommentEnd(int c)
        {
            if (c == '>')
            {
                State = TokenizerState.Data;
                return new Token[] { GetCommentToken() };
            }

            if (c == '!')
            {
                State = TokenizerState.CommentEndBang;
                return null;
            }

            if (c == '-')
            {
                _comment!.Append('-');
                return null;
            }

            if (c == EOF)
            {
                Error(ParseError.EofInComment);
                return new Token[] {
                    GetCommentToken(),
                    GetEndOfFileToken(),
                };
            }

            _comment!.Append("--");
            return ParseComment(c);
        }

        private Token[]? ParseCommentEndBang(int c)
        {
            if (c == '-')
            {
                _comment!.Append("--!");
                State = TokenizerState.CommentEndDash;
                return null;
            }

            if (c == '>')
            {
                Error(ParseError.IncorrectlyClosedComment);
                State = TokenizerState.Data;
                return new Token[] { GetCommentToken() };
            }

            if (c == EOF)
            {
                Error(ParseError.EofInComment);
                return new Token[] {
                    GetCommentToken(),
                    GetEndOfFileToken(),
                };
            }

            _comment!.Append("--!");
            return ParseComment(c);
        }

        private Token[]? ParseDOCTYPE(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseBeforeDOCTYPEName(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseDOCTYPEName(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseAfterDOCTYPEName(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseAfterDOCTYPEPublicKeyword(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseBeforeDOCTYPEPublicIdentifier(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseDOCTYPEPublicIdentifierDoubleQuoted(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseDOCTYPEPublicIdentifierSingleQuoted(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseAfterDOCTYPEPublicIdentifier(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseBetweenDOCTYPEPublicAndSystemIdentifiers(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseAfterDOCTYPESystemKeyword(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseBeforeDOCTYPESystemIdentifier(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseDOCTYPESystemIdentifierDoubleQuoted(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseDOCTYPESystemIdentifierSingleQuoted(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseAfterDOCTYPESystemIdentifier(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseBogusDOCTYPE(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseCDATASection(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseCDATASectionBracket(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseCDATASectionEnd(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseCharacterReference(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseNamedCharacterReference(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseAmbiguousAmpersand(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseNumericCharacterReference(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseHexadecimalCharacterReferenceStart(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseDecimalCharacterReferenceStart(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseHexadecimalCharacterReference(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseDecimalCharacterReference(int c)
        {
            throw new NotImplementedException();
        }

        private Token[]? ParseNumericCharacterReferenceEnd(int c)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
