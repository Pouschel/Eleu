using NUnit.Framework;

namespace EleuTester.ParseComb;
using static Parser<string>;



class ParserTester
{

  [Test]
  public static void TestOr()
  {
    var parser= Match("bye").Or(Match("hai"));
    Assert.ExpectThrow<ParserError>(() => parser.ParseString("nix"));
    parser.ParseString("bye").EqualExpected("bye");
    parser.ParseString("hai").EqualExpected("hai");
  }
}