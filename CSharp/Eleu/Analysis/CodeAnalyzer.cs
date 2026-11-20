namespace Eleu.Analysis;

/// <summary>A place in the source: lines and columns from 1, the end column after the last character.</summary>
public readonly record struct Span(int Line, int Column, int EndLine, int EndColumn)
{
  public bool Contains(int line, int column) =>
    (line > Line || (line == Line && column >= Column)) && (line < EndLine || (line == EndLine && column <= EndColumn));
}

public enum SymbolKind { Variable, Parameter, Function, Class, Method, Field }

/// <summary>
/// A name declared in a program: a variable, parameter, function, class – or a member (method or field) of the
/// classes, one symbol per member name (Eleu does not know the class of an object before it runs).
/// </summary>
public sealed class Symbol
{
  public int Id { get; init; }
  public string Name { get; init; } = "";
  public SymbolKind Kind { get; init; }
  /// <summary>Declared at the top level of the program (or a member).</summary>
  public bool IsGlobal { get; init; }
  /// <summary>The class of a method, the function or class a symbol is declared in (for the outline).</summary>
  public Symbol? Container { get; init; }
  /// <summary>Where the name is declared (several: a global declared twice, a member in several classes).</summary>
  public List<Span> Declarations { get; } = [];
  /// <summary>Where the name is used.</summary>
  public List<Span> References { get; } = [];
  /// <summary>The whole declaration (from "fun"/"class"/"var" to its end), for the outline.</summary>
  public Span? Full { get; internal set; }

  public IEnumerable<Span> Occurrences => Declarations.Concat(References);
}

/// <summary>
/// Finds the declarations of a program and where they are used, with the rules of the resolver: blocks, functions
/// and "for" open a scope, a local name is known from its declaration on, global names are bound late (a function may
/// call one declared further down). Works on the tokens, so the places are exact and code with errors works, too.
/// </summary>
public sealed class CodeAnalyzer
{
  sealed class Scope(Scope? parent, bool isGlobal, Symbol? owner)
  {
    public readonly Scope? Parent = parent;
    public readonly bool IsGlobal = isGlobal;
    /// <summary>The function or class whose scope this is (for containers).</summary>
    public readonly Symbol? Owner = owner;
    public readonly Dictionary<string, Symbol> Names = [];
    public Symbol? Class;
  }

  /// <summary>What a "{" opened, so that its "}" can close it.</summary>
  sealed record Brace(Scope Scope, Symbol? Completes, bool ClassBody, int ForScopesToClose);

  readonly string source;
  readonly List<Token> tokens;
  readonly int[] lineStarts;
  readonly List<Symbol> symbols = [];
  readonly Dictionary<string, Symbol> members = [];
  readonly List<(string Name, Span Span)> unresolved = [];
  readonly Scope global = new(null, true, null);
  Scope scope;
  readonly Stack<Brace> braces = new();
  // a function or class waiting for its "{"
  Scope? pendingScope;
  Symbol? pendingSymbol;
  bool pendingClassBody;
  // "for (…)": its scope ends with its statement
  int pendingForScopes;
  readonly List<ForStatement> forStatements = [];

  /// <summary>A "for" with its scope; HeaderEnd: the index of the ")" that closes its header.</summary>
  sealed class ForStatement(Scope scope, int parenDepth, int braceDepth)
  {
    public readonly Scope Scope = scope;
    public readonly int ParenDepth = parenDepth, BraceDepth = braceDepth;
    public int HeaderEnd = -1;
  }
  int parenDepth;
  // a "var" statement waiting for its ";" (for the outline)
  readonly List<(Symbol Symbol, int ParenDepth, int BraceDepth)> openVars = [];

  CodeAnalyzer(string source)
  {
    this.source = source;
    var scanner = new Scanning.Scanner(source);
    tokens = scanner.ScanAllTokens(true).Where(t => t.Type is not (TokenEof or TokenError)).ToList();
    var starts = new List<int> { 0 };
    for (int i = 0; i < source.Length; i++)
      if (source[i] == '\n') starts.Add(i + 1);
    lineStarts = [.. starts];
    scope = global;
  }

