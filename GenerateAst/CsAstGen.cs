﻿class CsAstGen
{
	public static void Run(string outputDir)
	{

		
		//> call-define-ast
		DefineAst(outputDir, "Expr", new string[] {
	//> Statements and State assign-expr
	"Assign   : string Name, Expr Value",
	//< Statements and State assign-expr
	"Binary   : Expr Left, Token Op, Expr Right",
	//> Functions call-expr
	"Call     : Expr Callee, string? Method, bool CallSuper, List<Expr> Arguments",
	//< Functions call-expr
	//> Classes get-ast
	"Get      : Expr Obj, string Name",
	//< Classes get-ast
	"Grouping : Expr Expression",
	"Literal  : object? Value",
	//> Control Flow logical-ast
	"Logical  : Expr Left, Token Op, Expr Right",
	//< Control Flow logical-ast
	//> Classes set-ast
	"Set      : Expr Obj, string Name, Expr Value",
	//< Classes set-ast
	//> Inheritance super-expr
	"Super    : string Keyword, string Method",
	//< Inheritance super-expr
	//> Classes this-ast
	"This     : string Keyword",
	//< Classes this-ast
	/* Representing Code call-define-ast < Statements and State var-expr
        "Unary    : Token operator, Expr right"
  */
	//> Statements and State var-expr
	"Unary    : Token Op, Expr Right",
	"Variable : string Name" }
		//< Statements and State var-expr
		);
		//> Statements and State stmt-ast

		DefineAst(outputDir, "Stmt", new string[] {
	"Block      : List<Stmt> Statements",
	"Class      : string Name, Expr.Variable? Superclass," +
							" List<Stmt.Function> Methods",
	"Expression : Expr expression",
	"Function   : FunctionType Type, string Name, List<Token> Paras," +
							" List<Stmt> Body",
	"If         : Expr Condition, Stmt ThenBranch," +
							" Stmt? ElseBranch",
	"Assert			: Expr expression, string? message, bool isErrorAssert",
	"Return     : Token Keyword, Expr? Value",
	"BreakContinue: bool IsBreak",
	"Var        : string Name, Expr? Initializer",
	"While      : Expr Condition, Stmt Body, Expr? Increment",
	"Repeat     : Expr Count, Stmt Body"
}
		);
	}
	static void DefineAst(string outputDir, string baseName, string[] types)
	{
		string path = outputDir + "/" + baseName + ".cs";
		if (File.Exists(path))
			new FileInfo(path).IsReadOnly = false;
		var writer = File.CreateText(path);

		//> omit
		writer.println("// This file was generated by a tool. Do not edit!");
		writer.println("// AST classes for " + baseName.ToLower());
		//< omit
		writer.println("namespace Eleu.Ast;");
		writer.println();
		writer.println("public abstract class " + baseName + " : ExprStmtBase {");

		//writer.println("  public InputStatus? Status;");
		writer.println("");
		//> call-define-visitor
		defineVisitor(writer, baseName, types);


		// The base accept() method.
		writer.println();
		writer.println("  public abstract R Accept<R>(Visitor<R> visitor);");


		writer.println();
		writer.println("  // Nested " + baseName + " classes here...");
		//< omit
		//> nested-classes
		// The AST classes.
		foreach (string type in types)
		{
			string className = type.Split(":")[0].Trim();
			string fields = type.Split(":")[1].Trim(); // [robust]
			defineType(writer, baseName, className, fields);
		}
		//< nested-classes
		//> base-accept-method
		writer.println("}");

		//< omit
		writer.Close();
		new FileInfo(path).IsReadOnly = true;
	}
	//< define-ast
	//> define-visitor
	static void defineVisitor(TextWriter writer, string baseName, string[] types)
	{
		writer.println("  public interface Visitor<R> {");

		foreach (string type in types)
		{
			string typeName = type.Split(":")[0].Trim();
			writer.println("    R Visit" + typeName + baseName + "(" +
					typeName + " " + baseName.ToLower() + ");");
		}

		writer.println("  }");

	}

	static void defineType(TextWriter writer, string baseName, string className, string fieldList)
	{
		writer.println("  // " + baseName.ToLower() + "-" + className.ToLower());
		writer.println("  public class " + className + " : " + baseName + " {");

		//> omit
		// Hack. Stmt.Class has such a long constructor that it overflows
		// the line length on the Appendix II page. Wrap it.
		if (fieldList.Length > 64)
		{
			fieldList = fieldList.Replace(", ", ",\n          ");
		}
		var fieldList1 = fieldList.Replace(",\n          ", ", ");
		//< omit

		// Store parameters in fields.
		string[] fields = fieldList1.Split(", ");
		foreach (string field in fields)
		{
			writer.println("    public readonly " + field + ";");
		}
		writer.println();


		//< omit
		// Constructor.
		writer.println("    internal " + className + "(" + fieldList + ") {");

		//> omit
		foreach (string field in fields)
		{
			string name = field.Split(" ")[1];
			writer.println("      this." + name + " = " + name + ";");
		}

		writer.println("    }");
		//> accept-method

		// Visitor pattern.
		writer.println();
		writer.println("    public override R Accept<R>(Visitor<R> visitor) {");
		writer.println("      return visitor.Visit" +
				className + baseName + "(this);");
		writer.println("    }");
		//< accept-method
		writer.println("  }");
		//writer.println("//< " +	baseName.ToLower() + "-" + className.ToLower());
	}
}