using Eleu.Types;
using static Eleu.Interpret.InterpretResult;
namespace Eleu.Ast;

internal class AstParser(EleuOptions options, List<Token> tokens)
{
  private static readonly Expr.Literal NilLiteral = new(NilValue);
  private static readonly Expr.Literal TrueLiteral = new(true);
  private static readonly Expr.Literal FalseLiteral = new(false);
  private int current = 0;
  public int ErrorCount { get; private set; }

  public List<Stmt> Parse()
  {
    try
    {
      return DoParse();
    }
    catch (EleuParseError)
    {
      return [];
    }
  }
  List<Stmt> DoParse()
  {
    List<Stmt> statements = [];
    while (!IsAtEnd())
    {
      Declaration(statements);
    }
    if (statements.Count == 0 && Peek.Type == TokenError && ErrorCount == 0)
    {
      throw CreateErrorException(Peek.Status, Peek.StringValue);
    }
    return statements;
  }
  InputStatus CurrentInputStatus => Peek.Status;
  private void Declaration(List<Stmt> statements)
  {
    try
    {
      if (Match(TokenFun)) statements.Add(Function(FunTypeFunction));
      else if (Match(TokenClass)) statements.Add(ClassDeclaration());
      else if (Match(TokenVar)) statements.Add(VarDeclaration());
      else statements.Add(Statement());
    }
    catch (EleuParseError)
    {
      if (options.OnlyFirstError) throw;
      Synchronize();
    }
  }
  private Stmt Statement()
  {
    Stmt stmt;
    var curStat = CurrentInputStatus;

    if (Match(TokenAssert)) stmt = AssertStatement();
    //TODO else if (Match(TokenVar)) stmt = VarDeclaration();
    else if (Match(TokenLeftBrace)) stmt = new Stmt.Block(Block());
    else if (Match(TokenFor)) stmt = ForStatement();
    else if (Match(TokenIf)) stmt = IfStatement();
    else if (Match(TokenReturn)) stmt = ReturnStatement();
    else if (Match(TokenWhile)) stmt = WhileStatement();
    else if (Match(TokenRepeat)) stmt = RepeatStatement();
    else if (Match(TokenContinue, TokenBreak)) stmt = BreakContinueStatement();
    else
    {
      var sex = ExpressionStatement();
      var exp = sex.expression;
      if (!(exp==NilLiteral || exp is Expr.Assign || exp is Expr.Call || exp is Expr.Set))
      {
        throw CreateErrorException(sex.Status, Messages.Invalid_Stmt_Expr);
      }
      stmt = sex;
    }
    stmt.Status = curStat.Union(Previous.Status);
    return stmt;
  }
  private Stmt ForStatement()
  {
    Consume(TokenLeftParen, "Nach 'for' wird '(' erwartet.");
    Stmt? initializer;
    if (Match(TokenSemicolon))
      initializer = null;
    else if (Match(TokenVar))
      initializer = VarDeclaration();
    else
      initializer = ExpressionStatement();
    Expr? condition = null;
    if (!Check(TokenSemicolon))
    {
      condition = Expression();
    }
    Consume(TokenSemicolon, "Expect ';' after loop condition.");
    Expr? increment = null;
    if (!Check(TokenRightParen))
      increment = Expression();
    Consume(TokenRightParen, "Expect ')' after for clauses.");
    CheckIllegalContinuation(Peek, TokenVar, TokenFun, TokenClass);
    if (CheckContinuation(Peek, TokenSemicolon))
      throw CreateErrorException(Peek.Status, "Ein Semikolon direkt hinter einen for ist nicht erlaubt.");
    Stmt body = Statement();
    condition ??= new Expr.Literal(true);
    body = new Stmt.While(condition, body, increment);
    if (initializer != null)
      body = new Stmt.Block([initializer, body]);
    return body;
  }

  bool CheckContinuation(Token tok, params TokenType[] types)
  {
    foreach (var tt in types)
    {
      if (tt == tok.Type) return true;
    }
    return false;
  }

