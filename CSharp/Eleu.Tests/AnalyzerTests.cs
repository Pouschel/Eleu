using Eleu.Analysis;

namespace Eleu.Tests;

/// <summary>The analysis for the editor: declarations, references, scopes, outline.</summary>
public class AnalyzerTests
{
  /// <summary>The place (line, column from 1) of the n-th occurrence of a text.</summary>
  static (int Line, int Column) At(string code, string text, int occurrence = 1)
  {
    int index = -1;
    for (int k = 0; k < occurrence; k++)
    {
      index = code.IndexOf(text, index + 1, StringComparison.Ordinal);
      Assert.True(index >= 0, $"'{text}' #{occurrence} not in the code");
    }
    var before = code[..index];
    var line = before.Count(c => c == '\n') + 1;
    return (line, index - (before.LastIndexOf('\n') + 1) + 1);
  }

  static Symbol? SymbolAt(List<Symbol> symbols, (int Line, int Column) at) =>
    symbols.FirstOrDefault(s => s.Occurrences.Any(o => o.Contains(at.Line, at.Column)));

  static Symbol Symbol(string code, string text, int occurrence = 1) =>
    SymbolAt(CodeAnalyzer.Analyze(code), At(code, text, occurrence)) ?? throw new Xunit.Sdk.XunitException($"no symbol at '{text}' #{occurrence}");

  [Fact]
  public void Global_function_is_bound_late()
  {
    const string code = "f(1);\nfun f(n) { if (n > 0) f(n - 1); }\nf(2);";
    var f = Symbol(code, "f(n)");
    Assert.Equal(SymbolKind.Function, f.Kind);
    Assert.Single(f.Declarations);
    Assert.Equal(3, f.References.Count);
    Assert.Equal(new Span(2, 5, 2, 6), f.Declarations[0]);
  }

  [Fact]
  public void Local_variable_hides_the_global_one()
  {
    const string code = "var i = 1;\nfun f() { var i = 2; print(i); }\nprint(i);";
    var global = Symbol(code, "i", 1);
    var local = Symbol(code, "i", 2);
    Assert.NotSame(global, local);
    Assert.Equal([new Span(3, 7, 3, 8)], global.References);
    Assert.Equal([new Span(2, 28, 2, 29)], local.References);
  }

  [Fact]
  public void Parameters_belong_to_their_function()
  {
    const string code = "fun add(a, b) { return a + b; }\nvar a = 5;\nprint(a);";
    var parameter = Symbol(code, "a, b");
    Assert.Equal(SymbolKind.Parameter, parameter.Kind);
    Assert.Single(parameter.References);
    var global = Symbol(code, "a = 5");
    Assert.Single(global.References);
  }

  [Fact]
  public void For_variable_ends_with_the_loop()
  {
    const string code = "for (var i = 0; i < 3; i = i + 1) print(i);\nfor (var i = 0; i < 2; i = i + 1)\n{\n  print(i);\n}\nprint(i);";
    var first = Symbol(code, "i", 1);
    var second = Symbol(code, "i", 6);
    Assert.NotSame(first, second);
    Assert.Equal(4, first.References.Count);
    Assert.Equal(4, second.References.Count);
    // the last print(i) is neither of them
    Assert.Null(SymbolAt(CodeAnalyzer.Analyze(code), (6, 7)));
  }

  [Fact]
  public void Block_variable_does_not_leak()
  {
    const string code = "{ var t = 1; print(t); }\nprint(t);";
    var t = Symbol(code, "t", 1);
    Assert.Single(t.References);
  }

  [Fact]
  public void Class_members_methods_and_fields()
  {
    const string code = """
      class Counter
      {
        init(start)
        {
          this.count = start;
        }
        step()
        {
          this.count = this.count + 1;
          return this.count;
        }
      }
      var c = Counter(3);
      c.step();
      print(c.count);
      """;
    var cls = Symbol(code, "Counter", 1);
    Assert.Equal(SymbolKind.Class, cls.Kind);
    Assert.Single(cls.References);
    var count = Symbol(code, "count", 1);
    Assert.Equal(SymbolKind.Field, count.Kind);
    Assert.Single(count.Declarations);
    Assert.Equal(4, count.References.Count);
    var step = Symbol(code, "step", 1);
    Assert.Equal(SymbolKind.Method, step.Kind);
    Assert.Single(step.References);
    var start = Symbol(code, "start", 1);
    Assert.Equal(SymbolKind.Parameter, start.Kind);
    Assert.Single(start.References);
  }

  [Fact]
  public void Builtin_functions_have_no_symbol()
  {
    const string code = "print(len(\"abc\"));";
    var symbols = CodeAnalyzer.Analyze(code);
    Assert.Null(SymbolAt(symbols, At(code, "print")));
    Assert.Null(SymbolAt(symbols, At(code, "len")));
  }

  [Fact]
  public void Code_with_a_syntax_error_is_analyzed()
  {
    const string code = "var x = ;\nprint(x);\nfun f( { x = 2; }";
    var x = Symbol(code, "x", 1);
    Assert.Equal(2, x.References.Count);
  }

  [Fact]
  public void Global_declared_twice_is_one_symbol()
  {
    const string code = "var n = 1;\nvar n = 2;\nprint(n);";
    var n = Symbol(code, "n", 1);
    Assert.Equal(2, n.Declarations.Count);
    Assert.Single(n.References);
  }

  [Fact]
  public void Outline_has_functions_classes_with_methods_and_global_variables()
  {
    const string code = """
      var max = 0;
      fun outer(a)
      {
        var local = 1;
        fun inner() { return local; }
        return inner();
      }
      class A < B
      {
        go() { }
      }
      """;
    var outline = CodeAnalyzer.Outline(code);
    Assert.Equal(["max", "outer", "A"], outline.Select(o => o.Name));
    Assert.Equal(new Span(1, 1, 1, 13), outline[0].Full);
    Assert.Equal(new Span(2, 1, 7, 2), outline[1].Full);
    Assert.Equal(new Span(2, 5, 2, 10), outline[1].Selection);
    Assert.Equal(["inner"], outline[1].Children.Select(c => c.Name));
    Assert.Equal(["go"], outline[2].Children.Select(c => c.Name));
    Assert.Equal(SymbolKind.Method, outline[2].Children[0].Kind);
  }

  public static TheoryData<string> All() => TestFiles.Data(TestFiles.All);

  [Theory]
  [MemberData(nameof(All))]
  public void Every_place_found_in_a_test_script_holds_the_name(string file)
  {
    var source = TestFiles.Source(file);
    if (source == null) return;
    var lines = source.Split('\n');
    foreach (var symbol in CodeAnalyzer.Analyze(source))
    {
      foreach (var span in symbol.Occurrences)
      {
        Assert.Equal(span.Line, span.EndLine);
        var text = lines[span.Line - 1][(span.Column - 1)..(span.EndColumn - 1)];
        Assert.Equal(symbol.Name, text);
      }
    }
    CodeAnalyzer.Outline(source);
  }
}
