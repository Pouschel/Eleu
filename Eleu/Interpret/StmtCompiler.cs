
using System.Text;

namespace Eleu.Interpret;

class Chunk
{
	public List<Instruction> code = new();
	public void add(Instruction ins) => code.Add(ins);
	public int length => code.Count;

	public override string ToString()
	{
		var sb = new StringBuilder();
		int scopeDepth = 0;
		for (int i = 0; i < code.Count; i++)
		{
			var ins = code[i];
			var s = new string(' ', scopeDepth * 2);
			sb.AppendLine($"{i,4}: {s}{ins}");
			if (ins is ScopeInstruction sins) scopeDepth += sins.begin ? 1 : -1;
		}
		return sb.ToString();
	}
}

internal class StmtCompiler : Expr.Visitor<object>, Stmt.Visitor<object>
{
	private static object nothing = new();

	Chunk chunk = new();
	List<JumpInstruction> breakContinues = new();
	int scopeDepth = 0;
	public bool isInitializer = false;

	public Chunk compile(List<Stmt> stmts)
	{
		foreach (var stmt in stmts)
		{
			stmt.Accept(this);
		}
		//Console.WriteLine(chunk.ToString());
		return chunk;
	}

	object emit(Instruction ins)
	{
		chunk.add(ins); return nothing;
	}

	public object VisitAssignExpr(Expr.Assign expr)
	{
		expr.Value.Accept(this);
		return emit(new AssignInstruction(expr.Name, expr.localDistance, expr.Status));
	}

	public object VisitBinaryExpr(Expr.Binary expr)
	{
		expr.Left.Accept(this);
		expr.Right.Accept(this);
		return emit(new BinaryOpInstruction(expr.Op.Type, expr.Status));
	}

	public object VisitCallExpr(Expr.Call expr)
	{
		for (int i = 0; i < expr.Arguments.Count; i++)
		{
			var argument = expr.Arguments[i];
			argument.Accept(this);
		}
		expr.Callee.Accept(this);
		return emit(new CallInstruction(expr.Arguments.Count, expr.Status));
	}

	public object VisitGetExpr(Expr.Get expr)
	{
		expr.Obj.Accept(this);
		return emit(new GetInstruction(expr.Name, expr.Status));
	}

	public object VisitGroupingExpr(Expr.Grouping expr) => expr.Expression.Accept(this);
	public object VisitLiteralExpr(Expr.Literal expr)
	{
		var value = expr.Value ?? NilValue;
		return emit(new PushInstruction(value, expr.Status));
	}
	public object VisitLogicalExpr(Expr.Logical expr)
	{
		expr.Left.Accept(this);
		expr.Right.Accept(this);
		return emit(new LogicalOpInstruction(expr.Op, expr.Status));
	}
	public object VisitSetExpr(Expr.Set expr)
	{
		expr.Obj.Accept(this);
		expr.Value.Accept(this);
		return emit(new SetInstruction(expr.Name, expr.Status));
	}
	public object VisitSuperExpr(Expr.Super expr)
	{
		var distance = expr.localDistance;
		if (distance < 0) distance = 0;
		return emit(new SuperInstruction(expr.Method, distance, expr.Status));
	}
	public object VisitThisExpr(Expr.This expr)
		=> emit(new LookupVarInstruction(expr.Keyword, expr.localDistance, expr.Status));
	public object VisitUnaryExpr(Expr.Unary expr)
	{
		expr.Right.Accept(this);
		return emit(new UnaryOpInstruction(expr.Op.Type, expr.Status));
	}
	public object VisitVariableExpr(Expr.Variable expr)
		=> emit(new LookupVarInstruction(expr.Name, expr.localDistance, expr.Status));
	public object VisitBlockStmt(Stmt.Block stmt)
	{
		emit(new ScopeInstruction(true));
		scopeDepth++;
		foreach (var s in stmt.Statements)
		{
			s.Accept(this);
		}
		scopeDepth--;
		return emit(new ScopeInstruction(false));
	}

	public object VisitClassStmt(Stmt.Class stmt)
	{
		if (stmt.Superclass != null)
		{
			stmt.Superclass!.Accept(this);
			emit(new PushInstruction(true, stmt.Status));
		}
		else
			emit(new PushInstruction(false, stmt.Status));
		return emit(new ClassInstruction(stmt.Name, stmt.Methods, stmt.Status));
	}

	public object VisitExpressionStmt(Stmt.Expression stmt)
	{
		stmt.expression.Accept(this);
		return emit(new PopInstruction(stmt.expression.Status));
	}

