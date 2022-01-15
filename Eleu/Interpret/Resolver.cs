using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eleu.CodeGen;

namespace Eleu.Interpret
{
	internal class Resolver : Expr.Visitor<object?>, Stmt.Visitor<object?>
	{
		private Interpreter interpreter;
		private List<Dictionary<string, bool>> scopes = new();
		private int stackLen;
		private FunctionType currentFunction = FunTypeScript;

		void Push(Dictionary<string, bool> d)
		{
			if (stackLen < scopes.Count)
				scopes[stackLen] = d;
			else 
				scopes.Add(d);
			stackLen++;
		}

		void Pop() => stackLen--;
		Dictionary<string, bool>? Peek() => stackLen>0 ? scopes[stackLen - 1]: null;

		public Resolver(Interpreter interpreter)
		{
			this.interpreter = interpreter;
		}

		public object? VisitAssignExpr(Expr.Assign expr)
		{
			resolve(expr.Value);
			resolveLocal(expr, expr.Name);
			return null;
		}

		public object? VisitBinaryExpr(Expr.Binary expr)
		{
			resolve(expr.Left);
			resolve(expr.Right);
			return null;
		}

		public object? VisitBlockStmt(Stmt.Block stmt)
		{
			beginScope();
			resolve(stmt.Statements);
			endScope();
			return null;
		}
		public void resolve(List<Stmt> statements)
		{
			foreach (Stmt statement in statements)
			{
				resolve(statement);
			}
		}

		private object? resolve(Stmt stmt) => stmt.Accept(this);

		private object? resolve(Expr? expr) => expr?.Accept(this);
		private void beginScope() => Push(new());
		private void endScope() => Pop();

		public object? VisitCallExpr(Expr.Call expr)
		{
			resolve(expr.Callee);
			foreach (Expr argument in expr.Arguments)
			{
				resolve(argument);
			}
			return null;
		}

		public object? VisitClassStmt(Stmt.Class stmt)
		{
			declare(stmt.Name);
			define(stmt.Name);
			return null;
		}

		public object? VisitExpressionStmt(Stmt.Expression stmt) => resolve(stmt.expression);
		public object? VisitFunctionStmt(Stmt.Function stmt)
		{
			declare(stmt.Name);
			define(stmt.Name);
			resolveFunction(stmt, FunTypeFunction);
			return null;
		}
		private void resolveFunction(Stmt.Function function, FunctionType type)
		{
			FunctionType enclosingFunction = currentFunction;
			currentFunction = type;
			beginScope();
			foreach (var param in function.Paras)
			{
				declare(param.StringValue);
				define(param.StringValue);
			}
			resolve(function.Body);
			endScope();
			currentFunction = enclosingFunction;
		}

		public object? VisitGetExpr(Expr.Get expr) => throw new NotImplementedException();
		public object? VisitGroupingExpr(Expr.Grouping expr) => resolve(expr.Expression);
		public object? VisitIfStmt(Stmt.If stmt)
		{
			resolve(stmt.Condition);
			resolve(stmt.ThenBranch);
			if (stmt.ElseBranch != null) resolve(stmt.ElseBranch);
			return null;
		}
		public object? VisitLiteralExpr(Expr.Literal expr) => null;
		public object? VisitLogicalExpr(Expr.Logical expr)
		{
			resolve(expr.Left);
			resolve(expr.Right);
			return null;
		}
		public object? VisitPrintStmt(Stmt.Print stmt) => resolve(stmt.expression);
		public object? VisitReturnStmt(Stmt.Return stmt)
		{
			if (currentFunction == FunTypeScript)
			{
				error("Can't return from top-level code.");
			}
			return resolve(stmt.Value);
		}

		public object? VisitSetExpr(Expr.Set expr) => throw new NotImplementedException();
		public object? VisitSuperExpr(Expr.Super expr) => throw new NotImplementedException();
		public object? VisitThisExpr(Expr.This expr) => throw new NotImplementedException();
		public object? VisitUnaryExpr(Expr.Unary expr) => resolve(expr.Right);
		public object? VisitVariableExpr(Expr.Variable expr)
		{
			var scope = Peek();
			if (scope!=null && scope.TryGetValue(expr.Name, out bool b) && !b)
			{
				error("Can't read local variable in its own initializer.");
			}
			resolveLocal(expr, expr.Name);
			return null;
		}
		private void resolveLocal(Expr expr, string name)
		{
			for (int i = stackLen - 1; i >= 0; i--)
			{

				if (scopes[i].ContainsKey(name))
				{
					interpreter.resolve(expr, stackLen - 1 - i);
					return;
				}
			}
		}
		void error(string msg) => interpreter.RuntimeError(msg);

		public object? VisitVarStmt(Stmt.Var stmt)
		{
			declare(stmt.Name);
			if (stmt.Initializer != null)
			{
				resolve(stmt.Initializer);
			}
			define(stmt.Name);
			return null;
		}
		private void declare(string name)
		{
			var scope = Peek();
			if (scope == null) return;
			if (scope.ContainsKey(name))
			{
				error("Already a variable with this name in this scope.");
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
			resolve(stmt.Condition);
			resolve(stmt.Body);
			return null;
		}


	}
}
