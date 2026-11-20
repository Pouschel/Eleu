using System.Text;

namespace Eleu.Scanning;

/// <summary>
/// Formats Eleu code (the web app, the VS Code extension, EleuTester -pp). Only whitespace changes:
/// <list type="bullet">
/// <item>two spaces per level; braces on lines of their own (also before "else")</item>
/// <item>a statement without braces after if, else, while, for, repeat on the next line, indented</item>
/// <item>spaces around binary operators, after commas and keywords ("if (", "repeat (8)"), none inside parentheses,
/// before "(" of a call, around "." and after a unary "-" or "!"</item>
/// <item>comments stay where they are (at the end of a line or on a line of their own)</item>
/// <item>blank lines stay (several become one, none after "{" or before "}"); a function or class gets one before it
/// (before its comment lines)</item>
/// </list>
/// Code the scanner cannot read (e.g. an unterminated string) comes back unchanged.
/// </summary>
public class PrettyPrinter
{
  readonly string text;
  readonly List<Token> tokens;
  public string NewLine = "\r\n";
  public string IndentString = "  ";

  public PrettyPrinter(string text)
  {
    this.text = text;
    Scanner scanner = new(text);
    tokens = scanner.ScanAllTokens(false);
  }

  /// <summary>A statement without braces after if/else/while/for/repeat: indented one level until it ends.</summary>
  sealed record Body(int BraceDepth);

  // ---- the lines being written
  readonly List<(int Indent, string Text)> lines = [];
  readonly StringBuilder line = new();
  int lineIndent;
  bool breakPending;

  // ---- the state of the code
  int braceDepth, parenDepth;
  readonly List<Body> bodies = [];
  // the paren depths of the headers "if (…)", "while (…)", "for (…)", "repeat (…)"
  readonly Stack<int> headers = new();
  bool expectHeader;
  Token? prev;
  bool prevUnary;

  public string Format()
  {
    if (tokens.Count == 0 || tokens[^1].Type == TokenError)
      return text;
    for (int i = 0; i < tokens.Count && tokens[i].Type != TokenEof; i++)
    {
      var token = tokens[i];
      var next = i + 1 < tokens.Count ? tokens[i + 1].Type : TokenEof;
      Handle(token, next);
      prev = token;
    }
    EndLine();
    return Join();
  }

  void Handle(Token t, TokenType next)
  {
    var type = t.Type;
    var unaryBefore = prevUnary;
    // only an operator written right before it makes a token stick to it
    prevUnary = false;
    if (type == TokenComment)
    {
      var comment = t.StringValue.TrimEnd();
      // at the end of a line of code: stays there
      if (prev is { } p && p.Status.LineEnd == t.Status.LineStart && line.Length > 0)
        line.Append(' ').Append(comment);
      else
      {
        StartLine(t);
        line.Append(comment);
      }
      breakPending = true;
      return;
    }

    switch (type)
    {
      case TokenLeftBrace:
        EndLine();
        StartLine(t);
        line.Append('{');
        breakPending = true;
        braceDepth++;
        return;
      case TokenRightBrace:
        EndLine();
        braceDepth = Math.Max(0, braceDepth - 1);
        StartLine(t);
        line.Append('}');
        breakPending = true;
        EndStatements();
        return;
      case TokenElse:
        EndLine();
        StartLine(t);
        line.Append("else");
        if (next is not (TokenLeftBrace or TokenIf)) OpenBody();
        return;
    }

    if (breakPending) StartLine(t);
    else if (line.Length == 0) StartLine(t);
    else if (unaryBefore ? WouldStick(prev!.Value, t) : NeedsSpace(prev!.Value, t)) line.Append(' ');
    line.Append(t.StringValue);
    prevUnary = IsUnary(t);

    switch (type)
    {
      case TokenIf or TokenWhile or TokenFor or TokenRepeat:
        expectHeader = true;
        break;
      case TokenLeftParen:
        if (expectHeader) headers.Push(parenDepth);
        expectHeader = false;
        parenDepth++;
        break;
      case TokenRightParen:
        parenDepth = Math.Max(0, parenDepth - 1);
        if (headers.Count > 0 && headers.Peek() == parenDepth)
        {
          headers.Pop();
          // the statement after the header: a block, an empty statement, or one statement on the next line
          if (next is not (TokenLeftBrace or TokenSemicolon)) OpenBody();
        }
        break;
      case TokenSemicolon:
        // inside "for (…;…;…)" the line goes on
        if (parenDepth == 0)
        {
          breakPending = true;
          EndStatements();
        }
        break;
    }
  }

