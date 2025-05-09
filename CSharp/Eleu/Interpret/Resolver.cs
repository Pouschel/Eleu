namespace Eleu.Interpret;

internal class Resolver : Expr.Visitor<object?>, Stmt.Visitor<object?>
{
	private enum ClassType
	{
		NONE,
		CLASS,
		SUBCLASS
	}
	private Interpreter interpreter;
	private List<Dictionary<string, bool>> scopes = new();
	private int stackLen;
	private FunctionType currentFunction = FunTypeScript;
	private ClassType currentClass = ClassType.NONE;
	private int loopLevel;

	void Push(Dictionary<string, bool> d)
	{
		if (stackLen < scopes.Count)
			scopes[stackLen] = d;
		else
			scopes.Add(d);
		stackLen++;
	}

	void Pop() => stackLen--;
	Dictionary<string, bool>? Peek() => stackLen > 0 ? scopes[stackLen - 1] : null;

	public Resolver(Interpreter interpreter)
	{
		this.interpreter = interpreter;
	}

	public object? VisitAssignExpr(Expr.Assign expr)
	{
		Resolve(expr.Value);
		ResolveLocal(expr, expr.Name);
		return null;
	}

	public object? VisitBinaryExpr(Expr.Binary expr)
	{
		Resolve(expr.Left);
		Resolve(expr.Right);
		return null;
	}

	public object? VisitBlockStmt(Stmt.Block stmt)
	{
		BeginScope();
		Resolve(stmt.Statements);
		EndScope();
		return null;
	}
	public void Resolve(List<Stmt> statements)
	{
		foreach (Stmt statement in statements)
		{
			Resolve(statement);
		}
	}

	private object? Resolve(Stmt stmt)
	{
		interpreter.RegisterStatus(stmt.Status);
		return stmt.Accept(this);
	}

	private object? Resolve(Expr? expr)
	{
		interpreter.RegisterStatus(expr?.Status);
		return expr?.Accept(this);
	}

	private void BeginScope() => Push(new());
	private void EndScope() => Pop();

	public object? VisitCallExpr(Expr.Call expr)
	{
		Resolve(expr.Callee);
		foreach (Expr argument in expr.Arguments)
		{
			Resolve(argument);
		}
		return null;
	}

	public object? VisitClassStmt(Stmt.Class stmt)
	{
		ClassType enclosingClass = currentClass;
		currentClass = ClassType.CLASS;
		Declare(stmt.Name);
		define(stmt.Name);
		if (stmt.Superclass != null && stmt.Name == stmt.Superclass.Name)
		{
			Error(stmt.Status, "A class can't inherit from itself.");
		}
		if (stmt.Superclass != null)
		{
			currentClass = ClassType.SUBCLASS;
			Resolve(stmt.Superclass);
		}
		if (stmt.Superclass != null)
		{
			BeginScope();
			Peek()!["super"] = true;
		}
		BeginScope();
		Peek()!["this"] = true;
		foreach (Stmt.Function method in stmt.Methods)
		{
			FunctionType declaration = FunTypeMethod;
			if (method.Name == "init") declaration = FunTypeInitializer;
			ResolveFunction(method, declaration);
		}
		EndScope();
		if (stmt.Superclass != null) EndScope();
		currentClass = enclosingClass;
		return null;
	}

  public object? VisitExpressionStmt(Stmt.Expression stmt) => Resolve(stmt.expression);

  public object? VisitFunctionStmt(Stmt.Function stmt)
	{
		Declare(stmt.Name);
		define(stmt.Name);
		ResolveFunction(stmt, FunTypeFunction);
		return null;
	}
	private void ResolveFunction(Stmt.Function function, FunctionType type)
	{
		FunctionType enclosingFunction = currentFunction;
		currentFunction = type;
		BeginScope();
		foreach (var param in function.Paras)
		{
			Declare(param.StringValue);
			define(param.StringValue);
		}
		Resolve(function.Body);
		EndScope();
		currentFunction = enclosingFunction;
	}