  /// <summary>The symbols of a program.</summary>
  public static List<Symbol> Analyze(string source)
  {
    var analyzer = new CodeAnalyzer(source);
    analyzer.Run();
    return analyzer.symbols;
  }

  (int Line, int Column) Position(int offset)
  {
    int line = Array.BinarySearch(lineStarts, offset);
    if (line < 0) line = ~line - 1;
    return (line + 1, offset - lineStarts[line] + 1);
  }

  Span SpanOf(Token t)
  {
    var (l, c) = Position(t.Start);
    var (el, ec) = Position(t.End);
    return new Span(l, c, el, ec);
  }

  Span SpanOf(Token from, Token to)
  {
    var (l, c) = Position(from.Start);
    var (el, ec) = Position(to.End);
    return new Span(l, c, el, ec);
  }

  TokenType TypeAt(int i) => i >= 0 && i < tokens.Count ? tokens[i].Type : TokenEof;

  Symbol NewSymbol(string name, SymbolKind kind, bool isGlobal, Symbol? container)
  {
    var symbol = new Symbol { Id = symbols.Count + 1, Name = name, Kind = kind, IsGlobal = isGlobal, Container = container };
    symbols.Add(symbol);
    return symbol;
  }

  /// <summary>Declares a name in the current scope (a global declared again is the same symbol).</summary>
  Symbol Declare(Token name, SymbolKind kind)
  {
    var text = name.StringValue;
    if (scope.IsGlobal && scope.Names.TryGetValue(text, out var existing))
    {
      existing.Declarations.Add(SpanOf(name));
      return existing;
    }
    var symbol = NewSymbol(text, kind, scope.IsGlobal, scope.Owner);
    symbol.Declarations.Add(SpanOf(name));
    scope.Names[text] = symbol;
    return symbol;
  }

  Symbol Member(string name, SymbolKind kind)
  {
    if (!members.TryGetValue(name, out var symbol))
    {
      symbol = NewSymbol(name, kind, true, null);
      members[name] = symbol;
    }
    return symbol;
  }

  void Reference(Token name)
  {
    for (var s = scope; s != null && !s.IsGlobal; s = s.Parent)
    {
      if (s.Names.TryGetValue(name.StringValue, out var local))
      {
        local.References.Add(SpanOf(name));
        return;
      }
    }
    // global: bound late, also to a declaration further down
    unresolved.Add((name.StringValue, SpanOf(name)));
  }

  Symbol? CurrentClass()
  {
    for (var s = scope; s != null; s = s.Parent)
      if (s.Class != null) return s.Class;
    return null;
  }

