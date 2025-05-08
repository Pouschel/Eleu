using Eleu.Ast;

namespace Eleu;

internal class Messages
{
  public static string Func_Call_Missing_Paren(string funcName) => $"Beim Aufruf von '{funcName}' fehlen die Klammern ().";
  public static string Wrong_Op_Arg(Token op, object arg) => $"Der Operator '{op.StringValue}' kann nicht auf '{arg}' angewendet werden.";

  public static string Else_Not_After_If => "Ein else folgt immer direkt der Anweisung bzw. {dem Block} nach dem if.";
  public static string Expression_Expected => "Hier wird ein Ausdruck erwartet.";
  public static string Invalid_Stmt_Expr = "Nur Variablenzuweiungen 'name=...' oder Funktionsaufrufe 'name(...)' dürfen als Anweisungen verwendet werden.";
}
