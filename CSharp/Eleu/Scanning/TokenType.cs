namespace Eleu.Scanning;

public enum TokenType
{
	// Single-character tokens.
	TokenLeftParen, TokenRightParen, // ()
	TokenLeftBrace, TokenRightBrace, // {}
	TokenLeftBracket, TokenRightBracket, //[]
	TokenComma, TokenSemicolon,
	// Arithmetic op tokens
	TokenDot, TokenMinus, TokenPlus,		// . - + 
	TokenSlash, TokenStar, // / *
	TokenPercent, // %
	// Comparison tokens
	TokenBang, TokenBangEqual, // !, !=
	TokenEqual, TokenEqualEqual,
	TokenGreater, TokenGreaterEqual,
	TokenLess, TokenLessEqual,
	// Literals.
	TokenIdentifier, TokenString, TokenNumber,
	// Keywords.
	TokenKeywordStart,
	TokenAnd, TokenBreak, TokenClass, TokenContinue, TokenElse, TokenFalse,
	TokenFor, TokenFun, TokenIf, TokenNil, TokenOr,
	TokenAssert, TokenReturn, TokenSuper, TokenThis,
	TokenTrue, TokenVar, TokenWhile, TokenRepeat,
	TokenKeywordEnd,
	TokenComment,
	// Error, EOF
	TokenError, TokenEof
}




