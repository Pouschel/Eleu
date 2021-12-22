global using static CsLox.TokenType;

namespace CsLox;

enum TokenType
{
	// Single-character tokens.
	TOKEN_LEFT_PAREN, TOKEN_RIGHT_PAREN,
	TOKEN_LEFT_BRACE, TOKEN_RIGHT_BRACE,
	TOKEN_COMMA, TOKEN_DOT, TOKEN_MINUS, TOKEN_PLUS,
	TOKEN_SEMICOLON, TOKEN_SLASH, TOKEN_STAR,
	// One or two character tokens.
	TOKEN_BANG, TOKEN_BANG_EQUAL,
	TOKEN_EQUAL, TOKEN_EQUAL_EQUAL,
	TOKEN_GREATER, TOKEN_GREATER_EQUAL,
	TOKEN_LESS, TOKEN_LESS_EQUAL,
	// Literals.
	TOKEN_IDENTIFIER, TOKEN_STRING, TOKEN_NUMBER,
	// Keywords.
	TOKEN_AND, TOKEN_CLASS, TOKEN_ELSE, TOKEN_FALSE,
	TOKEN_FOR, TOKEN_FUN, TOKEN_IF, TOKEN_NIL, TOKEN_OR,
	TOKEN_PRINT, TOKEN_RETURN, TOKEN_SUPER, TOKEN_THIS,
	TOKEN_TRUE, TOKEN_VAR, TOKEN_WHILE,

	TOKEN_ERROR, TOKEN_EOF
}


internal struct Token
{
	public TokenType type;
	public int start;
	public int end;
	public int line;
	public string source;

	public string StringValue => string.Intern(source[start..end]);

	public string StringStringValue =>source[(start + 1)..(end - 1)];

	public override string ToString() => $"{type}: {StringValue}";

}

internal class Scanner
{
	int line;
	int start;
	int current;
	private string source;

	public Scanner(string source)
	{
		this.source = source;
		Reset();
	}

	public void Reset()
	{
		this.line = 1;
		this.start = this.current = 0;
	}

	public Token scanToken()
	{
		skipWhitespace();
		start = current;
		if (isAtEnd()) return makeToken(TOKEN_EOF);
		char c = advance();
		if (isAlpha(c)) return identifier();
		if (isDigit(c)) return number();
		switch (c)
		{
			case '(': return makeToken(TOKEN_LEFT_PAREN);
			case ')': return makeToken(TOKEN_RIGHT_PAREN);
			case '{': return makeToken(TOKEN_LEFT_BRACE);
			case '}': return makeToken(TOKEN_RIGHT_BRACE);
			case ';': return makeToken(TOKEN_SEMICOLON);
			case ',': return makeToken(TOKEN_COMMA);
			case '.': return makeToken(TOKEN_DOT);
			case '-': return makeToken(TOKEN_MINUS);
			case '+': return makeToken(TOKEN_PLUS);
			case '/': return makeToken(TOKEN_SLASH);
			case '*': return makeToken(TOKEN_STAR);
			case '!':
				return makeToken(
						match('=') ? TOKEN_BANG_EQUAL : TOKEN_BANG);
			case '=':
				return makeToken(
						match('=') ? TOKEN_EQUAL_EQUAL : TOKEN_EQUAL);
			case '<':
				return makeToken(
						match('=') ? TOKEN_LESS_EQUAL : TOKEN_LESS);
			case '>':
				return makeToken(
						match('=') ? TOKEN_GREATER_EQUAL : TOKEN_GREATER);
			case '"': return scanString();
		}

		return errorToken("Unexpected character.");
	}

	static bool isDigit(char c) => c >= '0' && c <= '9';
	static bool isAlpha(char c)
	{
		return (c >= 'a' && c <= 'z') ||
					 (c >= 'A' && c <= 'Z') ||
						c == '_';
	}