  void Run()
  {
    for (int i = 0; i < tokens.Count; i++)
    {
      var t = tokens[i];
      switch (t.Type)
      {
        case TokenFun when TypeAt(i + 1) == TokenIdentifier:
          {
            var name = tokens[++i];
            var function = Declare(name, SymbolKind.Function);
            function.Full = SpanOf(t, name);
            i = Parameters(i + 1, function);
            break;
          }
        case TokenClass when TypeAt(i + 1) == TokenIdentifier:
          {
            var name = tokens[++i];
            var cls = Declare(name, SymbolKind.Class);
            cls.Full = SpanOf(t, name);
            if (TypeAt(i + 1) == TokenLess && TypeAt(i + 2) == TokenIdentifier)
            {
              Reference(tokens[i + 2]);
              i += 2;
            }
            pendingScope = new Scope(scope, false, cls) { Class = cls };
            pendingSymbol = cls;
            pendingClassBody = true;
            break;
          }
        case TokenVar when TypeAt(i + 1) == TokenIdentifier:
          {
            var name = tokens[++i];
            var variable = Declare(name, SymbolKind.Variable);
            if (variable.Full == null)
            {
              variable.Full = SpanOf(t, name);
              openVars.Add((variable, parenDepth, braces.Count));
            }
            break;
          }
        case TokenFor:
          // the scope of "for (var i …)": opened with its "(", closed with the end of its statement
          pendingForScopes++;
          break;
        case TokenLeftParen:
          if (pendingForScopes > 0 && TypeAt(i - 1) == TokenFor)
          {
            pendingForScopes--;
            scope = new Scope(scope, false, scope.Owner);
            forStatements.Add(new ForStatement(scope, parenDepth, braces.Count));
          }
          parenDepth++;
          break;
        case TokenRightParen:
          parenDepth = Math.Max(0, parenDepth - 1);
          if (forStatements.Count > 0 && forStatements[^1].HeaderEnd < 0 && forStatements[^1].ParenDepth == parenDepth)
            forStatements[^1].HeaderEnd = i;
          break;
        case TokenLeftBrace:
          {
            int forScopes = 0;
            // the block of a "for": its "}" closes the for scope, too
            if (forStatements.Count > 0 && forStatements[^1].Scope == scope && forStatements[^1].HeaderEnd == i - 1)
            {
              forScopes = 1;
              forStatements.RemoveAt(forStatements.Count - 1);
            }
            var opened = pendingScope ?? new Scope(scope, false, scope.Owner);
            braces.Push(new Brace(opened, pendingSymbol, pendingClassBody, forScopes));
            scope = opened;
            pendingScope = null;
            pendingSymbol = null;
            pendingClassBody = false;
            break;
          }
        case TokenRightBrace:
          if (braces.Count > 0)
          {
            var brace = braces.Pop();
            if (brace.Completes is { Full: { } full })
              brace.Completes.Full = full with { EndLine = SpanOf(t).EndLine, EndColumn = SpanOf(t).EndColumn };
            scope = brace.Scope.Parent ?? global;
            for (int k = 0; k < brace.ForScopesToClose; k++) scope = scope.Parent ?? global;
            EndStatement(t);
          }
          break;
        case TokenSemicolon:
          EndVars(t);
          if (parenDepth == 0) EndStatement(t);
          break;
        case TokenIdentifier:
          Identifier(ref i);
          break;
      }
    }
    // the globals: bound to their declaration anywhere in the program; others are built-in or unknown
    foreach (var (name, span) in unresolved)
      if (global.Names.TryGetValue(name, out var symbol))
        symbol.References.Add(span);
  }

  /// <summary>A statement without braces after "for (…)" ends: its scope, too.</summary>
  void EndStatement(Token t)
  {
    while (forStatements.Count > 0 && forStatements[^1].Scope == scope && forStatements[^1].HeaderEnd >= 0
      && forStatements[^1].BraceDepth == braces.Count && forStatements[^1].ParenDepth == parenDepth)
    {
      forStatements.RemoveAt(forStatements.Count - 1);
      scope = scope.Parent ?? global;
    }
  }

  void EndVars(Token semicolon)
  {
    for (int k = openVars.Count - 1; k >= 0; k--)
    {
      var (symbol, depth, braceDepth) = openVars[k];
      if (depth == parenDepth && braceDepth == braces.Count)
      {
        var end = SpanOf(semicolon);
        symbol.Full = symbol.Full!.Value with { EndLine = end.EndLine, EndColumn = end.EndColumn };
        openVars.RemoveAt(k);
      }
    }
  }

