namespace Eleu.Scanning;

internal ref struct Scanner
{
	int start;
	int current;
	private readonly string fileName;
	private readonly string source;
	int line, col, startLine, startCol;

	public Scanner(string source, string fileName = "")
	{
		this.fileName = fileName;
		this.source = source;
		this.start = this.current = 0;
		this.line = this.col = 1;
		this.startLine = 1; this.startCol = 1;
	}

	public Token ScanToken()
	{
		SkipWhitespace();
		start = current;
		startLine = line; startCol = col;
		if (IsAtEnd) return MakeToken(TokenEof);
		char c = Advance();
		if (IsAlpha(c)) return Identifier();
		if (IsDigit(c)) return Number();
		return c switch
		{
			'(' => MakeToken(TokenLeftParen),
			')' => MakeToken(TokenRightParen),
			'{' => MakeToken(TokenLeftBrace),
			'}' => MakeToken(TokenRightBrace),
			'[' => MakeToken(TokenLeftBracket),
			']' => MakeToken(TokenRightBracket),
			';' => MakeToken(TokenSemicolon),
			',' => MakeToken(TokenComma),
			'.' => MakeToken(TokenDot),
			'-' => MakeToken(TokenMinus),
			'+' => MakeToken(TokenPlus),
			'/' => MakeToken(TokenSlash),
			'*' => MakeToken(TokenStar),
			'%' => MakeToken(TokenPercent),
			'!' => MakeToken(Match('=') ? TokenBangEqual : TokenBang),
			'=' => MakeToken(Match('=') ? TokenEqualEqual : TokenEqual),
			'<' => MakeToken(Match('=') ? TokenLessEqual : TokenLess),
			'>' => MakeToken(Match('=') ? TokenGreaterEqual : TokenGreater),
			'"' => ScanString(),
			_ => ErrorToken($"Unerwartetes Zeichen: '{c}'"),
		};
	}

	public List<Token> ScanAllTokens()
	{
		var result = new List<Token>();
		while (true)
		{
			var token = ScanToken();
			result.Add(token);
			if (token.Type == TokenEof || token.Type == TokenError) break;
		}
		return result;
	}
	static bool IsDigit(char c) => c >= '0' && c <= '9';
	static bool IsAlpha(char c) => char.IsLetter(c) || c == '_';
	Token Number()
	{
		while (IsDigit(Peek())) Advance();
		// Look for a fractional part.
		if (Peek() == '.' && IsDigit(Peek(1)))
		{
			// Consume the ".".
			Advance();
			while (IsDigit(Peek())) Advance();
		}
		return MakeToken(TokenNumber);
	}
	Token Identifier()
	{
		while (IsAlpha(Peek()) || IsDigit(Peek()))
			Advance();
		return MakeToken(GetIdentOrKeywordType());
	}

	TokenType GetIdentOrKeywordType()
	{
		switch (PeekFromStart())
		{
			case 'a':
					switch (PeekFromStart(1))
					{
						case 'n': return CheckKeyword(2, "and", TokenAnd);
						case 's': return CheckKeyword(2, "assert", TokenAssert);
					}
				break;
			case 'b': return CheckKeyword(1, "break", TokenBreak);
			case 'c':
					switch (PeekFromStart(1))
					{
						case 'l': return CheckKeyword(2, "class", TokenClass);
						case 'o': return CheckKeyword(2, "continue", TokenContinue);
					}
				break;
			case 'e': return CheckKeyword(1, "else", TokenElse);
			case 'f':
					switch (PeekFromStart(1))
					{
						case 'a': return CheckKeyword(2, "false", TokenFalse);
						case 'o': return CheckKeyword(2, "for", TokenFor);
						case 'u':
							if (CheckKeyword(2, "fun", TokenFun) == TokenFun)
								return TokenFun;
							else 
							  return (CheckKeyword(2, "function", TokenFun));
					}
				break;
			case 'i': return CheckKeyword(1, "if", TokenIf);
			case 'n': return CheckKeyword(1, "nil", TokenNil);
			case 'o': return CheckKeyword(1, "or", TokenOr);
			case 'r':
					switch (PeekFromStart(2))
					{
						case 'p': return CheckKeyword(1, "repeat", TokenRepeat);
						case 't': return CheckKeyword(1, "return", TokenReturn);
					}
				break;
			case 's': return CheckKeyword(1, "super", TokenSuper);
			case 't':
					switch (PeekFromStart(1))
					{
						case 'h': return CheckKeyword(2, "this", TokenThis);
						case 'r': return CheckKeyword(2, "true", TokenTrue);
					}
				break;
			case 'v': return CheckKeyword(1, "var", TokenVar);
			case 'w': return CheckKeyword(1, "while", TokenWhile);
		}
		return TokenIdentifier;
	}
	TokenType CheckKeyword(int start, string rest, TokenType type)
	{
		if (this.current - this.start != rest.Length) return TokenIdentifier;
		for (int i = start; i < rest.Length; i++)
		{
			if (source[this.start + i] != rest[i]) return TokenIdentifier;
		}
		return type;
	}
	Token ScanString()
	{
		while (Peek() != '"' && !IsAtEnd)
		{
			if (Peek() == '\n') { line++; col = 0; }
			Advance();
		}
		if (IsAtEnd)
			return ErrorToken("Nicht abgeschlossene Zeichenkette.");

		// The closing quote.
		Advance();
		return MakeToken(TokenString);
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
					if (Peek(1) == '/')
					{
						// A comment goes until the end of the line.
						while (Peek() != '\n' && !IsAtEnd) Advance();
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
	char Peek(int n = 0) => current >= source.Length - n ? '\0' : source[current + n];
	char PeekFromStart(int n = 0) => start >= source.Length - n ? '\0' : source[start + n];
	bool IsAtEnd => current >= source.Length;

	bool Match(char expected)
	{
		if (IsAtEnd) return false;
		if (source[current] != expected) return false;
		current++;
		return true;
	}

	InputStatus CreateStatus()
	{
		return new InputStatus(fileName)
		{
			LineStart = startLine,
			LineEnd = line,
			ColStart = startCol, // col - (current - start),
			ColEnd = col
		};
	}

	Token MakeToken(TokenType type)
	{
		Token token = new()
		{
			Type = type,
			Start = start,
			End = current,
			Source = source,
			Status = CreateStatus()
		};
		return token;
	}
	Token ErrorToken(string message)
	{
		Token token = new()
		{
			Type = TokenError,
			Start = 0,
			End = message.Length,
			Source = message,
			Status = CreateStatus()
		};
		return token;
	}
}