  void CheckIllegalContinuation(Token tok, params TokenType[] illegalTokenTypes)
  {
    if (CheckContinuation(tok, illegalTokenTypes))
      throw CreateErrorException(tok.Status, $"'{tok.StringValue}' ist hier nicht erlaubt.");
  }
  private Stmt.If IfStatement()
  {
    Consume(TokenLeftParen, "Nach 'if' wird '(' erwartet.");
    Expr condition = Expression();
    Consume(TokenRightParen, "Nach der 'if'-Bedingung wird ')' erwartet.", condition.Status);
    CheckIllegalContinuation(Peek, TokenVar, TokenFun, TokenClass);
    if (CheckContinuation(Peek, TokenSemicolon))
      throw CreateErrorException(Peek.Status, "Ein Semikolon direkt hinter einen if ist nicht erlaubt.");
    Stmt thenBranch = Statement();
    Stmt? elseBranch = null;
    if (Match(TokenElse))
    {
      CheckIllegalContinuation(Peek, TokenVar, TokenFun, TokenClass, TokenLeftParen);
      elseBranch = Statement();
    }
    return new Stmt.If(condition, thenBranch, elseBranch);
  }
  private Stmt.Assert AssertStatement()
  {
    string? msg = null;
    bool isErrorAssert = Check(TokenBreak);
    if (isErrorAssert) Advance();
    Expr value = Expression();
    if (Match(TokenString))
      msg = Previous.StringStringValue;
    Consume(TokenSemicolon, "Nach der Bedingung wird eine Zeichenkette oder ein ';' erwartet.");
    return new Stmt.Assert(value, msg, isErrorAssert);
  }

  private Stmt.Return ReturnStatement()
  {
    Token keyword = Previous;
    Expr? value = null;
    if (!Check(TokenSemicolon))
    {
      value = Expression();
    }
    Consume(TokenSemicolon, "Nach dem Rückgabewert wird ein ';' erwartet.");
    return new Stmt.Return(keyword, value);
  }
  private Stmt.BreakContinue BreakContinueStatement()
  {
    Token keyword = Previous;
    Consume(TokenSemicolon, $"Nach '{keyword.StringValue}' wird ein ';' erwartet.");
    bool isBreak = keyword.Type == TokenBreak;
    return new Stmt.BreakContinue(isBreak);
  }

  private Stmt.Expression ExpressionStatement()
  {
    if (Match(TokenSemicolon))
      return new Stmt.Expression(NilLiteral) { Status = Previous.Status };
    Expr expr = Expression();
    Consume(TokenSemicolon, "Ein ';' wird hier erwartet.");
    return new Stmt.Expression(expr) { Status = expr.Status };
  }
  private Stmt.Function Function(FunctionType kind)
  {

    Token name = Consume(TokenIdentifier, "Expect " + kind + " name.");
    Consume(TokenLeftParen, "Expect '(' after " + kind + " name.");
    List<Token> parameters = new();
    if (!Check(TokenRightParen))
    {
      do
      {
        if (parameters.Count >= 255)
        {
          throw CreateErrorException(Peek.Status, "Can't have more than 255 parameters.");
        }
        parameters.Add(Consume(TokenIdentifier, "Expect parameter name."));
      } while (Match(TokenComma));
    }
    Consume(TokenRightParen, "Nach den Parametern wird ')' erwartet.");
    string tmsg = kind == FunctionType.FunTypeFunction ? "function" : "method";
    Consume(TokenLeftBrace, "Expect '{' before " + tmsg + " body.");
    List<Stmt> body = Block();
    return new Stmt.Function(kind, name.StringValue, parameters, body) { Status = name.Status };
  }
  private List<Stmt> Block()
  {
    List<Stmt> statements = new();
    while (!Check(TokenRightBrace) && !IsAtEnd())
    {
      Declaration(statements);
    }
    Consume(TokenRightBrace, "Nach einem Block wird '}' erwartet.");
    return statements;
  }
  private Expr Assignment()
  {
    Expr expr = Or();
    if (Match(TokenEqual))
    {
      Token equals = Previous;
      Expr value = Assignment();
      if (expr is Expr.Variable variable)
      {
        var name = variable.Name;
        return new Expr.Assign(name, value);
      }
      else if (expr is Expr.Get get)
      {
        return new Expr.Set(get.Obj, get.Name, value);
      }
      throw CreateErrorException(expr.Status.IsEmpty ? equals.Status : expr.Status, "Diesem Ausdruck kann kein Wert zugewiesen werden.");
    }
    return expr;
  }
  private Expr Or()
  {
    Expr expr = And();
    while (Match(TokenOr))
    {
      Token _operator = Previous;
      Expr right = And();
      expr = new Expr.Logical(expr, _operator, right);
    }
    return expr;
  }
  private Expr And()
  {
    Expr expr = Equality();
    while (Match(TokenAnd))
    {
      Token _operator = Previous;
      Expr right = Equality();
      expr = new Expr.Logical(expr, _operator, right);
    }
    return expr;
  }
  private Expr Expression()
  {
    var curStat = CurrentInputStatus;
    var expr = Assignment();
    expr.Status = curStat.Union(CurrentInputStatus);
    return expr;
  }