  void OpenBody()
  {
    bodies.Add(new Body(braceDepth));
    breakPending = true;
  }

  /// <summary>A statement ended (";" or "}" at this brace depth): the bodies it completes end, too.</summary>
  void EndStatements()
  {
    while (bodies.Count > 0 && bodies[^1].BraceDepth == braceDepth)
      bodies.RemoveAt(bodies.Count - 1);
  }

  int Indent => braceDepth + bodies.Count;

  void StartLine(Token t)
  {
    EndLine();
    // blank lines of the source stay (as one)
    if (prev is { } p && t.Status.LineStart - p.Status.LineEnd >= 2)
      lines.Add((0, ""));
    lineIndent = Indent;
    breakPending = false;
  }

  void EndLine()
  {
    if (line.Length > 0)
    {
      lines.Add((lineIndent, line.ToString()));
      line.Clear();
    }
  }

  static bool IsValue(TokenType tt) => tt is TokenIdentifier or TokenNumber or TokenString or TokenRightParen
    or TokenRightBracket or TokenTrue or TokenFalse or TokenNil or TokenThis or TokenSuper;

  bool IsUnary(Token t) => t.Type == TokenBang || (t.Type == TokenMinus && (prev == null || !IsValue(prev.Value.Type)));

  /// <summary>Operators written together become one (and an invalid one for the scanner): "- -3", "! !b".</summary>
  static bool WouldStick(Token p, Token t)
  {
    const string operatorChars = "+-*/=<>!%.";
    var a = p.StringValue;
    var b = t.StringValue;
    return a.Length > 0 && b.Length > 0 && operatorChars.Contains(a[^1]) && operatorChars.Contains(b[0]);
  }

  bool NeedsSpace(Token p, Token t)
  {
    var a = p.Type;
    var b = t.Type;
    if (b is TokenRightParen or TokenRightBracket or TokenComma or TokenSemicolon or TokenDot) return false;
    if (a is TokenLeftParen or TokenLeftBracket or TokenDot) return false;
    // a call or an index: f(x), a[i], f(x)(y)
    if (b is TokenLeftParen or TokenLeftBracket && a is TokenIdentifier or TokenRightParen or TokenRightBracket or TokenThis or TokenSuper or TokenString)
      return false;
    return true;
  }

  string Join()
  {
    // a function or class: a blank line before it (and before its comment lines), not at the start of a block
    var result = new List<(int Indent, string Text)>();
    foreach (var l in lines)
    {
      if (l.Text.StartsWith("fun ") || l.Text.StartsWith("class "))
      {
        int at = result.Count;
        while (at > 0 && result[at - 1].Text.StartsWith("//") && result[at - 1].Indent == l.Indent) at--;
        if (at > 0 && result[at - 1].Text != "" && result[at - 1].Text != "{")
          result.Insert(at, (0, ""));
      }
      result.Add(l);
    }
    var sb = new StringBuilder();
    for (int i = 0; i < result.Count; i++)
    {
      var (indent, text) = result[i];
      if (text == "")
      {
        // one blank line at most; none at the start, after "{" or before "}"
        if (i == 0 || result[i - 1].Text is "" or "{" || i + 1 >= result.Count || result[i + 1].Text is "" or "}")
          continue;
        sb.Append(NewLine);
        continue;
      }
      for (int k = 0; k < indent; k++) sb.Append(IndentString);
      sb.Append(text).Append(NewLine);
    }
    return sb.ToString();
  }
}
