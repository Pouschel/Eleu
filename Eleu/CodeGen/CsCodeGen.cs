using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eleu.CodeGen
{
	
	static class CsCodeGenHelper
	{
		public static void OpenBlock(this IndentedTextWriter tw, char c='{')
		{
			tw.WriteLine(c); tw.Indent++;
		}

		public static void CloseBlock(this IndentedTextWriter tw, char c = '}')
		{
			tw.Indent--;
			tw.WriteLine(c); 
		}

	}

	internal class CsCodeGen : CodeGenBase, Expr.Visitor<string>, Stmt.Visitor<bool>
	{
		class State
		{
			State? parent;
			FunctionType type;
			string funName;
			List<State> childStates = new();
			List<string> localNames = new();
			public readonly IndentedTextWriter Twcode;

			public State(FunctionType type, string funName, State? parent)
			{
				this.type = type;
				this.parent = parent;
				this.funName = funName;
				this.Twcode = new IndentedTextWriter(new StringWriter(),"\t");
			}

			public void WriteTo(IndentedTextWriter tw)
			{
				if (type==FunctionType.FunTypeScript)
				{
					tw.WriteLine("using static Eleu.ValueStatics;");
					tw.WriteLine();
					tw.WriteLine($"class {funName}");
					tw.OpenBlock();
				}
				tw.WriteLine($"static Value {funName}()");
				tw.OpenBlock();
				var s = (Twcode.InnerWriter as StringWriter)!.ToString();
				tw.Write(s);
				tw.WriteLine("return Nil;");
				tw.CloseBlock();
				if (type==FunctionType.FunTypeScript)
				{
					tw.CloseBlock();
				}
			}
		}
		
		State current;
		
		public CsCodeGen(EleuOptions options, List<Stmt> statements) : base(options, statements)
		{
			current = new State(FunTypeScript,"Scipt",null);
		}

		public bool GenCode()
		{

			foreach (var stm in statements)
			{
				try
				{
					stm.Accept(this);
				}
				catch (CodeGenError)
				{
					return false;
				}
			}
			using var tw = File.CreateText(options.CsOutputFile!);
			var idtw = new IndentedTextWriter(tw,"\t");
			current.WriteTo(idtw);
			return true;
		}

		public bool VisitBlockStmt(Stmt.Block stmt) => throw new NotImplementedException();
		public bool VisitClassStmt(Stmt.Class stmt) => throw new NotImplementedException();
		public bool VisitExpressionStmt(Stmt.Expression stmt)
		{
			var sex= stmt.expression.Accept(this);
			current.Twcode.Write(sex);
			current.Twcode.WriteLine(';');
			return true;
		}

		public bool VisitFunctionStmt(Stmt.Function stmt) => throw new NotImplementedException();
		public bool VisitIfStmt(Stmt.If stmt) => throw new NotImplementedException();
		public bool VisitPrintStmt(Stmt.Print stmt) => throw new NotImplementedException();
		public bool VisitReturnStmt(Stmt.Return stmt) => throw new NotImplementedException();
		public bool VisitVarStmt(Stmt.Var stmt) => throw new NotImplementedException();
		public bool VisitWhileStmt(Stmt.While stmt) => throw new NotImplementedException();
		public string VisitAssignExpr(Expr.Assign expr) => throw new NotImplementedException();
		public string VisitBinaryExpr(Expr.Binary expr) => throw new NotImplementedException();
		public string VisitCallExpr(Expr.Call expr) => throw new NotImplementedException();
		public string VisitGetExpr(Expr.Get expr) => throw new NotImplementedException();
		public string VisitGroupingExpr(Expr.Grouping expr) => throw new NotImplementedException();
		public string VisitLiteralExpr(Expr.Literal expr)
		{
			switch (expr.Value)
			{
				case bool b: return b ? "BoolTrue" : "BoolFalse";
				case null: return "Nil";
				case double d: return $"CreateNumberVal({d})";
				case string s: return $"CreateStringVal({s})";
				default: Error($"Unsupported constant of type: {expr.Value}"); return "";
			}
		}

		public string VisitLogicalExpr(Expr.Logical expr) => throw new NotImplementedException();
		public string VisitSetExpr(Expr.Set expr) => throw new NotImplementedException();
		public string VisitSuperExpr(Expr.Super expr) => throw new NotImplementedException();
		public string VisitThisExpr(Expr.This expr) => throw new NotImplementedException();
		public string VisitUnaryExpr(Expr.Unary expr) => throw new NotImplementedException();
		public string VisitVariableExpr(Expr.Variable expr) => throw new NotImplementedException();
	}
}
