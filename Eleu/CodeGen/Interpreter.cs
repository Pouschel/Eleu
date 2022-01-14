using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eleu.CodeGen
{

	public struct InterpretResult
	{
		public enum Status
		{
			Normal
		}


		public readonly Value Value;
		public readonly Status Stat;

		public InterpretResult(Value val, Status stat = Status.Normal)
		{
			Value = val;
			this.Stat = stat;
		}

		public static implicit operator InterpretResult(in Value val) => new(val);
	}

	internal class Interpreter : IInterpreter, Expr.Visitor<Value>
	{
		protected List<Stmt> statements;
		protected EleuOptions options;

		public Interpreter(EleuOptions options, List<Stmt> statements)
		{
			this.statements = statements;
			this.options = options;
		}

		public EleuRuntimeError Error(string message)
		{
			//options.Err.WriteLine(message);
			return new EleuRuntimeError(message);
		}
		private Value Evaluate(Expr expr) => expr.Accept(this);

		public Value VisitAssignExpr(Expr.Assign expr) => throw new NotImplementedException();
		public Value VisitBinaryExpr(Expr.Binary expr) => throw new NotImplementedException();
		public Value VisitCallExpr(Expr.Call expr) => throw new NotImplementedException();
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

		public Value VisitLogicalExpr(Expr.Logical expr) => throw new NotImplementedException();
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

		public Value VisitVariableExpr(Expr.Variable expr) => throw new NotImplementedException();
		public EEleuResult Interpret()
		{
			EEleuResult result = EEleuResult.Ok;
			try
			{
				foreach (var stmt in this.statements)
				{
					if (stmt is Stmt.Expression sex)
						Evaluate(sex.expression);
				}
			}
			catch (EleuRuntimeError ex)
			{
				options.Err.WriteLine(ex.Message);
				result = RuntimeError;
			}
			return result;
		}
	}
}
