namespace Eleu.Interpret;

internal class Interpreter : IInterpreter, Expr.Visitor<Value>, Stmt.Visitor<InterpretResult>
{
	private List<Stmt> statements;
	private EleuOptions options;
	internal EleuEnvironment globals = new();
	private EleuEnvironment environment;
	private Dictionary<Expr, int> locals = new();
	private CancellationToken ctoken = CancellationToken.None;

	public Interpreter(EleuOptions options, List<Stmt> statements)
	{
		this.environment = globals;
		this.statements = statements;
		this.options = options;
		var _ = new NativeFunctions(this);
	}

	public void DefineNative(string name, NativeFn function)
	{
		var ofun = CreateObjVal(new ObjNative(function));
		globals.Define(name, ofun);
	}
	public EEleuResult InterpretWithDebug(CancellationToken token)
	{
		this.ctoken = token;
		return Interpret();
	}

	public EEleuResult Interpret()
	{
		EEleuResult result = Ok;
		try
		{
			locals = new Dictionary<Expr, int>();
			var resolver = new Resolver(this);
			resolver.resolve(this.statements);
			resolver = null;
			foreach (var stmt in this.statements)
			{
				Execute(stmt);
			}
		}
		catch (EleuRuntimeError ex)
		{
			options.Err.WriteLine("Rerr: " + ex.Message);
			result = EEleuResult.RuntimeError;
		}
		catch (OperationCanceledException)
		{
			result = EEleuResult.RuntimeError;
		}
		return result;
	}
	public EleuRuntimeError Error(string message)
	{
		//options.Err.WriteLine(message);
		return new EleuRuntimeError(message);
	}
	private Value Evaluate(Expr expr) => expr.Accept(this);

	private InterpretResult Execute(Stmt stmt)
	{
		ctoken.ThrowIfCancellationRequested();
		return stmt.Accept(this);
	}

	public Value VisitAssignExpr(Expr.Assign expr)
	{
		var value = Evaluate(expr.Value);
		if (locals.TryGetValue(expr, out var distance))
		{
			environment.assignAt(distance, expr.Name, value);
		}
		else
		{
			globals.Assign(expr.Name, value);
		}
		return value;
	}

	public Value VisitBinaryExpr(Expr.Binary expr)
	{
		var lhs = Evaluate(expr.Left);
		var rhs = Evaluate(expr.Right);
		return expr.Op.type switch
		{
			TOKEN_BANG_EQUAL => lhs != rhs,
			TOKEN_EQUAL_EQUAL => lhs == rhs,
			TOKEN_GREATER => lhs > rhs,
			TOKEN_GREATER_EQUAL => lhs >= rhs,
			TOKEN_LESS => lhs < rhs,
			TOKEN_LESS_EQUAL => lhs <= rhs,
			TokenPlus => lhs + rhs,
			TokenMinus => lhs - rhs,
			TokenStar => lhs * rhs,
			TokenPercent => lhs % rhs,
			TOKEN_SLASH => lhs / rhs,
			_ => throw Error("Invalid op: " + expr.Op.type),
		};
	}

	public Value VisitCallExpr(Expr.Call expr)
	{
		var callee = Evaluate(expr.Callee);
		if (callee.oValue is not LoxCallable function)
		{
			throw new EleuRuntimeError("Can only call functions and classes.");
		}
		if (expr.Arguments.Count != function.arity())
			throw new EleuRuntimeError("Expected " + function.arity() + " arguments but got " + expr.Arguments.Count + ".");
		var arguments = new Value[expr.Arguments.Count];
		for (int i = 0; i < expr.Arguments.Count; i++)
		{
			var argument = expr.Arguments[i];
			arguments[i] = Evaluate(argument);
		}
		return function.Call(this, arguments);
	}

	public Value VisitGetExpr(Expr.Get expr) => throw new NotImplementedException();
	public Value VisitGroupingExpr(Expr.Grouping expr) => Evaluate(expr.Expression);
	public Value VisitLiteralExpr(Expr.Literal expr)
	{
		return expr.Value switch
		{
			bool b => b ? BoolTrue : BoolFalse,
			null => Nil,
			double d => CreateNumberVal(d),
			string s => CreateStringVal(s),
			_ => throw Error($"Unsupported constant of type: {expr.Value}"),
		};
	}