	public object VisitFunctionStmt(Stmt.Function stmt) => emit(new DefFunInstruction(stmt));
	public object VisitIfStmt(Stmt.If stmt)
	{
		stmt.Condition.Accept(this);
		var thenJump = new JumpInstruction(JumpMode.jmp_false, stmt.Condition.Status);
		emit(thenJump);
		emit(new PopInstruction(stmt.Condition.Status));
		stmt.ThenBranch.Accept(this);
		var elseJump = new JumpInstruction(JumpMode.jmp, stmt.ThenBranch.Status);
		emit(elseJump);
		thenJump.offset = chunk.length;
		emit(new PopInstruction(stmt.ThenBranch.Status));
		if (stmt.ElseBranch != null) stmt.ElseBranch!.Accept(this);
		elseJump.offset = chunk.length;
		return nothing;
	}

	public object VisitAssertStmt(Stmt.Assert stmt)
	{
		if (!stmt.isErrorAssert)
		{
			stmt.expression.Accept(this);
			emit(new AssertInstruction(stmt.Status));
			return nothing;
		}
		throw new EleuRuntimeError(stmt.Status, "assert break not supported");
	}

	public object VisitReturnStmt(Stmt.Return stmt)
	{
		if (!isInitializer)
		{
			if (stmt.Value != null)
				stmt.Value!.Accept(this);
			else
				emit(new PushInstruction(NilValue, stmt.Keyword.Status));
		}
		else
			emit(new LookupVarInstruction("this", 1, stmt.Status));
		return emit(new ReturnInstruction(this.scopeDepth, stmt.Status));
	}

	public object VisitBreakContinueStmt(Stmt.BreakContinue stmt)
	{
		var jump = new JumpInstruction(stmt.IsBreak ? JumpMode.jmp_true : JumpMode.jmp_false, stmt.Status);
		breakContinues.Add(jump);
		jump.leaveScopes = scopeDepth;
		return emit(jump);
	}

	public object VisitVarStmt(Stmt.Var stmt)
	{
		if (stmt.Initializer != null)
			stmt.Initializer!.Accept(this);
		else
			emit(new PushInstruction(NilValue, stmt.Status));
		return emit(new VarDefInstruction(stmt.Name, stmt.Status));
	}

	public object VisitWhileStmt(Stmt.While stmt)
	{
		var oldBreaks = breakContinues;
		breakContinues = new();
		int loopStart = chunk.length;
		stmt.Condition.Accept(this);
		var exitJump = new JumpInstruction(JumpMode.jmp_false, stmt.Condition.Status);
		emit(exitJump);
		emit(new PopInstruction(stmt.Condition.Status));
		stmt.Body.Accept(this);
		int incrementOfs = chunk.length;
		if (stmt.Increment != null)
		{
			stmt.Increment!.Accept(this);
			emit(new PopInstruction(stmt.Increment!.Status));
		}
		var jsi = new JumpInstruction(JumpMode.jmp, stmt.Condition.Status)
		{ offset = loopStart };
		emit(jsi);
		exitJump.offset = chunk.length;
		patchBreakContinues(exitJump.offset, incrementOfs);
		breakContinues = oldBreaks;
		return nothing;
	}

	public object VisitRepeatStmt(Stmt.Repeat stmt)
	{
		var oldBreaks = breakContinues;
		breakContinues = new();
		stmt.Count.Accept(this);
		int repeatIndex = chunk.length;
		var endJump = new JumpInstruction(JumpMode.jmp_le_zero, stmt.Count.Status);
		emit(endJump);
		stmt.Body.Accept(this);
		int incrPos = chunk.length;
		emit(new PushInstruction(new Eleu.Types.Number(1), stmt.Count.Status));
		emit(new BinaryOpInstruction(TokenType.TokenMinus, stmt.Count.Status));
		var jsi = new JumpInstruction(JumpMode.jmp, stmt.Count.Status);
		emit(jsi);
		jsi.offset = repeatIndex;
		endJump.offset = chunk.length;
		emit(new PopInstruction(stmt.Status));
		patchBreakContinues(endJump.offset, incrPos);
		breakContinues = oldBreaks;
		return nothing;
	}

	void patchBreakContinues(int breakOfs, int continueOfs)
	{
		foreach (var jmp in breakContinues)
		{
			jmp.leaveScopes -= this.scopeDepth;
			if (jmp.mode == JumpMode.jmp_true)
				jmp.offset = breakOfs;
			else
				jmp.offset = continueOfs;
			jmp.mode = JumpMode.jmp;
		}
	}
}
