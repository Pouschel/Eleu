
namespace Eleu.Ast
{
	internal class AstParser
	{
		class ParseError : Exception { }

		private List<Token> tokens;
		private int current = 0;
		private EleuOptions options;
		private string fileName;
		public AstParser(EleuOptions options, string fileName, List<Token> tokens)
		{
			this.tokens = tokens;
			this.fileName = fileName;
			this.options = options;
		}
		internal List<Stmt> parse()
		{
			List<Stmt> statements = new ();
			while (!isAtEnd())
			{
				var decl = declaration();
				if (decl!=null) statements.Add(decl);
			}
			return statements;
		}
		private Stmt statement()
		{
			if (match(TOKEN_PRINT)) return printStatement();
			if (match(TOKEN_LEFT_BRACE)) return new Stmt.Block(block());
			if (match(TOKEN_IF)) return ifStatement();
			return expressionStatement();
		}
		private Stmt ifStatement()
		{
			consume(TOKEN_LEFT_PAREN, "Expect '(' after 'if'.");
			Expr condition = expression();
			consume(TOKEN_RIGHT_PAREN, "Expect ')' after if condition.");

			Stmt thenBranch = statement();
			Stmt? elseBranch = null;
			if (match(TOKEN_ELSE))
			{
				elseBranch = statement();
			}
			return new Stmt.If(condition, thenBranch, elseBranch);
		}
		private Stmt printStatement()
		{
			Expr value = expression();
			consume(TokenSemicolon, "Expect ';' after value.");
			return new Stmt.Print(value);
		}
		private Stmt expressionStatement()
		{
			Expr expr = expression();
			consume(TokenSemicolon, "Expect ';' after expression.");
			return new Stmt.Expression(expr);
		}
		private List<Stmt> block()
		{
			List<Stmt> statements = new ();
			while (!check(TOKEN_RIGHT_BRACE) && !isAtEnd())
			{
				var decl = declaration();
				if (decl != null) statements.Add(decl);
			}
			consume(TOKEN_RIGHT_BRACE, "Expect '}' after block.");
			return statements;
		}
		private Expr assignment()
		{
			Expr expr = equality();
			if (match(TOKEN_EQUAL))
			{
				Token equals = previous();
				Expr value = assignment();
				if (expr is Expr.Variable variable) 
				{
					Token name = variable.name;
					return new Expr.Assign(name, value);
				}
				error(equals, "Invalid assignment target.");
			}
			return expr;
		}
		private Expr expression()
		{
			return assignment();
		}
		private Stmt? declaration()
		{
			try
			{
				if (match(TOKEN_VAR)) return varDeclaration();
				return statement();
			}
			catch (ParseError )
			{
				synchronize();
				return null;
			}
		}
		private Stmt varDeclaration()
		{
			Token name = consume(TOKEN_IDENTIFIER, "Expect variable name.");
			Expr? initializer = null;
			if (match(TOKEN_EQUAL))
			{
				initializer = expression();
			}
			consume(TokenSemicolon, "Expect ';' after variable declaration.");
			return new Stmt.Var(name, initializer);
		}

		private Expr equality()
		{
			Expr expr = comparison();
			while (match(TOKEN_BANG_EQUAL, TOKEN_EQUAL_EQUAL))
			{
				Token _operator = previous();
				Expr right = comparison();
				expr = new Expr.Binary(expr, _operator, right);
			}
			return expr;
		}
		private Expr comparison()
		{
			Expr expr = term();

			while (match(TOKEN_GREATER, TOKEN_GREATER_EQUAL, TOKEN_LESS, TOKEN_LESS_EQUAL))
			{
				Token _operator = previous();
				Expr right = term();
				expr = new Expr.Binary(expr, _operator, right);
			}
			return expr;
		}
		private Expr term()
		{
			Expr expr = factor();
			while (match(TokenMinus, TokenPlus))
			{
				Token _operator = previous();
				Expr right = factor();
				expr = new Expr.Binary(expr, _operator, right);
			}
			return expr;
		}
		private Expr factor()
		{
			Expr expr = unary();

			while (match(TOKEN_SLASH, TokenStar))
			{
				Token _operator = previous();
				Expr right = unary();
				expr = new Expr.Binary(expr, _operator, right);
			}
			return expr;
		}
		private Expr unary()
		{
			if (match(TOKEN_BANG, TokenMinus))
			{
				Token _operator = previous();
				Expr right = unary();
				return new Expr.Unary(_operator, right);
			}

			return primary();
		}
		private Expr primary()
		{
			if (match(TOKEN_FALSE)) return new Expr.Literal(false);
			if (match(TOKEN_TRUE)) return new Expr.Literal(true);
			if (match(TOKEN_NIL)) return new Expr.Literal(null);
			if (match(TOKEN_NUMBER)) return new Expr.Literal(double.Parse(previous().StringValue));
			if (match(TOKEN_STRING))
			{
				return new Expr.Literal(previous().StringStringValue);
			}
			if (match(TOKEN_IDENTIFIER))
			{
				return new Expr.Variable(previous());
			}
			if (match(TOKEN_LEFT_PAREN))
			{
				Expr expr = expression();
				consume(TOKEN_RIGHT_PAREN, "Expect ')' after expression.");
				return new Expr.Grouping(expr);
			}
			throw error(peek(), "Expect expression.");
		}

		private bool match(params TokenType[] types)
		{
			foreach (TokenType type in types)
			{
				if (check(type))
				{
					advance();
					return true;
				}
			}
			return false;
		}
		private Token consume(TokenType type, String message)
		{
			if (check(type)) return advance();

			throw error(peek(), message);
		}
		private ParseError error(Token token, String message)
		{
			errorAt(token, message);
			return new ParseError();
		}
		void errorAt(in Token token, string message)
		{
			//if (panicMode) return;
			//panicMode = true;
			var msg = string.IsNullOrEmpty(fileName) ? message : $"{fileName}({token.line}): Cerr: {message}";
			//msg = $"File \"{fileName}\", line {token.line}: Compiler error: {message}";
			options.Err.WriteLine(msg);
			System.Diagnostics.Trace.WriteLine(msg);
		}
		private void synchronize()
		{
			advance();

			while (!isAtEnd())
			{
				if (previous().type == TokenSemicolon) return;

				switch (peek().type)
				{
					case TOKEN_CLASS:
					case TOKEN_FUN:
					case TOKEN_VAR:
					case TOKEN_FOR:
					case TOKEN_IF:
					case TOKEN_WHILE:
					case TOKEN_PRINT:
					case TOKEN_RETURN:
						return;
				}

				advance();
			}
		}
		private bool check(TokenType type)
		{
			if (isAtEnd()) return false;
			return peek().type == type;
		}
		private Token advance()
		{
			if (!isAtEnd()) current++;
			return previous();
		}
		private bool isAtEnd()
		{
			return peek().type == TOKEN_EOF;
		}
		private Token peek()
		{
			return tokens[current];
		}
		private Token previous()
		{
			return tokens[current - 1];
		}
	}
}
