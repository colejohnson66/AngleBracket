/* ============================================================================
 * File:   ParseError.cs
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
namespace AngleBracket.Tokenizer
{
    internal enum ParseError
    {
        AbruptClosingOfEmptyComment,
        AbruptDoctypePublicIdentifier,
        AbruptDoctypeSystemIdentifier,
        AbsenseOfDigitsInNumericCharacterReference,
        CDataInHtmlContext,
        CharacterReferenceOutsideUnicodeRange,
        ControlCharacterInInputStream,
        ControlCharacterReference,
        EndTagWithAttributes,
        DuplicateAttribute,
        EndTagWithTrailingSolidus,
        EofBeforeTagName,
        EofInCData,
        EofInComment,
        EofInDoctype,
        EofInScriptHtmlCommentLikeText,
        EofInTag,
        IncorrectlyClosedComment,
        IncorrectlyOpenedComment,
        InvalidCharacterSequenceAfterDoctypeName,
        InvalidFirstCharacterOfTagName,
        MissingAttributeValue,
        MissingDoctypeName,
        MissingDoctypePublicIdentifier,
        MissingDoctypeSystemIdentifier,
        MissingEndTagName,
        MissingQuoteBeforeDoctypePublicIdentifier,
        MissingQuoteBeforeDoctypeSystemIdentifier,
        MissingSemicolonAfterCharacterReference,
        MissingWhitespaceAfterDoctypePublicKeyword,
        MissingWhitespaceAfterDoctypeSystemKeyword,
        MissingWhitespaceBeforeDoctypeName,
        MissingWhitespaceBetweenAttributes,
        MissingWhitespaceBetweenDoctypePublicAndSystemIdentifiers,
        NestedComment,
        NoncharacterCharacterReference,
        NoncharacterInInputStream,
        NonVoidHtmlElementStartTagWithTrailingSolidus,
        NullCharacterReference,
        SurrogateCharacterReference,
        SurrogateCharacterInInputStream,
        UnexpectedCharacterAfterDoctypeSystemIdentifier,
        UnexpectedCharacterInAttributeName,
        UnexpectedCharacterInUnquotedAttributeValue,
        UnexpectedEqualsSignBeforeAttributeName,
        UnexpectedNullCharacter,
        UnexpectedQuestionMarkInsteadOfTagName,
        UnexpectedSolidusInTag,
        UnknownNamedCharacterReference
    }
}