	Token number()
	{
		while (isDigit(peek())) advance();
		// Look for a fractional part.
		if (peek() == '.' && isDigit(peekNext()))
		{
			// Consume the ".".
			advance();
			while (isDigit(peek())) advance();
		}
		return makeToken(TOKEN_NUMBER);
	}

	Token identifier()
	{
		while (isAlpha(peek()) || isDigit(peek())) advance();
		return makeToken(identifierType());
	}

	TokenType identifierType()
	{
		switch (source[start])
		{
			case 'a': return checkKeyword(1, "nd", TOKEN_AND);
			case 'c': return checkKeyword(1, "lass", TOKEN_CLASS);
			case 'e': return checkKeyword(1, "lse", TOKEN_ELSE);
			case 'f':
				if (current - start > 1)
				{
					switch (source[start+1])
					{
						case 'a': return checkKeyword(2, "lse", TOKEN_FALSE);
						case 'o': return checkKeyword(2, "r", TOKEN_FOR);
						case 'u': return checkKeyword(2, "n", TOKEN_FUN);
					}
				}
				break;
			case 'i': return checkKeyword(1, "f", TOKEN_IF);
			case 'n': return checkKeyword(1, "il", TOKEN_NIL);
			case 'o': return checkKeyword(1, "r", TOKEN_OR);
			case 'p': return checkKeyword(1, "rint", TOKEN_PRINT);
			case 'r': return checkKeyword(1, "eturn", TOKEN_RETURN);
			case 's': return checkKeyword(1, "uper", TOKEN_SUPER);
			case 't':
				if (current - start > 1)
				{
					switch (source[start + 1])
					{
						case 'h': return checkKeyword(2, "is", TOKEN_THIS);
						case 'r': return checkKeyword(2, "ue", TOKEN_TRUE);
					}
				}
				break;
			case 'v': return checkKeyword(1, "ar", TOKEN_VAR);
			case 'w': return checkKeyword(1, "hile", TOKEN_WHILE);
		}
		return TOKEN_IDENTIFIER;
	}

	TokenType checkKeyword(int start, string rest, TokenType type)
	{
		if (this.current - this.start != start + rest.Length) return TOKEN_IDENTIFIER;
		for (int i = 0; i < rest.Length; i++)
		{
			if (source[start + this.start + i] != rest[i]) return TOKEN_IDENTIFIER;
		}
		return type;
	}

	Token scanString()
	{
		while (peek() != '"' && !isAtEnd())
		{
			if (peek() == '\n') line++;
			advance();
		}

		if (isAtEnd()) return errorToken("Unterminated string.");

		// The closing quote.
		advance();
		return makeToken(TOKEN_STRING);
	}


	void skipWhitespace()
	{
		while (true)
		{
			char c = peek();
			switch (c)
			{
				case ' ':
				case '\r':
				case '\t':
					advance();
					break;
				case '\n':
					line++;
					advance();
					break;
				case '/':
					if (peekNext() == '/')
					{
						// A comment goes until the end of the line.
						while (peek() != '\n' && !isAtEnd()) advance();
					}
					else
					{
						return;
					}
					break;
				default:
					if (char.IsWhiteSpace(c))
						continue;
					return;
			}
		}
	}

	char advance() => source[current++];
	char peek() => current >= source.Length ? '\0' : source[current];
	char peekNext() => current >= source.Length - 1 ? '\0' : source[current + 1];

	bool isAtEnd()
	{
		return current >= source.Length;
	}

	bool match(char expected)
	{
		if (isAtEnd()) return false;
		if (source[current] != expected) return false;
		current++;
		return true;
	}

	Token makeToken(TokenType type)
	{
		Token token = new();
		token.type = type;
		token.start = start;
		token.end = current;
		token.line = line;
		token.source = source;
		return token;
	}

	Token errorToken(string message)
	{
		Token token;
		token.type = TOKEN_ERROR;
		token.start = 0;
		token.end = message.Length;
		token.line = line;
		token.source = message;
		return token;
	}

}




