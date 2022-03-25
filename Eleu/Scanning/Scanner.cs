namespace Eleu.Scanning;

internal class Scanner
{
	int start;
	int current;
	private string fileName;
	private string source;
	int line, col;

	public Scanner(string source, string fileName = "")
	{
		this.fileName = fileName;
		this.source = source;
		this.start = this.current = 0;
		this.line = this.col = 1;
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
	static bool IsAlpha(char c) => char.IsLetter(c) || c == '_';
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
						case 'u':
							if (CheckKeyword(2, "n", TOKEN_FUN) == TOKEN_FUN)
								return TOKEN_FUN;
							else return (CheckKeyword(2, "nction", TOKEN_FUN));
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
					col = 0;
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

	char Advance()
	{
		col++;
		return source[current++];
	}

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

	InputStatus CreateStatus()
	{
		return new InputStatus(fileName)
		{
			LineStart = line,
			LineEnd = line,
			ColStart = col - (current - start),
			ColEnd = col
		};
	}

	Token MakeToken(TokenType type)
	{
		Token token = new();
		token.type = type;
		token.start = start;
		token.end = current;
		token.source = source;
		token.status = CreateStatus();
		return token;
	}

	Token ErrorToken(string message)
	{
		Token token;
		token.type = TOKEN_ERROR;
		token.start = 0;
		token.end = message.Length;
		token.source = message;
		token.status = CreateStatus();
		return token;
	}

}




