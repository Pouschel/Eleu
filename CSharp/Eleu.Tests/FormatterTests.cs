using Eleu.Scanning;

namespace Eleu.Tests;

/// <summary>The formatter (PrettyPrinter): the style of the course, and only whitespace may change.</summary>
public class FormatterTests
{
  static string Format(string code) => new PrettyPrinter(code) { NewLine = "\n" }.Format();

  // the style as wished, from a program of the course
  const string Course = """
    //notest
    // Heute hitzefrei?
    var temp = 20;
    var stunde = 1;
    var klasse = 10;
    repeat (8)
    {
      print("Stunde " + stunde);
      if (klasse < 10)
      {
        if (stunde >= 5 and temp > 22)
        {
          print("Hitzefrei!");
          break;
        }
        else
        {
          print("Temperatur: " + temp);
        }
      }
      stunde = stunde + 1;
    }

    """;

  [Fact]
  public void Course_program_stays_as_it_is()
  {
    Assert.Equal(Course, Format(Course));
  }

  [Fact]
  public void Messy_course_program_gets_the_style()
  {
    const string messy = """
      //notest
          // Heute hitzefrei?
      var temp=20;  var stunde =1;
      var klasse= 10;
      repeat(8){
      print ( "Stunde "+stunde ) ;
          if(klasse<10) {
        if (stunde>=5 and temp>22) { print("Hitzefrei!"); break; }
        else {
                print("Temperatur: "+temp);}
        }
      stunde=stunde+1;
      }
      """;
    Assert.Equal(Course, Format(messy));
  }

  [Theory]
  [InlineData("if (a) print(1);", "if (a)\n  print(1);\n")]
  [InlineData("if (a) print(1); else print(2);", "if (a)\n  print(1);\nelse\n  print(2);\n")]
  [InlineData("if (a) { x = 1; } else if (b) { x = 2; }", "if (a)\n{\n  x = 1;\n}\nelse if (b)\n{\n  x = 2;\n}\n")]
  [InlineData("while (i < 3) i = i + 1;", "while (i < 3)\n  i = i + 1;\n")]
  [InlineData("for (var i = 0; i < 10; i = i + 1) { print(i); }", "for (var i = 0; i < 10; i = i + 1)\n{\n  print(i);\n}\n")]
  [InlineData("while (a) if (b) x = 1;", "while (a)\n  if (b)\n    x = 1;\n")]
  [InlineData("var x = -1; var y = a - -b; var z = !(a and b); return -x;", "var x = -1;\nvar y = a - -b;\nvar z = !(a and b);\nreturn -x;\n")]
  [InlineData("print(- -3); print(! !true); print(-(-3));", "print(- -3);\nprint(! !true);\nprint(-(-3));\n")]
  [InlineData("var l = [1,2, 3]; print(l[0]); print(len(\"abc\")); o.f(1)(2);", "var l = [1, 2, 3];\nprint(l[0]);\nprint(len(\"abc\"));\no.f(1)(2);\n")]
  [InlineData("print(1); // expect: 1\n// own line\nprint(2);", "print(1); // expect: 1\n// own line\nprint(2);\n")]
  [InlineData("x = 1;\n\n\n\ny = 2;", "x = 1;\n\ny = 2;\n")]
  [InlineData("{\n\n  x = 1;\n\n}", "{\n  x = 1;\n}\n")]
  [InlineData("var a = 1;\n// doubles\nfun f(x) { return 2 * x; }\nfun g() { }", "var a = 1;\n\n// doubles\nfun f(x)\n{\n  return 2 * x;\n}\n\nfun g()\n{\n}\n")]
  [InlineData("class A < B { init(x) { this.x = x; super.init(); } }", "class A < B\n{\n  init(x)\n  {\n    this.x = x;\n    super.init();\n  }\n}\n")]
  [InlineData("repeat (3) { move(); turn(\"left\"); }", "repeat (3)\n{\n  move();\n  turn(\"left\");\n}\n")]
  public void Rules(string code, string expected)
  {
    Assert.Equal(expected, Format(code));
  }

  [Fact]
  public void Multiline_string_keeps_its_lines()
  {
    const string code = "var p = \"\n:def\n  c=Cat\n:grid\nc.\n\";\nassert _puzzle(p);\n";
    Assert.Equal(code, Format(code));
  }

  [Fact]
  public void Unreadable_code_comes_back_unchanged()
  {
    const string code = "print(\"open";
    Assert.Equal(code, Format(code));
  }

  public static TheoryData<string> All() => TestFiles.Data(TestFiles.All);

  [Theory]
  [MemberData(nameof(All))]
  public void Test_scripts_change_only_in_whitespace(string file)
  {
    var source = TestFiles.Source(file);
    if (source == null) return;
    var formatted = Format(source);
    Assert.Equal(Tokens(source), Tokens(formatted));
    Assert.Equal(formatted, Format(formatted));
  }

  static List<string> Tokens(string code)
  {
    var scanner = new Scanner(code);
    return scanner.ScanAllTokens(false).Select(t => $"{t.Type}:{(t.Type == TokenType.TokenComment ? t.StringValue.TrimEnd() : t.StringValue)}").ToList();
  }
}
