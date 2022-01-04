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
			string funName, clsName;
			List<State> childStates = new();
			List<string> localInitNames = new();
			public readonly IndentedTextWriter Twcode;
			internal readonly CompilerState cstate;

			public State(FunctionType type, string clsName, string funName, State? parent)
			{
				this.type = type;
				this.parent = parent;
				this.funName = funName;
				this.clsName = clsName;
				this.Twcode = new IndentedTextWriter(new StringWriter(),"\t");
				cstate = new CompilerState(type);
			}

			void WriteLocals(IndentedTextWriter tw, bool fStatic)
			{
				var sStatic = fStatic ? "static " : "";
				foreach (var loc in localInitNames)
				{
					tw.WriteLine($"{sStatic}Value {loc};");
				}
			}
			public void WriteTo(IndentedTextWriter tw)
			{
				if (type==FunctionType.FunTypeScript)
				{
					tw.WriteLine("using Eleu;");
					tw.WriteLine("using static Eleu.ValueStatics;");
					tw.WriteLine();
					tw.WriteLine($"public class {clsName}");
					tw.OpenBlock();
					WriteLocals(tw, true);
	
				}
				tw.WriteLine($"public static Value {funName}()");
				tw.OpenBlock();
				var lines = (Twcode.InnerWriter as StringWriter)!.ToString().Split('\n');
				foreach (var line in lines)
				{
					tw.WriteLine(line.TrimEnd());
				}
				tw.WriteLine("return Nil;");
				tw.CloseBlock();
				if (type==FunctionType.FunTypeScript)
				{
					tw.CloseBlock();
				}
			}

			public void AddLocalInitName(string str)
			{
				localInitNames.Add(str);
			}
		}
		
		State state, globalState;
		override protected CompilerState current => state.cstate;

		IndentedTextWriter twcur => state.Twcode;

		public CsCodeGen(EleuOptions options, List<Stmt> statements) : base(options, statements)
		{
			state = new State(FunTypeScript,Path.GetFileNameWithoutExtension(options.CsOutputFile)!,  "Main",null);
			globalState = state;
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
			state.WriteTo(idtw);
			return true;
		}

		public bool VisitBlockStmt(Stmt.Block stmt) => throw new NotImplementedException();
		public bool VisitClassStmt(Stmt.Class stmt) => throw new NotImplementedException();
		public bool VisitExpressionStmt(Stmt.Expression stmt)
		{
			var sex= stmt.expression.Accept(this);
			if (stmt.expression is not Expr.Assign)
			  twcur.Write("_ = ");
			twcur.Write(sex);
			twcur.WriteLine(';');
			return true;
		}

		public bool VisitFunctionStmt(Stmt.Function stmt) => throw new NotImplementedException();
		public bool VisitIfStmt(Stmt.If stmt) => throw new NotImplementedException();
		public bool VisitPrintStmt(Stmt.Print stmt)
		{
			var ex = stmt.expression.Accept(this);
			twcur.WriteLine($"Console.WriteLine({ex});");
			return true;
		}

		public bool VisitReturnStmt(Stmt.Return stmt) => throw new NotImplementedException();
		public bool VisitVarStmt(Stmt.Var stmt)
		{
			var name = ParseVariable(stmt.Name);
			string initExpr = stmt.Initializer != null ? stmt.Initializer.Accept(this) : "Nil";
			DefineVariable(name);
			state.AddLocalInitName($"{name} = {initExpr}");
			return true;
		}


		public bool VisitWhileStmt(Stmt.While stmt) => throw new NotImplementedException();
		public string VisitAssignExpr(Expr.Assign expr)
		{
			var rhs= expr.Value.Accept(this);
			var name = expr.Name;
			return $"{name} = {rhs}";
		}

		public string VisitBinaryExpr(Expr.Binary expr)
		{
			var lhs=expr.Left.Accept(this);
			var rhs= expr.Right.Accept(this);
			string op = "";
			switch (expr.Op.type)
			{
				case TOKEN_BANG_EQUAL: op="!="; break;
				case TOKEN_EQUAL_EQUAL: op = "=="; break;
				case TOKEN_GREATER: op = ">"; break;
				case TOKEN_GREATER_EQUAL: op = ">="; break;
				case TOKEN_LESS: op = "<"; break;
				case TOKEN_LESS_EQUAL: op = "<="; break;
				case TokenPlus: op = "+"; break;
				case TokenMinus: op = "-"; break;
				case TokenStar: op = "*"; break;
				case TokenPercent: op = "%"; break;
				case TOKEN_SLASH: op = "/"; break;
				default: throw new CodeGenError("Unknown binary operator: " + op);
			}
			return $"{lhs} {op} {rhs}";
		}

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
				case string s: return $"CreateStringVal(@\"{s}\")";
				default: Error($"Unsupported constant of type: {expr.Value}"); return "";
			}
		}

		public string VisitLogicalExpr(Expr.Logical expr) => throw new NotImplementedException();
		public string VisitSetExpr(Expr.Set expr) => throw new NotImplementedException();
		public string VisitSuperExpr(Expr.Super expr) => throw new NotImplementedException();
		public string VisitThisExpr(Expr.This expr) => throw new NotImplementedException();
		public string VisitUnaryExpr(Expr.Unary expr)
		{
			return $"{expr.Op.StringValue}{expr.Right.Accept(this)}";  
		}
		public string VisitVariableExpr(Expr.Variable expr)
		{
			return expr.Name;
		}
		string ParseVariable(string name)
		{
			DeclareVariable(name);
			if (current.scopeDepth > 0) 
				return name;
			return name;
		}
		void DefineVariable(string name)
		{
			if (current.scopeDepth > 0)
			{
				MarkInitialized();
			}

		}
	}
}