  private Stmt.Class ClassDeclaration()
  {
    var curStat = Previous.Status;
    Token name = Consume(TokenIdentifier, "Expect class name.");
    Expr.Variable? superclass = null;
    if (Match(TokenLess))
    {
      Consume(TokenIdentifier, "Expect superclass name.");
      superclass = new Expr.Variable(Previous.StringValue);
    }
    curStat = Previous.Status.Union(curStat);
    Consume(TokenLeftBrace, "Expect '{' before class body.");
    List<Stmt.Function> methods = new();
    while (!Check(TokenRightBrace) && !IsAtEnd())
    {
      methods.Add(Function(FunTypeMethod));
    }
    Consume(TokenRightBrace, "Expect '}' after class body.");
    return new Stmt.Class(name.StringValue, superclass, methods) { Status = curStat };
  }
  private Stmt.Var VarDeclaration()
  {
    var cs = CurrentInputStatus;
    if (Peek.Type >= TokenKeywordStart && Peek.Type <= TokenKeywordEnd)
      throw CreateErrorException(Peek.Status, $"Das Schlüsselwort '{Peek.StringValue}' ist kein gültiger Variablenname.");
    Token name = Consume(TokenIdentifier, "Der Name einer Variablen wird erwartet.");
    Expr? initializer = null;
    if (Match(TokenEqual))
    {
      initializer = Expression();
    }
    cs = cs.Union(CurrentInputStatus);
    Consume(TokenSemicolon, "Nach einer Variablendeklaration wird ';' erwartet.");
    return new Stmt.Var(name.StringValue, initializer) { Status = cs };
  }
  private Stmt.While WhileStatement()
  {
    Consume(TokenLeftParen, "Nach 'while' wird '(' erwartet.");
    Expr condition = Expression();
    Consume(TokenRightParen, "Nach der 'while'-Bedingung wird ')' erwartet.", condition.Status);
    if (CheckContinuation(Peek, TokenSemicolon))
      throw CreateErrorException(Peek.Status, "Ein Semikolon direkt hinter einen while ist nicht erlaubt.");
    CheckIllegalContinuation(Peek, TokenVar, TokenFun, TokenClass);
    Stmt body = Statement();
    return new Stmt.While(condition, body, null);
  }
  private Stmt.Repeat RepeatStatement()
  {
    Consume(TokenLeftParen, "Nach 'repeat' wird '(' erwartet.");
    Expr numExpr = Expression();
    Consume(TokenRightParen, "Nach der Anzahl wird ')' erwartet.", numExpr.Status);
    if (CheckContinuation(Peek, TokenSemicolon))
      throw CreateErrorException(Peek.Status, "Ein Semikolon direkt hinter einen repeat ist nicht erlaubt.");
    Stmt body = Statement();
    return new Stmt.Repeat(numExpr, body);
  }
  private Expr Equality()
  {
    Expr expr = Comparison();
    while (Match(TokenBangEqual, TokenEqualEqual))
    {
      Token _operator = Previous;
      Expr right = Comparison();
      expr = new Expr.Binary(expr, _operator, right);
    }
    return expr;
  }
  private Expr Comparison()
  {
    Expr expr = Term();

    while (Match(TokenGreater, TokenGreaterEqual, TokenLess, TokenLessEqual))
    {
      Token _operator = Previous;
      Expr right = Term();
      expr = new Expr.Binary(expr, _operator, right);
    }
    return expr;
  }
  private Expr Term()
  {
    Expr expr = Factor();
    while (Match(TokenMinus, TokenPlus))
    {
      Token _operator = Previous;
      Expr right = Factor();
      expr = new Expr.Binary(expr, _operator, right);
    }
    return expr;
  }
  private Expr Factor()
  {
    Expr expr = Unary();

    while (Match(TokenSlash, TokenStar, TokenPercent))
    {
      Token _operator = Previous;
      Expr right = Unary();
      expr = new Expr.Binary(expr, _operator, right);
    }
    return expr;
  }
  private Expr Unary()
  {
    if (Match(TokenBang, TokenMinus))
    {
      Token _operator = Previous;
      Expr right = Unary();
      return new Expr.Unary(_operator, right);
    }
    return Call();
  }
  private Expr Call()
  {
    Expr expr = Primary();
    while (true)
    {
      if (Match(TokenLeftParen))
      {
        expr = FinishCall(expr, null);
      }
      else if (Match(TokenDot))
      {
        Token name = Consume(TokenIdentifier, "Expect property name after '.'.");
        expr = new Expr.Get(expr, name.StringValue);
      }
      else
      {
        break;
      }
    }
    return expr;
  }
  private Expr.Call FinishCall(Expr callee, string? mthName)
  {
    List<Expr> arguments = new();
    if (!Check(TokenRightParen))
    {
      do
      {
        if (arguments.Count >= 255)
        {
          throw CreateErrorException(Peek.Status, "Can't have more than 255 arguments.");
        }
        arguments.Add(Expression());
      } while (Match(TokenComma));
    }
    Consume(TokenRightParen, "Nach den Argumenten wird ')' erwartet.");
    return new Expr.Call(callee, mthName, callee is Expr.Super, arguments);
  }
  private Expr Primary()
  {
    if (Match(TokenSuper))
    {
      Token keyword = Previous;
      Consume(TokenDot, "Expect '.' after 'super'.");
      Token method = Consume(TokenIdentifier, "Expect superclass method name.");
      return new Expr.Super(keyword.StringValue, method.StringValue);
    }
    if (Match(TokenThis)) return new Expr.This(Previous.StringValue);
    if (Match(TokenIdentifier))
    {
      return new Expr.Variable(Previous.StringValue) { Status = Previous.Status };
    }
    if (Match(TokenLeftParen))
    {
      Expr expr = Expression();
      Consume(TokenRightParen, "Expect ')' after expression.");
      return new Expr.Grouping(expr);
    }
    var lit = Literal();
    if (lit == null)
    {
      if (Peek.Type == TokenElse)
        throw CreateErrorException(Previous.Status.Union(Peek.Status), Else_Not_After_If);
      throw CreateErrorException(Previous.Status, Expression_Expected);
    }
    return lit;
  }