  /// <summary>"(a, b)" after the name of a function or method: the parameters, in the scope of its body.</summary>
  int Parameters(int i, Symbol function)
  {
    var body = new Scope(scope, false, function);
    if (TypeAt(i) == TokenLeftParen)
    {
      int depth = 0;
      for (; i < tokens.Count; i++)
      {
        var t = tokens[i];
        // an unclosed parameter list: the body starts anyway
        if (t.Type is TokenLeftBrace or TokenSemicolon)
        {
          i--;
          break;
        }
        if (t.Type == TokenLeftParen) depth++;
        else if (t.Type == TokenRightParen && --depth == 0) break;
        else if (t.Type == TokenIdentifier && depth == 1)
        {
          var parameter = NewSymbol(t.StringValue, SymbolKind.Parameter, false, function);
          parameter.Declarations.Add(SpanOf(t));
          body.Names[t.StringValue] = parameter;
        }
      }
    }
    else i--;
    pendingScope = body;
    pendingSymbol = function;
    pendingClassBody = false;
    return i;
  }

  void Identifier(ref int i)
  {
    var t = tokens[i];
    // a member: obj.name, this.name = …, super.name
    if (TypeAt(i - 1) == TokenDot)
    {
      var cls = CurrentClass();
      bool fieldDeclaration = TypeAt(i - 2) == TokenThis && TypeAt(i + 1) == TokenEqual && cls != null;
      var member = Member(t.StringValue, fieldDeclaration ? SymbolKind.Field : SymbolKind.Method);
      if (fieldDeclaration && !member.Declarations.Any(d => declaredIn.TryGetValue(d, out var c) && c == cls))
      {
        member.Declarations.Add(SpanOf(t));
        declaredIn[SpanOf(t)] = cls!;
      }
      else member.References.Add(SpanOf(t));
      return;
    }
    // a method: "name(…) {" in the body of a class
    if (braces.Count > 0 && braces.Peek().ClassBody && scope == braces.Peek().Scope && TypeAt(i + 1) == TokenLeftParen)
    {
      var cls = scope.Class!;
      var method = Member(t.StringValue, SymbolKind.Method);
      method.Declarations.Add(SpanOf(t));
      declaredIn[SpanOf(t)] = cls;
      var function = NewSymbol(t.StringValue, SymbolKind.Method, false, cls);
      function.Full = SpanOf(t);
      methodsOf.Add((cls, function, method));
      i = Parameters(i + 1, function);
      return;
    }
    Reference(t);
  }

  // where a member was declared (one declaration per class), the methods of the classes (for the outline)
  readonly Dictionary<Span, Symbol> declaredIn = [];
  readonly List<(Symbol Class, Symbol Function, Symbol Member)> methodsOf = [];

  /// <summary>The outline: functions and classes (with their methods), global variables.</summary>
  public static List<OutlineItem> Outline(string source)
  {
    var analyzer = new CodeAnalyzer(source);
    analyzer.Run();
    return analyzer.BuildOutline();
  }

  List<OutlineItem> BuildOutline()
  {
    var items = new List<OutlineItem>();
    var byOwner = new Dictionary<Symbol, OutlineItem>();
    foreach (var s in symbols.Where(s => s.Full != null && s.Kind is SymbolKind.Function or SymbolKind.Class or SymbolKind.Variable))
    {
      // local variables are no part of the outline
      if (s.Kind == SymbolKind.Variable && !s.IsGlobal) continue;
      var item = new OutlineItem(s.Name, s.Kind, s.Full!.Value, s.Declarations[0], []);
      byOwner[s] = item;
      if (s.Container != null && byOwner.TryGetValue(s.Container, out var parent)) parent.Children.Add(item);
      else if (s.Container == null || s.IsGlobal) items.Add(item);
      else continue;
    }
    foreach (var (cls, function, _) in methodsOf)
    {
      if (byOwner.TryGetValue(cls, out var parent))
        parent.Children.Add(new OutlineItem(function.Name, SymbolKind.Method, function.Full!.Value, function.Full!.Value with
        {
          EndLine = function.Full!.Value.Line,
          EndColumn = function.Full!.Value.Column + function.Name.Length
        }, []));
    }
    return items;
  }
}

/// <summary>An entry of the outline: its whole range and the range of its name.</summary>
public sealed record OutlineItem(string Name, SymbolKind Kind, Span Full, Span Selection, List<OutlineItem> Children);