	public object? VisitGetExpr(Expr.Get expr) => Resolve(expr.Obj);
	public object? VisitGroupingExpr(Expr.Grouping expr) => Resolve(expr.Expression);
	public object? VisitIfStmt(Stmt.If stmt)
	{
		Resolve(stmt.Condition);
		Resolve(stmt.ThenBranch);
		if (stmt.ElseBranch != null) Resolve(stmt.ElseBranch);
		return null;
	}
	public object? VisitLiteralExpr(Expr.Literal expr) => null;
	public object? VisitLogicalExpr(Expr.Logical expr)
	{
		Resolve(expr.Left);
		Resolve(expr.Right);
		return null;
	}
	public object? VisitReturnStmt(Stmt.Return stmt)
	{
		if (currentFunction == FunTypeScript)
		{
			Error(stmt.Status, "Can't return from top-level code.");
		}
		if (currentFunction == FunTypeInitializer && stmt.Value != null)
			Error(stmt.Status, "Can't return a value from an initializer.");
		return Resolve(stmt.Value);
	}

	public object? VisitSetExpr(Expr.Set expr)
	{
		Resolve(expr.Value);
		Resolve(expr.Obj);
		return null;
	}

	public object? VisitSuperExpr(Expr.Super expr)
	{
		if (currentClass == ClassType.NONE)
		{
			Error(expr.Status, "Can't use 'super' outside of a class.");
		}
		else if (currentClass != ClassType.SUBCLASS)
		{
			Error(expr.Status, "Can't use 'super' in a class with no superclass.");
		}
		return ResolveLocal(expr, expr.Keyword);
	}

	public object? VisitThisExpr(Expr.This expr)
	{
		if (currentClass == ClassType.NONE)
		{
			Error(expr.Status, "Can't use 'this' outside of a class.");
			return null;
		}
		return ResolveLocal(expr, expr.Keyword);
	}

	public object? VisitUnaryExpr(Expr.Unary expr) => Resolve(expr.Right);
	public object? VisitVariableExpr(Expr.Variable expr)
	{
		var scope = Peek();
		if (scope != null && scope.TryGetValue(expr.Name, out bool b) && !b)
		{
			Error(expr.Status, "Can't read local variable in its own initializer.");
		}
		ResolveLocal(expr, expr.Name);
		return null;
	}
	private object? ResolveLocal(Expr expr, string name)
	{
		for (int i = stackLen - 1; i >= 0; i--)
		{
			if (scopes[i].ContainsKey(name))
			{
				var dist = stackLen - 1 - i;
				interpreter.resolve(expr,dist );
				return null;
			}
		}
		return null;
	}
	void Error(InputStatus? status, string msg)
	{
		status ??= interpreter.currentStatus;
		throw new EleuResolverError(status, "Cerr: " + msg);
	}

	public object? VisitVarStmt(Stmt.Var stmt)
	{
		Declare(stmt.Name);
		if (stmt.Initializer != null)
		{
			Resolve(stmt.Initializer);
		}
		define(stmt.Name);
		return null;
	}
	private void Declare(string name)
	{
		var scope = Peek();
		if (scope == null) return;
		if (scope.ContainsKey(name))
		{
			Error(null, $"Eine Variable mit dem Namen '{name}' existiert in diesem Geltungsbereich schon!");
		}
		scope[name] = false;
	}

	private void define(string name)
	{
		var scope = Peek();
		if (scope == null) return;
		scope[name] = true;
	}

	public object? VisitWhileStmt(Stmt.While stmt)
	{
		Resolve(stmt.Condition);
		loopLevel++;
		Resolve(stmt.Body);
		Resolve(stmt.Increment);
		loopLevel--;
		return null;
	}
	public object? VisitRepeatStmt(Stmt.Repeat stmt)
	{
		Resolve(stmt.Count);
		loopLevel++;
		Resolve(stmt.Body);
		loopLevel--;
		return null;
	}
	public object? VisitAssertStmt(Stmt.Assert stmt) => Resolve(stmt.expression);
	public object? VisitBreakContinueStmt(Stmt.BreakContinue stmt)
	{
		if (loopLevel == 0)
		{
			var s = stmt.IsBreak ? "break" : "continue";
			Error(stmt.Status, $"'{s}' ist hier nicht erlaubt.");
		}
		return null;
	}

}
