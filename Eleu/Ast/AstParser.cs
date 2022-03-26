using System.Globalization;

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
	internal List<Stmt> Parse()
	{
		List<Stmt> statements = new();
		while (!IsAtEnd())
		{
			var decl = Declaration();
			if (decl != null) statements.Add(decl);
		}
		if (statements.Count == 0 && Peek().type == TOKEN_ERROR)
		{
			ErrorAt(Peek(), Peek().StringValue);
		}

		return statements;
	}

	InputStatus CurrentInputStatus => Peek().status;

	private Stmt Statement()
	{
		// eat up empty statements
		while (Match(TokenSemicolon))
			;
		Stmt stmt;
		var curStat = CurrentInputStatus;
		if (Match(TOKEN_PRINT)) stmt = PrintStatement();
		else if (Match(TOKEN_LEFT_BRACE)) stmt = new Stmt.Block(Block());
		else if (Match(TOKEN_FOR)) stmt = ForStatement();
		else if (Match(TOKEN_IF)) stmt = IfStatement();
		else if (Match(TOKEN_RETURN)) stmt = ReturnStatement();
		else if (Match(TOKEN_WHILE)) stmt = WhileStatement();
		else stmt = ExpressionStatement();
		stmt.Status = curStat.Union(Previous.status);
		return stmt;
	}
	private Stmt ForStatement()
	{
		Consume(TOKEN_LEFT_PAREN, "Expect '(' after 'for'.");
		Stmt? initializer;
		if (Match(TokenSemicolon))
			initializer = null;
		else if (Match(TOKEN_VAR))
			initializer = VarDeclaration();
		else
			initializer = ExpressionStatement();
		Expr? condition = null;
		if (!Check(TokenSemicolon))
		{
			condition = Expression();
		}
		Consume(TokenSemicolon, "Expect ';' after loop condition.");
		Expr? increment = null;
		if (!Check(TOKEN_RIGHT_PAREN))
		{
			increment = Expression();
		}
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after for clauses.");
		Stmt body = Statement();
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
	private Stmt IfStatement()
	{
		Consume(TOKEN_LEFT_PAREN, "Expect '(' after 'if'.");
		Expr condition = Expression();
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after if condition.");

		Stmt thenBranch = Statement();
		Stmt? elseBranch = null;
		if (Match(TOKEN_ELSE))
		{
			elseBranch = Statement();
		}
		return new Stmt.If(condition, thenBranch, elseBranch);
	}
	private Stmt PrintStatement()
	{
		Expr value = Expression();
		Consume(TokenSemicolon, "Ein ';' wird nach einer print-Anweisung erwartet.");
		return new Stmt.Print(value);
	}
	private Stmt ReturnStatement()
	{
		Token keyword = Previous;
		Expr? value = null;
		if (!Check(TokenSemicolon))
		{
			value = Expression();
		}
		Consume(TokenSemicolon, "Nach dem Rückgabewert wird ein ';' erwartet.");
		return new Stmt.Return(keyword, value);
	}
	private Stmt ExpressionStatement()
	{
		Expr expr = Expression();
		Consume(TokenSemicolon, "Ein ';' wird hier erwartet.");
		return new Stmt.Expression(expr);
	}
	private Stmt.Function Function(FunctionType kind)
	{
		Token name = Consume(TOKEN_IDENTIFIER, "Expect " + kind + " name.");
		Consume(TOKEN_LEFT_PAREN, "Expect '(' after " + kind + " name.");
		List<Token> parameters = new();
		if (!Check(TOKEN_RIGHT_PAREN))
		{
			do
			{
				if (parameters.Count >= 255)
				{
					Error(Peek(), "Can't have more than 255 parameters.");
				}

				parameters.Add(Consume(TOKEN_IDENTIFIER, "Expect parameter name."));
			} while (Match(TOKEN_COMMA));
		}
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after parameters.");
		string tmsg = kind == FunctionType.FunTypeFunction ? "function" : "method";
		Consume(TOKEN_LEFT_BRACE, "Expect '{' before " + tmsg + " body.");
		List<Stmt> body = Block();
		return new Stmt.Function(kind, name.StringValue, parameters, body);
	}
	private List<Stmt> Block()
	{
		List<Stmt> statements = new();
		while (!Check(TOKEN_RIGHT_BRACE) && !IsAtEnd())
		{
			var decl = Declaration();
			if (decl != null) statements.Add(decl);
		}
		Consume(TOKEN_RIGHT_BRACE, "Expect '}' after block.");
		return statements;
	}
	private Expr Assignment()
	{
		Expr expr = Or();
		if (Match(TOKEN_EQUAL))
		{
			Token equals = Previous;
			Expr value = Assignment();
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
	private Expr Or()
	{
		Expr expr = And();
		while (Match(TOKEN_OR))
		{
			Token _operator = Previous;
			Expr right = And();
			expr = new Expr.Logical(expr, _operator, right);
		}
		return expr;
	}
	private Expr And()
	{
		Expr expr = Equality();
		while (Match(TOKEN_AND))
		{
			Token _operator = Previous;
			Expr right = Equality();
			expr = new Expr.Logical(expr, _operator, right);
		}
		return expr;
	}
	private Expr Expression()
	{
		var curStat = CurrentInputStatus;
		var expr = Assignment();
		expr.Status = curStat.Union(CurrentInputStatus);
		return expr;
	}
	private Stmt? Declaration()
	{
		try
		{
			if (Match(TOKEN_FUN)) return Function(FunTypeFunction);
			if (Match(TOKEN_CLASS)) return ClassDeclaration();
			if (Match(TOKEN_VAR)) return VarDeclaration();
			return Statement();
		}
		catch (EleuParseError)
		{
			Synchronize();
			return null;
		}
	}
	private Stmt ClassDeclaration()
	{
		var curStat = Previous.status;
		Token name = Consume(TOKEN_IDENTIFIER, "Expect class name.");
		Expr.Variable? superclass = null;
		if (Match(TOKEN_LESS))
		{
			Consume(TOKEN_IDENTIFIER, "Expect superclass name.");
			superclass = new Expr.Variable(Previous.StringValue);
		}
		curStat = Previous.status.Union(curStat);
		Consume(TOKEN_LEFT_BRACE, "Expect '{' before class body.");
		List<Stmt.Function> methods = new();
		while (!Check(TOKEN_RIGHT_BRACE) && !IsAtEnd())
		{
			methods.Add(Function(FunTypeMethod));
		}
		Consume(TOKEN_RIGHT_BRACE, "Expect '}' after class body.");
		return new Stmt.Class(name.StringValue, superclass, methods) { Status = curStat };
	}
	private Stmt VarDeclaration()
	{
		var cs = CurrentInputStatus;
		Token name = Consume(TOKEN_IDENTIFIER, "Expect variable name.");
		Expr? initializer = null;
		if (Match(TOKEN_EQUAL))
		{
			initializer = Expression();
		}
		cs = cs.Union(CurrentInputStatus);
		Consume(TokenSemicolon, "Expect ';' after variable declaration.");
		return new Stmt.Var(name.StringValue, initializer) { Status = cs };
	}
	private Stmt WhileStatement()
	{
		Consume(TOKEN_LEFT_PAREN, "Expect '(' after 'while'.");
		Expr condition = Expression();
		Consume(TOKEN_RIGHT_PAREN, "Expect ')' after condition.");
		Stmt body = Statement();
		return new Stmt.While(condition, body);
	}
	private Expr Equality()
	{
		Expr expr = Comparison();
		while (Match(TOKEN_BANG_EQUAL, TOKEN_EQUAL_EQUAL))
		{
			Token _operator = Previous;
			Expr right = Comparison();
			expr = new Expr.Binary(expr, _operator, right);
		}
		return expr;
	}
	private Expr Comparison()
	{
		Expr expr = Term();

		while (Match(TOKEN_GREATER, TOKEN_GREATER_EQUAL, TOKEN_LESS, TOKEN_LESS_EQUAL))
		{
			Token _operator = Previous;
			Expr right = Term();
			expr = new Expr.Binary(expr, _operator, right);
		}
		return expr;
	}
	private Expr Term()
	{
		Expr expr = Factor();
		while (Match(TokenMinus, TokenPlus))
		{
			Token _operator = Previous;
			Expr right = Factor();
			expr = new Expr.Binary(expr, _operator, right);
		}
		return expr;
	}
	private Expr Factor()
	{
		Expr expr = Unary();

		while (Match(TOKEN_SLASH, TokenStar, TokenPercent))
		{
			Token _operator = Previous;
			Expr right = Unary();
			expr = new Expr.Binary(expr, _operator, right);
		}
		return expr;
	}
	private Expr Unary()
	{
		if (Match(TOKEN_BANG, TokenMinus))
		{
			Token _operator = Previous;
			Expr right = Unary();
			return new Expr.Unary(_operator, right);
		}
		return Call();
	}
	private Expr Call()
	{
		Expr expr = Primary();
		while (true)
		{
			if (Match(TOKEN_LEFT_PAREN))
			{
				expr = FinishCall(expr, null);
			}
			else if (Match(TOKEN_DOT))
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
	private Expr FinishCall(Expr callee, string? mthName)
	{
		List<Expr> arguments = new();
		if (!Check(TOKEN_RIGHT_PAREN))
		{
			do
			{
				if (arguments.Count >= 255)
				{
					Error(Peek(), "Can't have more than 255 arguments.");
				}
				arguments.Add(Expression());
			} while (Match(TOKEN_COMMA));
		}
		Token paren = Consume(TOKEN_RIGHT_PAREN, "Expect ')' after arguments.");
		return new Expr.Call(callee, mthName, callee is Expr.Super, arguments);
	}
	private Expr Primary()
	{
		if (Match(TOKEN_FALSE)) return new Expr.Literal(false);
		if (Match(TOKEN_TRUE)) return new Expr.Literal(true);
		if (Match(TOKEN_NIL)) return new Expr.Literal(null);
		if (Match(TOKEN_NUMBER)) return new Expr.Literal(double.Parse(Previous.StringValue, CultureInfo.InvariantCulture));
		if (Match(TOKEN_STRING))
		{
			return new Expr.Literal(Previous.StringStringValue);
		}
		if (Match(TOKEN_SUPER))
		{
			//if (!check(TOKEN_DOT)) error(peek(), "Expect '.' after 'super'.");
			Token keyword = Previous;
			Consume(TOKEN_DOT, "Expect '.' after 'super'.");
			Token method = Consume(TOKEN_IDENTIFIER, "Expect superclass method name.");
			return new Expr.Super(keyword.StringValue, method.StringValue);
		}
		if (Match(TOKEN_THIS)) return new Expr.This(Previous.StringValue);
		if (Match(TOKEN_IDENTIFIER))
		{
			return new Expr.Variable(Previous.StringValue);
		}
		if (Match(TOKEN_LEFT_PAREN))
		{
			Expr expr = Expression();
			Consume(TOKEN_RIGHT_PAREN, "Expect ')' after expression.");
			return new Expr.Grouping(expr);
		}
		throw Error(Peek(), "Expect expression.");
	}

	private bool Match(params TokenType[] types)
	{
		foreach (TokenType type in types)
		{
			if (Check(type))
			{
				Advance();
				return true;
			}
		}
		return false;
	}
	private Token Consume(TokenType type, string message)
	{
		if (Check(type)) return Advance();

		throw Error(Peek(), message);
	}
	private EleuParseError Error(Token token, string message)
	{
		ErrorAt(token, message);
		return new EleuParseError();
	}
	void ErrorAt(in Token token, string message)
	{
		ErrorCount++;
		var msg = string.IsNullOrEmpty(fileName) ? message : $"{token.status.Message}: Cerr: {message}";
		//msg = $"File \"{fileName}\", line {token.line}: Compiler error: {message}";
		options.Err.WriteLine(msg);
		System.Diagnostics.Trace.WriteLine(msg);
	}
	private void Synchronize()
	{
		Advance();
		while (!IsAtEnd())
		{
			if (Previous.type == TokenSemicolon) return;
			switch (Peek().type)
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
			Advance();
		}
	}
	private bool Check(TokenType type)
	{
		if (IsAtEnd()) return false;
		return Peek().type == type;
	}
	private Token Advance()
	{
		if (!IsAtEnd())
		{
			current++;
			var tok = Peek();
			if (tok.type == TOKEN_ERROR)
				throw Error(tok, tok.StringValue);
		}
		return Previous;
	}
	private bool IsAtEnd()
	{
		var t = Peek().type;
		return t == TOKEN_EOF || t == TOKEN_ERROR;
	}
	private Token Peek() => tokens[current];
	private Token Previous => tokens[current - 1];
}
