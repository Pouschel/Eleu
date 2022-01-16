namespace Eleu.Interpret;

using System.Diagnostics;
using static Eleu.Interpret.InterpreterStatics;

internal class Interpreter : IInterpreter, Expr.Visitor<object>, Stmt.Visitor<InterpretResult>
{
	private List<Stmt> statements;
	private EleuOptions options;
	internal EleuEnvironment globals = new();
	private EleuEnvironment environment;
	private Dictionary<Expr, int> locals = new();
	private CancellationToken ctoken = CancellationToken.None;
	InputStatus currentStatus;

	public Interpreter(EleuOptions options, List<Stmt> statements)
	{
		this.environment = globals;
		this.statements = statements;
		this.options = options;
		var _ = new NativeFunctions(this);
	}

	public int InstructionCount
	{
		get;
		private set;
	}

	public void DefineNative(string name, NativeFn function)
	{
		var ofun = new LoxNative(function);
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
		InstructionCount = 0;
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
			var msg = $"{currentStatus.Message}: {ex.Message}";
			options.Err.WriteLine(msg);
			Trace.WriteLine(msg);
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
	private object Evaluate(Expr expr)
	{
		InstructionCount++;
		var evaluated = expr.Accept(this);
		return evaluated;
	}

	private InterpretResult Execute(Stmt stmt)
	{
		ctoken.ThrowIfCancellationRequested();
		if (stmt.Status.HasValue)
			currentStatus = stmt.Status.Value;
		InstructionCount++;
		return stmt.Accept(this);
	}

	public object VisitAssignExpr(Expr.Assign expr)
	{
		var value = Evaluate(expr.Value);
		if (locals.TryGetValue(expr, out var distance))
		{
			environment.AssignAt(distance, expr.Name, value);
		}
		else
		{
			globals.Assign(expr.Name, value);
		}
		return value;
	}

	public object VisitBinaryExpr(Expr.Binary expr)
	{
		var lhs = Evaluate(expr.Left);
		var rhs = Evaluate(expr.Right);
		return expr.Op.type switch
		{
			TOKEN_BANG_EQUAL => !ObjEquals(lhs, rhs),
			TOKEN_EQUAL_EQUAL => ObjEquals(lhs, rhs),
			TOKEN_GREATER => InternalCompare(lhs, rhs) > 0,
			TOKEN_GREATER_EQUAL => InternalCompare(lhs, rhs) >= 0,
			TOKEN_LESS => InternalCompare(lhs, rhs) < 0,
			TOKEN_LESS_EQUAL => InternalCompare(lhs, rhs) <= 0,
			TokenPlus => NumStrOp(lhs, rhs, (a, b) => a + b, (a, b) => a + b),
			TokenMinus => NumberOp(lhs, rhs, (a, b) => a - b),
			TokenStar => NumberOp(lhs, rhs, (a, b) => a * b),
			TokenPercent => NumberOp(lhs, rhs, (a, b) => a % b),
			TOKEN_SLASH => NumberOp(lhs, rhs, (a, b) => a / b),
			_ => throw Error("Invalid op: " + expr.Op.type),
		};
	}

	public object VisitCallExpr(Expr.Call expr)
	{
		var callee = Evaluate(expr.Callee);
		if (callee is not LoxCallable function)
		{
			throw new EleuRuntimeError("Can only call functions and classes.");
		}
		if (expr.Arguments.Count != function.arity())
			throw new EleuRuntimeError("Expected " + function.arity() + " arguments but got " + expr.Arguments.Count + ".");
		var arguments = new object[expr.Arguments.Count];
		for (int i = 0; i < expr.Arguments.Count; i++)
		{
			var argument = expr.Arguments[i];
			arguments[i] = Evaluate(argument);
		}
		return function.Call(this, arguments);
	}

	public object VisitGetExpr(Expr.Get expr)
	{
		var obj = Evaluate(expr.Obj);
		if (obj is LoxInstance inst)
		{
			return inst.get(expr.Name);
		}
		throw Error("Only instances have properties.");
	}

	public object VisitGroupingExpr(Expr.Grouping expr) => Evaluate(expr.Expression);
	public object VisitLiteralExpr(Expr.Literal expr)
	{
		if (expr.Value == null) return Nil;
		return expr.Value;
		//return expr.Value switch
		//{
		//	bool b => b ? BoolTrue : BoolFalse,
		//	null => NilValue,
		//	double d => CreateNumberVal(d),
		//	string s => CreateStringVal(s),
		//	_ => throw Error($"Unsupported constant of type: {expr.Value}"),
		//};
	}

	public object VisitLogicalExpr(Expr.Logical expr)
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

	public object VisitSetExpr(Expr.Set expr)
	{
		var obj = Evaluate(expr.Obj);
		if (obj is not LoxInstance li)
		{
			throw Error("Only instances have fields.");
		}
		var value = Evaluate(expr.Value);
		li.set(expr.Name, value);
		return value;
	}

	public object VisitSuperExpr(Expr.Super expr)
	{
		int distance = locals[expr];
		LoxClass superclass = (LoxClass)environment.GetAt("super", distance);
		LoxInstance obj = (LoxInstance)environment.GetAt("this", distance - 1);
		LoxFunction? method = superclass.findMethod(expr.Method) as LoxFunction;
		if (method == null)
		{
			throw Error("Undefined property '" + expr.Method + "'.");
		}
		return method.bind(obj);
	}

	public object VisitThisExpr(Expr.This expr)
	{
		return LookUpVariable(expr.Keyword, expr);
	}

	public object VisitUnaryExpr(Expr.Unary expr)
	{
		var right = Evaluate(expr.Right);
		switch (expr.Op.type)
		{
			case TOKEN_BANG: return !IsTruthy(right);
			case TokenMinus:
				{
					if (right is not double d) throw Error("Operand must be a number.");
					return -d;
				}
			default: throw Error("Unknown op type: " + expr.Op.type);// Unreachable.
		};
	}

	internal void resolve(Expr expr, int depth)
	{
		locals[expr] = depth;
	}

	public object VisitVariableExpr(Expr.Variable expr) => LookUpVariable(expr.Name, expr);
	private object LookUpVariable(string name, Expr expr)
	{
		if (locals.TryGetValue(expr, out int distance))
		{
			return environment.GetAt(name, distance);
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
		InterpretResult result = InterpretResult.NilResult;
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
		LoxClass? superclass = null;
		if (stmt.Superclass != null)
		{
			var superclassV = Evaluate(stmt.Superclass);
			if (superclassV is not LoxClass)
			{
				throw new EleuRuntimeError("Superclass must be a class.");
			}
			superclass = superclassV as LoxClass;
		}
		environment.Define(stmt.Name, Nil);
		if (superclass != null)
		{
			environment = new EleuEnvironment(environment);
			environment.Define("super", superclass);
		}
		var klass = new LoxClass(stmt.Name, superclass);
		foreach (Stmt.Function method in stmt.Methods)
		{
			LoxFunction function = new LoxFunction(method, environment, method.Name == "init");
			klass.Methods.Set(method.Name, function);
		}
		var kval = klass;
		if (superclass != null)
		{
			environment = environment.enclosing!;
		}
		environment.Assign(stmt.Name, kval);
		return new InterpretResult(kval);
	}

	public InterpretResult VisitExpressionStmt(Stmt.Expression stmt) => new(Evaluate(stmt.expression));

	public InterpretResult VisitFunctionStmt(Stmt.Function stmt)
	{
		LoxFunction function = new LoxFunction(stmt, environment, false);
		environment.Define(stmt.Name, function);
		return new(function);
	}

	public InterpretResult VisitIfStmt(Stmt.If stmt)
	{
		if (IsTruthy(Evaluate(stmt.Condition)))
			return Execute(stmt.ThenBranch);
		else if (stmt.ElseBranch != null)
			return Execute(stmt.ElseBranch);
		return InterpretResult.NilResult;
	}

	public InterpretResult VisitPrintStmt(Stmt.Print stmt)
	{
		var val = Evaluate(stmt.expression);
		var s = val switch
		{
			bool b => b ? "true" : "false",
			object o when o == Nil => "nil",
			_ => val.ToString(),
		};
		options.Out.WriteLine(s);
		return new(val);
	}

	public InterpretResult VisitReturnStmt(Stmt.Return stmt)
	{
		var val = Nil;
		if (stmt.Value != null) val = Evaluate(stmt.Value);
		return new InterpretResult(val, InterpretResult.Status.Return);
	}

	public InterpretResult VisitVarStmt(Stmt.Var stmt)
	{
		var value = Nil;
		if (stmt.Initializer != null)
		{
			value = Evaluate(stmt.Initializer);
		}
		environment.Define(stmt.Name, value);
		return new(value);
	}
	public InterpretResult VisitWhileStmt(Stmt.While stmt)
	{
		var result = InterpretResult.NilResult;
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