  private Expr.Literal? Literal()
  {
    if (Match(TokenFalse)) return FalseLiteral;
    if (Match(TokenTrue)) return TrueLiteral;
    if (Match(TokenNil)) return NilLiteral;
    if (Match(TokenNumber))
    {
      return new Expr.Literal(Number.TryParse(Previous.StringValue));
    }
    if (Match(TokenString))
    {
      return new Expr.Literal(Previous.StringStringValue);
    }
    if (Match(TokenLeftBracket))
    {
      return ListLiteral();
    }
    return null;
  }
  private Expr.Literal ListLiteral()
  {
    EleuList arguments = new();
    if (!Check(TokenRightBracket))
    {
      do
      {
        var l = Expression() ?? throw CreateErrorException(Previous.Status, "Hier wird ein Ausdruck erwartet.");
        arguments.Add(l);
      } while (Match(TokenComma));
    }
    Consume(TokenRightBracket, "Am Ende einer Liste wird ']' erwartet.");
    return new(arguments);
  }
  private bool Match(params TokenType[] types)
  {
    foreach (TokenType type in types)
    {
      if (Check(type))
      {
        Advance();
        return true;
      }
    }
    return false;
  }
  private Token Consume(TokenType type, string message, InputStatus? status = null)
  {
    if (Check(type)) return Advance();
    var stat = status ?? Previous.Status;
    throw CreateErrorException(stat, message);
  }
  private EleuParseError CreateErrorException(InputStatus status, string message)
  {
    ErrorCount++;
    options.WriteCompilerError(status, message);
    return new EleuParseError();
  }
  private void Synchronize()
  {
    Advance();
    while (!IsAtEnd())
    {
      if (Previous.Type == TokenSemicolon) return;
      switch (Peek.Type)
      {
        case TokenClass:
        case TokenFun:
        case TokenVar:
        case TokenFor:
        case TokenIf:
        case TokenWhile:
        case TokenReturn:
        case TokenError:
          return;
      }
      Advance();
    }
  }
  private bool Check(TokenType type)
  {
    if (IsAtEnd()) return false;
    return Peek.Type == type;
  }
  private Token Advance()
  {
    if (!IsAtEnd())
    {
      current++;
      var tok = Peek;
      if (tok.Type == TokenError)
      {
        throw CreateErrorException(tok.Status, tok.StringValue);
      }
    }
    return Previous;
  }
  private bool IsAtEnd()
  {
    var t = Peek.Type;
    return t == TokenEof || t == TokenError;
  }
  private Token Peek => tokens[current];
  private Token Previous => current > 0 ? tokens[current - 1] : tokens[0];
}
