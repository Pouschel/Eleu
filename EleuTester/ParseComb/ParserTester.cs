using NUnit.Framework;

namespace EleuTester.ParseComb;
using static Parser<string>;


class SourceTester
{

  [Test]
  public static void TestRegex()
  {
    TestRex("Hallo", "Hal");
    TestRex("Hallo8", "Hallo[0-9]");
  }

  static void TestRex(string input, string rex)
  {
    var src = new Source(input);
    var res = src.MatchRegex(rex);
    Assert.IsNotNull(res);
    res!.Value.EqualExpected(input[..res.Value.Length]);

  }
}

class ParserTester
{

  [Test]
  public static void TestOr()
  {
    var parser = Match("bye").Or(Match("hai"));
    Assert.ExpectThrow<ParserError>(() => parser.ParseString("nix"));
    parser.ParseString("bye").EqualExpected("bye");
    parser.ParseString("hai").EqualExpected("hai");
  }

  [Test]
  public static void TestOr2()
  {
    var parser = MatchRegex("[a-z]+").Or(MatchRegex("[0-9]+"));
    Assert.ExpectThrow<ParserError>(() => parser.ParseString("a7"));
    parser.ParseString("hallo").EqualExpected("hallo");
    parser.ParseString("1309").EqualExpected("1309");
  }

  [Test]
  public static void TestBind()
  {
    // pair <- [0-9]+ "," [0-9]+
    var parser = MatchRegex("[0-9]+").Bind(first => Match(",").Bind(_ => MatchRegex("[0-9]+")
    .Bind(second => Constant((first, second)))));

    var (f, s) = parser.ParseString("12,34");
    f.EqualExpected("12"); s.EqualExpected("34");

  }
}