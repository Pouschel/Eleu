namespace Eleu.Ast;



internal class AstParser
{

	private List<Token> tokens;
	private int current = 0;
	private EleuOptions options;
	private string fileName;
	public int ErrorCount { get; private set; }
	public AstParser(EleuOptions options, string fileName, List<Token> tokens)
	{
		this.tokens = tokens;
		this.fileName = fileName;
		this.options = options;
	}
	internal List<Stmt> parse()
	{
		List<Stmt> statements = new();
		while (!isAtEnd())
		{
			var decl = Declaration();
			if (decl != null) statements.Add(decl);
		}
		if (statements.Count == 0 && peek().type == TOKEN_ERROR)
		{
			errorAt(peek(), peek().StringValue);
		}

		return statements;
	}
	private Stmt statement()
	{
		// eat up empty statements
		while (match(TokenSemicolon))
			;
		if (match(TOKEN_PRINT)) return printStatement();
		if (match(TOKEN_LEFT_BRACE)) return new Stmt.Block(block());
		if (match(TOKEN_FOR)) return forStatement();
		if (match(TOKEN_IF)) return ifStatement();
		if (match(TOKEN_RETURN)) return returnStatement();
		if (match(TOKEN_WHILE)) return whileStatement();
		return expressionStatement();
	}
	private Stmt forStatement()
	{
		Consume(TOKEN_LEFT_PAREN, "Expect '(' after 'for'.");
		Stmt? initializer;
		if (match(TokenSemicolon))
			initializer = null;
		else if (match(TOKEN_VAR))
			initializer = varDeclaration();
		else
			initializer = expressionStatement();
		Expr? condition = null;
		if (!check(TokenSemicolon))
		{
			condition = expression();
		}
		Consume(TokenSemicolon, "Expect ';' after loop condition.");
		Expr? increment = null;
		if (!check(TOKEN_RIGHT_PAREN))
		{
			increment = expression();
		}
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after for clauses.");
		Stmt body = statement();
		if (increment != null)
		{
			body = new Stmt.Block(new()
			{
				body,
				new Stmt.Expression(increment)
			});
		}
		if (condition == null) condition = new Expr.Literal(true);
		body = new Stmt.While(condition, body);
		if (initializer != null)
		{
			body = new Stmt.Block(new() { initializer, body });
		}
		return body;
	}
	private Stmt ifStatement()
	{
		Consume(TOKEN_LEFT_PAREN, "Expect '(' after 'if'.");
		Expr condition = expression();
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after if condition.");

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
		Consume(TokenSemicolon, "Expect ';' after value.");
		return new Stmt.Print(value);
	}
	private Stmt returnStatement()
	{
		Token keyword = previous();
		Expr? value = null;
		if (!check(TokenSemicolon))
		{
			value = expression();
		}
		Consume(TokenSemicolon, "Expect ';' after return value.");
		return new Stmt.Return(keyword, value);
	}
	private Stmt expressionStatement()
	{
		Expr expr = expression();
		Consume(TokenSemicolon, "Expect ';' after expression.");
		return new Stmt.Expression(expr);
	}
	private Stmt.Function function(FunctionType kind)
	{
		Token name = Consume(TOKEN_IDENTIFIER, "Expect " + kind + " name.");
		Consume(TOKEN_LEFT_PAREN, "Expect '(' after " + kind + " name.");
		List<Token> parameters = new();
		if (!check(TOKEN_RIGHT_PAREN))
		{
			do
			{
				if (parameters.Count >= 255)
				{
					Error(peek(), "Can't have more than 255 parameters.");
				}

				parameters.Add(Consume(TOKEN_IDENTIFIER, "Expect parameter name."));
			} while (match(TOKEN_COMMA));
		}
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after parameters.");
		string tmsg = kind == FunctionType.FunTypeFunction ? "function" : "method";
		Consume(TOKEN_LEFT_BRACE, "Expect '{' before " + tmsg + " body.");
		List<Stmt> body = block();
		return new Stmt.Function(kind, name.StringValue, parameters, body);
	}
	private List<Stmt> block()
	{
		List<Stmt> statements = new();
		while (!check(TOKEN_RIGHT_BRACE) && !isAtEnd())
		{
			var decl = Declaration();
			if (decl != null) statements.Add(decl);
		}
		Consume(TOKEN_RIGHT_BRACE, "Expect '}' after block.");
		return statements;
	}
	private Expr assignment()
	{
		Expr expr = or();
		if (match(TOKEN_EQUAL))
		{
			Token equals = previous();
			Expr value = assignment();
			if (expr is Expr.Variable variable)
			{
				var name = variable.Name;
				return new Expr.Assign(name, value);
			}
			else if (expr is Expr.Get get)
			{
				return new Expr.Set(get.Obj, get.Name, value);
			}
			Error(equals, "Invalid assignment target.");
		}
		return expr;
	}
	private Expr or()
	{
		Expr expr = and();
		while (match(TOKEN_OR))
		{
			Token _operator = previous();
			Expr right = and();
			expr = new Expr.Logical(expr, _operator, right);
		}
		return expr;
	}
	private Expr and()
	{
		Expr expr = equality();
		while (match(TOKEN_AND))
		{
			Token _operator = previous();
			Expr right = equality();
			expr = new Expr.Logical(expr, _operator, right);
		}
		return expr;
	}
	private Expr expression()
	{
		return assignment();
	}
	private Stmt? Declaration()
	{
		try
		{
			if (match(TOKEN_FUN)) return function(FunTypeFunction);
			if (match(TOKEN_CLASS)) return ClassDeclaration();
			if (match(TOKEN_VAR)) return varDeclaration();
			return statement();
		}
		catch (EleuParseError)
		{
			Synchronize();
			return null;
		}
	}
	private Stmt ClassDeclaration()
	{
		Token name = Consume(TOKEN_IDENTIFIER, "Expect class name.");
		Expr.Variable? superclass = null;
		if (match(TOKEN_LESS))
		{
			Consume(TOKEN_IDENTIFIER, "Expect superclass name.");
			superclass = new Expr.Variable(previous().StringValue);
		}
		Consume(TOKEN_LEFT_BRACE, "Expect '{' before class body.");
		List<Stmt.Function> methods = new();
		while (!check(TOKEN_RIGHT_BRACE) && !isAtEnd())
		{
			methods.Add(function(FunTypeMethod));
		}
		Consume(TOKEN_RIGHT_BRACE, "Expect '}' after class body.");
		return new Stmt.Class(name.StringValue, superclass, methods);
	}
	private Stmt varDeclaration()
	{
		Token name = Consume(TOKEN_IDENTIFIER, "Expect variable name.");
		Expr? initializer = null;
		if (match(TOKEN_EQUAL))
		{
			initializer = expression();
		}
		Consume(TokenSemicolon, "Expect ';' after variable declaration.");
		return new Stmt.Var(name.StringValue, initializer);
	}
	private Stmt whileStatement()
	{
		Consume(TOKEN_LEFT_PAREN, "Expect '(' after 'while'.");
		Expr condition = expression();
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after condition.");
		Stmt body = statement();
		return new Stmt.While(condition, body);
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
		Expr expr = Factor();
		while (match(TokenMinus, TokenPlus))
		{
			Token _operator = previous();
			Expr right = Factor();
			expr = new Expr.Binary(expr, _operator, right);
		}
		return expr;
	}
	private Expr Factor()
	{
		Expr expr = unary();

		while (match(TOKEN_SLASH, TokenStar, TokenPercent))
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
		return call();
	}
	private Expr call()
	{
		Expr expr = primary();
		while (true)
		{
			if (match(TOKEN_LEFT_PAREN))
			{
				expr = finishCall(expr, null);
			}
			else if (match(TOKEN_DOT))
			{
				Token name = Consume(TOKEN_IDENTIFIER, "Expect property name after '.'.");
				//TODO for Byte code gen
				//if (match(TOKEN_LEFT_PAREN))
				//	expr = finishCall(expr, name.StringValue);
				//else
				expr = new Expr.Get(expr, name.StringValue);
			}
			else
			{
				break;
			}
		}
		return expr;
	}
	private Expr finishCall(Expr callee, string? mthName)
	{
		List<Expr> arguments = new();
		if (!check(TOKEN_RIGHT_PAREN))
		{
			do
			{
				if (arguments.Count >= 255)
				{
					Error(peek(), "Can't have more than 255 arguments.");
				}
				arguments.Add(expression());
			} while (match(TOKEN_COMMA));
		}
		Token paren = Consume(TOKEN_RIGHT_PAREN, "Expect ')' after arguments.");
		return new Expr.Call(callee, mthName, callee is Expr.Super, arguments);
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
		if (match(TOKEN_SUPER))
		{
			//if (!check(TOKEN_DOT)) error(peek(), "Expect '.' after 'super'.");
			Token keyword = previous();
			Consume(TOKEN_DOT, "Expect '.' after 'super'.");
			Token method = Consume(TOKEN_IDENTIFIER, "Expect superclass method name.");
			return new Expr.Super(keyword.StringValue, method.StringValue);
		}
		if (match(TOKEN_THIS)) return new Expr.This(previous().StringValue);
		if (match(TOKEN_IDENTIFIER))
		{
			return new Expr.Variable(previous().StringValue);
		}
		if (match(TOKEN_LEFT_PAREN))
		{
			Expr expr = expression();
			Consume(TOKEN_RIGHT_PAREN, "Expect ')' after expression.");
			return new Expr.Grouping(expr);
		}
		throw Error(peek(), "Expect expression.");
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
	private Token Consume(TokenType type, string message)
	{
		if (check(type)) return advance();

		throw Error(peek(), message);
	}
	private EleuParseError Error(Token token, string message)
	{
		errorAt(token, message);
		return new EleuParseError();
	}
	void errorAt(in Token token, string message)
	{
		ErrorCount++;
		var msg = string.IsNullOrEmpty(fileName) ? message : $"{token.status.Message}: Cerr: {message}";
		//msg = $"File \"{fileName}\", line {token.line}: Compiler error: {message}";
		options.Err.WriteLine(msg);
		System.Diagnostics.Trace.WriteLine(msg);
	}
	private void Synchronize()
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
				case TOKEN_ERROR:
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
		if (!isAtEnd())
		{
			current++;
			var tok = peek();
			if (tok.type == TOKEN_ERROR)
				throw Error(tok, tok.StringValue);
		}
		return previous();
	}
	private bool isAtEnd()
	{
		var t = peek().type;
		return t == TOKEN_EOF || t == TOKEN_ERROR;
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
