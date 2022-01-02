// See https://aka.ms/new-console-template for more information



String outputDir = args[0];
//> call-define-ast
defineAst(outputDir, "Expr", new string[] {
	//> Statements and State assign-expr
	"Assign   : Token name, Expr value",
	//< Statements and State assign-expr
	"Binary   : Expr left, Token op, Expr right",
	//> Functions call-expr
	"Call     : Expr callee, Token paren, List<Expr> arguments",
	//< Functions call-expr
	//> Classes get-ast
	"Get      : Expr obj, Token name",
	//< Classes get-ast
	"Grouping : Expr expression",
	"Literal  : object? value",
	//> Control Flow logical-ast
	"Logical  : Expr left, Token op, Expr right",
	//< Control Flow logical-ast
	//> Classes set-ast
	"Set      : Expr obj, Token name, Expr value",
	//< Classes set-ast
	//> Inheritance super-expr
	"Super    : Token keyword, Token method",
	//< Inheritance super-expr
	//> Classes this-ast
	"This     : Token keyword",
	//< Classes this-ast
	/* Representing Code call-define-ast < Statements and State var-expr
        "Unary    : Token operator, Expr right"
  */
	//> Statements and State var-expr
	"Unary    : Token op, Expr right",
	"Variable : Token name" }
//< Statements and State var-expr
);
//> Statements and State stmt-ast

defineAst(outputDir, "Stmt", new string[] {
  //> block-ast
  "Block      : List<Stmt> statements",
  //< block-ast
  /* Classes class-ast < Inheritance superclass-ast
        "Class      : Token name, List<Stmt.Function> methods",
  */
  //> Inheritance superclass-ast
  "Class      : Token name, Expr.Variable superclass," +
							" List<Stmt.Function> methods",
  //< Inheritance superclass-ast
  "Expression : Expr expression",
  //> Functions function-ast
  "Function   : Token name, List<Token> paras," +
							" List<Stmt> body",
  //< Functions function-ast
  //> Control Flow if-ast
  "If         : Expr condition, Stmt thenBranch," +
							" Stmt? elseBranch",
  //< Control Flow if-ast
  /* Statements and State stmt-ast < Statements and State var-stmt-ast
        "Print      : Expr expression"
  */
  //> var-stmt-ast
  "Print      : Expr expression",
  //< var-stmt-ast
  //> Functions return-ast
  "Return     : Token keyword, Expr value",
  //< Functions return-ast
  /* Statements and State var-stmt-ast < Control Flow while-ast
        "Var        : Token name, Expr initializer"
  */
  //> Control Flow while-ast
  "Var        : Token name, Expr? initializer",
	"While      : Expr condition, Stmt body" }
//< Control Flow while-ast
);
//< Statements and State stmt-ast
//< call-define-ast



//> define-ast
static void defineAst(String outputDir, String baseName, String[] types)
{
	String path = outputDir + "/" + baseName + ".cs";
	var writer = File.CreateText(path);

	//> omit
	writer.println("//> Appendix II " + baseName.ToLower());
	//< omit
	writer.println("namespace Eleu.Ast;");
	writer.println();
	writer.println("public abstract class " + baseName + " {");

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
	foreach (String type in types)
	{
		String className = type.Split(":")[0].Trim();
		String fields = type.Split(":")[1].Trim(); // [robust]
		defineType(writer, baseName, className, fields);
	}
	//< nested-classes
	//> base-accept-method
	writer.println("}");

	//< omit
	writer.Close();
}
//< define-ast
//> define-visitor
static void defineVisitor(TextWriter writer, String baseName, String[] types)
{
	writer.println("  public interface Visitor<R> {");

	foreach (String type in types)
	{
		String typeName = type.Split(":")[0].Trim();
		writer.println("    R Visit" + typeName + baseName + "(" +
				typeName + " " + baseName.ToLower() + ");");
	}

	writer.println("  }");
}
//< define-visitor
//> define-type

static void defineType(TextWriter writer, String baseName, String className, String fieldList)
{
	//> omit
	writer.println("//> " +
			baseName.ToLower() + "-" + className.ToLower());
	//< omit
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
	String[] fields = fieldList1.Split(", ");
	foreach (String field in fields)
	{
		writer.println("    public readonly " + field + ";");
	}
	writer.println();


	//< omit
	// Constructor.
	writer.println("    internal " + className + "(" + fieldList + ") {");

	//> omit
	foreach (String field in fields)
	{
		String name = field.Split(" ")[1];
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
	//> omit
	writer.println("//< " +
			baseName.ToLower() + "-" + className.ToLower());
	//< omit
}
//< define-type
//> pastry-visitor


static class Extensions
{
	public static void println(this TextWriter tw, string s = "") => tw.WriteLine(s);

}