	public Value VisitLogicalExpr(Expr.Logical expr)
	{
		var left = Evaluate(expr.Left);
		if (expr.Op.type == TOKEN_OR)
		{
			if (IsTruthy(left)) return left;
		}
		else
		{
			if (IsFalsey(left)) return left;
		}
		return Evaluate(expr.Right);
	}

	public Value VisitSetExpr(Expr.Set expr) => throw new NotImplementedException();
	public Value VisitSuperExpr(Expr.Super expr) => throw new NotImplementedException();
	public Value VisitThisExpr(Expr.This expr) => throw new NotImplementedException();
	public Value VisitUnaryExpr(Expr.Unary expr)
	{
		var right = Evaluate(expr.Right);
		return expr.Op.type switch
		{
			TOKEN_BANG => !right,
			TokenMinus => -right,
			_ => throw Error("Unknown op type: " + expr.Op.type),// Unreachable.
		};
	}

	internal void resolve(Expr expr, int depth)
	{
		locals[expr] = depth;
	}

	public Value VisitVariableExpr(Expr.Variable expr) => lookUpVariable(expr.Name, expr);
	private Value lookUpVariable(string name, Expr expr)
	{
		if (locals.TryGetValue(expr, out int distance))
		{
			return environment.getAt(distance, name);
		}
		else
		{
			return globals.Get(name);
		}
	}

	public InterpretResult VisitBlockStmt(Stmt.Block stmt) => executeBlock(stmt.Statements, new EleuEnvironment(environment));

	internal InterpretResult executeBlock(List<Stmt> statements, EleuEnvironment environment)
	{
		var previous = this.environment;
		InterpretResult result = Nil;
		try
		{
			this.environment = environment;
			foreach (Stmt statement in statements)
			{
				result = Execute(statement);
				if (result.Stat == InterpretResult.Status.Return) break;
			}
			return result;
		}
		finally
		{
			this.environment = previous;
		}
	}

	public InterpretResult VisitClassStmt(Stmt.Class stmt)
	{
		environment.Define(stmt.Name, Nil);
		var klass = new LoxClass(stmt.Name);
		var kval = new Value(VAL_OBJ, klass);
		environment.Assign(stmt.Name, kval);
		return kval;
	}

	public InterpretResult VisitExpressionStmt(Stmt.Expression stmt) => Evaluate(stmt.expression);

	public InterpretResult VisitFunctionStmt(Stmt.Function stmt)
	{
		LoxFunction function = new LoxFunction(stmt, environment);
		var val = new Value(VAL_OBJ, function);
		environment.Define(stmt.Name, val);
		return val;
	}

	public InterpretResult VisitIfStmt(Stmt.If stmt)
	{
		if (IsTruthy(Evaluate(stmt.Condition)))
			return Execute(stmt.ThenBranch);
		else if (stmt.ElseBranch != null)
			return Execute(stmt.ElseBranch);
		return Nil;
	}

	public InterpretResult VisitPrintStmt(Stmt.Print stmt)
	{
		var val = Evaluate(stmt.expression);
		options.Out.WriteLine(val);
		return val;
	}

	public InterpretResult VisitReturnStmt(Stmt.Return stmt)
	{
		var val = Nil;
		if (stmt.Value != null) val = Evaluate(stmt.Value);
		return new InterpretResult(val, InterpretResult.Status.Return);
	}

	public InterpretResult VisitVarStmt(Stmt.Var stmt)
	{
		Value value = Nil;
		if (stmt.Initializer != null)
		{
			value = Evaluate(stmt.Initializer);
		}
		environment.Define(stmt.Name, value);
		return value;
	}
	public InterpretResult VisitWhileStmt(Stmt.While stmt)
	{
		var result = new InterpretResult(Nil);
		while (IsTruthy(Evaluate(stmt.Condition)))
		{
			result = Execute(stmt.Body);
			if (result.Stat != InterpretResult.Status.Normal) 
				break;
		}
		return result;
	}

	public void RuntimeError(string msg) => throw new EleuRuntimeError(msg);
	
}
