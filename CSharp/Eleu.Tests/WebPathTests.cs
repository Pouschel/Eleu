using System.Text.RegularExpressions;
using Eleu.Puzzles;
using Eleu.Testing;

namespace Eleu.Tests;

/// <summary>
/// Every script run the way the web app runs it (see <see cref="WebSession"/>): stepwise, only the first compile
/// error, every puzzle state rendered. The output must match the expectations in the script's comments, too.
/// </summary>
public partial class WebPathTests
{
  public static TheoryData<string> All() => TestFiles.Data(TestFiles.All);
  public static TheoryData<string> WithPuzzle() => TestFiles.Data(TestFiles.WithPuzzle);

  // shown by the web app, not part of the expectations: the runtime error summary and the call stack after it,
  // and the win check at the end of a script that does not solve its puzzle (e.g. pixel art)
  const string ErrorSummary = "Bei der Skriptausführung sind Fehler aufgetreten.";
  const string CallStack = "Aufgerufene Funktionen:";
  [GeneratedRegex(@"^Das Puzzle \(Test \d+\) wurde nicht gelöst\.$")]
  private static partial Regex NotSolved();

  [Theory]
  [MemberData(nameof(All))]
  public void Output_in_web_app_matches_expectations(string file)
  {
    var source = TestFiles.Read(file);
    var session = new WebSession().Run(source);
    Assert.True(session.InternalErrors.Count == 0, $"Internal errors (e.g. while rendering a puzzle):\n{session}");

    var shown = session.Log
      .Where(l => l.StartsWith("out: ") || l.StartsWith("err: "))
      .Select(l => l[5..])
      .TakeWhile(l => l != CallStack)
      .Where(l => l != ErrorSummary && !NotSolved().IsMatch(l));
    var actual = ScriptExpectation.NormalizeOutput(string.Join('\n', shown)).ReplaceLineEndings().TrimEnd();

    // the web app reports only the first compile error
    var expectedLines = ScriptExpectation.ExpectedOutput(source).ReplaceLineEndings().TrimEnd().Split(Environment.NewLine).ToList();
    int firstCerr = expectedLines.FindIndex(l => l.StartsWith("Cerr: "));
    if (firstCerr >= 0) expectedLines = expectedLines[..(firstCerr + 1)];
    var expected = string.Join(Environment.NewLine, expectedLines).TrimEnd();

    Assert.Equal(expected, actual);
  }

  [Theory]
  [MemberData(nameof(WithPuzzle))]
  public void Compressed_puzzle_code_decodes_to_the_same_puzzle(string file)
  {
    var session = new WebSession().Run(TestFiles.Read(file));
    foreach (var call in session.PuzzleCalls)
    {
      var original = PuzzleStatics.ParseBundle(call.Text);
      var decoded = PuzzleStatics.ParseBundle(call.Compressed);
      Assert.Equal(original.Count, decoded.Count);
      for (int i = 0; i < original.Count; i++)
      {
        Assert.True(original[i].EqualsContent(decoded[i]), $"Test {i + 1}: grid differs after decoding");
        Assert.Equal(original[i].Name, decoded[i].Name);
        // decoding adds a line break at the end of the description (invisible in the page)
        Assert.Equal(original[i].Description.TrimEnd(), decoded[i].Description.TrimEnd());
        Assert.Equal(original[i].WinCond, decoded[i].WinCond);
        Assert.Equal(original[i].GetAllowedFuncString(" "), decoded[i].GetAllowedFuncString(" "));
      }
    }
  }
}
