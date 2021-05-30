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
        private LineCountingReader _reader;
        private ErrorHandler? _errorHandler;

        internal TokenizerState State { get; private set; }
        private TokenizerState _returnState;
        private Tag? _lastEmittedTag;

        private Comment? _comment;
        private Tag? _tag;
        private StringBuilder? _tempBuf;
        private Attribute? _attr;
        private Doctype? _doctype;
        private int _charRefCode;

        private delegate IEnumerable<Token> Handler(int c);
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
            return Token.FromCharacter((char)c);
        }
        private Token GetNullCharacterToken() => Token.FromCharacter('\0');
        private Token GetReplacementCharacterToken() => Token.FromCharacter('\xFFFD');
        private Token GetEndOfFileToken() => Token.FromEof();

        private uint GetLowercaseCharFromAsciiUpper(uint c) => c + 0x20;
        private uint GetUppercaseCharFromAsciiLower(uint c) => c - 0x20;

        private void Error(ParseError error)
        {
            if (_errorHandler != null)
                _errorHandler(error);
        }

        private bool IsInAttributeState(TokenizerState returnState)
        {
            return returnState == TokenizerState.AttributeValueDoubleQuoted ||
                returnState == TokenizerState.AttributeValueSingleQuoted ||
                returnState == TokenizerState.AttributeValueUnquoted;
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
                foreach (Token tok in _stateHandlerMap[State](c))
                {
                    yield return tok;
                    if (tok.Type == TokenType.EndOfFile)
                        yield break;
                }
            }
        }

        private IEnumerable<Token> ParseData(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseRCDATA(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseRAWTEXT(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptData(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParsePLAINTEXT(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseTagOpen(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseEndTagOpen(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseTagName(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseRCDATALessThanSign(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseRCDATAEndTagOpen(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseRCDATAEndTagName(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseRAWTEXTLessThanSign(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseRAWTEXTEndTagOpen(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseRAWTEXTEndTagName(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataLessThanSign(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataEndTagOpen(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataEndTagName(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataEscapeStart(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataEscapeStartDash(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataEscaped(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataEscapedDash(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataEscapedDashDash(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataEscapedLessThanSign(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataEscapedEndTagOpen(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataEscapedEndTagName(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataDoubleEscapeStart(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataDoubleEscaped(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataDoubleEscapedDash(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataDoubleEscapedDashDash(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataDoubleEscapedLessThanSign(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseScriptDataDoubleEscapeEnd(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseBeforeAttributeName(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAttributeName(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAfterAttributeName(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseBeforeAttributeValue(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAttributeValueDoubleQuoted(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAttributeValueSingleQuoted(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAttributeValueUnquoted(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAfterAttributeValueQuoted(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseSelfClosingStartTag(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseBogusComment(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseMarkupDeclarationOpen(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCommentStart(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCommentStartDash(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseComment(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCommentLessThanSign(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCommentLessThanSignBang(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCommentLessThanSignBangDash(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCommentLessThanSignBangDashDash(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCommentEndDash(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCommentEnd(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCommentEndBang(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseDOCTYPE(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseBeforeDOCTYPEName(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseDOCTYPEName(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAfterDOCTYPEName(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAfterDOCTYPEPublicKeyword(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseBeforeDOCTYPEPublicIdentifier(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseDOCTYPEPublicIdentifierDoubleQuoted(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseDOCTYPEPublicIdentifierSingleQuoted(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAfterDOCTYPEPublicIdentifier(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseBetweenDOCTYPEPublicAndSystemIdentifiers(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAfterDOCTYPESystemKeyword(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseBeforeDOCTYPESystemIdentifier(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseDOCTYPESystemIdentifierDoubleQuoted(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseDOCTYPESystemIdentifierSingleQuoted(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAfterDOCTYPESystemIdentifier(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseBogusDOCTYPE(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCDATASection(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCDATASectionBracket(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCDATASectionEnd(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseCharacterReference(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseNamedCharacterReference(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseAmbiguousAmpersand(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseNumericCharacterReference(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseHexadecimalCharacterReferenceStart(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseDecimalCharacterReferenceStart(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseHexadecimalCharacterReference(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseDecimalCharacterReference(int c)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Token> ParseNumericCharacterReferenceEnd(int c)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
