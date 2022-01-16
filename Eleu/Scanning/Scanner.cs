namespace Eleu.Scanning;

public enum TokenType
{
	// Single-character tokens.
	TOKEN_LEFT_PAREN, TOKEN_RIGHT_PAREN,
	TOKEN_LEFT_BRACE, TOKEN_RIGHT_BRACE,
	TOKEN_COMMA, TOKEN_DOT, TokenMinus, TokenPlus,
	TokenSemicolon, TOKEN_SLASH, TokenStar,
	TokenPercent, TokenLeftBracket, TokenRightBracket,
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

public struct Token
{
	public TokenType type;
	public int start;
	public int end;
	public int line;
	public string source;

	public string StringValue => string.Intern(source[start..end]);

	public string StringStringValue => source[(start + 1)..(end - 1)];

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

	public Token ScanToken()
	{
		SkipWhitespace();
		start = current;
		if (IsAtEnd()) return MakeToken(TOKEN_EOF);
		char c = Advance();
		if (IsAlpha(c)) return Identifier();
		if (IsDigit(c)) return Number();
		switch (c)
		{
			case '(': return MakeToken(TOKEN_LEFT_PAREN);
			case ')': return MakeToken(TOKEN_RIGHT_PAREN);
			case '{': return MakeToken(TOKEN_LEFT_BRACE);
			case '}': return MakeToken(TOKEN_RIGHT_BRACE);
			case ';': return MakeToken(TokenSemicolon);
			case ',': return MakeToken(TOKEN_COMMA);
			case '.': return MakeToken(TOKEN_DOT);
			case '-': return MakeToken(TokenMinus);
			case '+': return MakeToken(TokenPlus);
			case '/': return MakeToken(TOKEN_SLASH);
			case '*': return MakeToken(TokenStar);
			case '%': return MakeToken(TokenPercent);
			case '[': return MakeToken(TokenLeftBracket);
			case ']': return MakeToken(TokenRightBracket);
			case '!':
				return MakeToken(
						Match('=') ? TOKEN_BANG_EQUAL : TOKEN_BANG);
			case '=':
				return MakeToken(
						Match('=') ? TOKEN_EQUAL_EQUAL : TOKEN_EQUAL);
			case '<':
				return MakeToken(
						Match('=') ? TOKEN_LESS_EQUAL : TOKEN_LESS);
			case '>':
				return MakeToken(
						Match('=') ? TOKEN_GREATER_EQUAL : TOKEN_GREATER);
			case '"': return ScanString();
		}
		return ErrorToken($"Unexpected character: {c}");
	}

	public List<Token> ScanAllTokens()
	{
		var result = new List<Token>();
		while (true)
		{
			var token = ScanToken();
			result.Add(token);
			if (token.type == TOKEN_EOF || token.type == TOKEN_ERROR) break;
		}
		return result;
	}

	static bool IsDigit(char c) => c >= '0' && c <= '9';
	static bool IsAlpha(char c)
	{
		return (c >= 'a' && c <= 'z') ||
					 (c >= 'A' && c <= 'Z') ||
						c == '_';
	}

	Token Number()
	{
		while (IsDigit(Peek())) Advance();
		// Look for a fractional part.
		if (Peek() == '.' && IsDigit(PeekNext()))
		{
			// Consume the ".".
			Advance();
			while (IsDigit(Peek())) Advance();
		}
		return MakeToken(TOKEN_NUMBER);
	}

	Token Identifier()
	{
		while (IsAlpha(Peek()) || IsDigit(Peek())) Advance();
		return MakeToken(IdentifierType());
	}

	TokenType IdentifierType()
	{
		switch (source[start])
		{
			case 'a': return CheckKeyword(1, "nd", TOKEN_AND);
			case 'c': return CheckKeyword(1, "lass", TOKEN_CLASS);
			case 'e': return CheckKeyword(1, "lse", TOKEN_ELSE);
			case 'f':
				if (current - start > 1)
				{
					switch (source[start + 1])
					{
						case 'a': return CheckKeyword(2, "lse", TOKEN_FALSE);
						case 'o': return CheckKeyword(2, "r", TOKEN_FOR);
						case 'u': return CheckKeyword(2, "n", TOKEN_FUN);
					}
				}
				break;
			case 'i': return CheckKeyword(1, "f", TOKEN_IF);
			case 'n': return CheckKeyword(1, "il", TOKEN_NIL);
			case 'o': return CheckKeyword(1, "r", TOKEN_OR);
			case 'p': return CheckKeyword(1, "rint", TOKEN_PRINT);
			case 'r': return CheckKeyword(1, "eturn", TOKEN_RETURN);
			case 's': return CheckKeyword(1, "uper", TOKEN_SUPER);
			case 't':
				if (current - start > 1)
				{
					switch (source[start + 1])
					{
						case 'h': return CheckKeyword(2, "is", TOKEN_THIS);
						case 'r': return CheckKeyword(2, "ue", TOKEN_TRUE);
					}
				}
				break;
			case 'v': return CheckKeyword(1, "ar", TOKEN_VAR);
			case 'w': return CheckKeyword(1, "hile", TOKEN_WHILE);
		}
		return TOKEN_IDENTIFIER;
	}

	TokenType CheckKeyword(int start, string rest, TokenType type)
	{
		if (this.current - this.start != start + rest.Length) return TOKEN_IDENTIFIER;
		for (int i = 0; i < rest.Length; i++)
		{
			if (source[start + this.start + i] != rest[i]) return TOKEN_IDENTIFIER;
		}
		return type;
	}

	Token ScanString()
	{
		while (Peek() != '"' && !IsAtEnd())
		{
			if (Peek() == '\n') line++;
			Advance();
		}

		if (IsAtEnd()) return ErrorToken("Unterminated string.");

		// The closing quote.
		Advance();
		return MakeToken(TOKEN_STRING);
	}
	void SkipWhitespace()
	{
		while (true)
		{
			char c = Peek();
			switch (c)
			{
				case ' ':
				case '\r':
				case '\t':
					Advance();
					break;
				case '\n':
					line++;
					Advance();
					break;
				case '/':
					if (PeekNext() == '/')
					{
						// A comment goes until the end of the line.
						while (Peek() != '\n' && !IsAtEnd()) Advance();
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

	char Advance() => source[current++];
	char Peek() => current >= source.Length ? '\0' : source[current];
	char PeekNext() => current >= source.Length - 1 ? '\0' : source[current + 1];

	bool IsAtEnd()
	{
		return current >= source.Length;
	}

	bool Match(char expected)
	{
		if (IsAtEnd()) return false;
		if (source[current] != expected) return false;
		current++;
		return true;
	}

	Token MakeToken(TokenType type)
	{
		Token token = new();
		token.type = type;
		token.start = start;
		token.end = current;
		token.line = line;
		token.source = source;
		return token;
	}

	Token ErrorToken(string message)
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




