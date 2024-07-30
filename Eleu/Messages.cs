using Eleu.Ast;

namespace Eleu;

internal class Messages
{
  public static string Func_Call_Missing_Paren(string funcName) => $"Beim Aufruf von '{funcName}' fehlen die Klammern ().";
  public static string Wrong_Op_Arg(Token op, object arg) => $"Der Operator '{op.StringValue}' kann nicht auf '{arg}' angewendet werden.";


}
