using System.Globalization;
using Eleu.LangServer;
using Eleu.Puzzles;

namespace Eleu.Tests;

/// <summary>The language server as the web app uses it: what pupils do on the page.</summary>
public class LanguageServerTests
{
  // from Eleu/Tests/puzzle/numbers/Maximum.eleu: four tests with random numbers (fixed seed)
  const string MaximumPuzzle = @"
:def
c=Cat
g=Green Square
seed=2
r=rnd 1 13
:grid
.
crrrrrrrrrr g.
.
:meta
win = Max 1 1 10 1 12 1
name = Maximum finden
funcs = move read write turn
:info
Lies die Zahlen ein und schreibe auf das grüne Feld die größte der Zahlen.
Das Programm muss für alle Testfälle funktionieren.
:grid
.
crrrrrrrrrr g.
.
:meta
:def
r=rnd 0 15
:grid
.
crrrrrrrrrr g.
.
:meta
:def
r=rnd 0 17
:grid
.
crrrrrrrrrr g.
.
:meta
hash = 7e5b46ca32
";

  const string MaximumSolution = @"
var max = 0;
repeat(10)
{
  var num = parseInt(read());
  move();
  if (num > max) max = num;
}
move(); write(max);
";

  static string PupilCode => PuzzleStatics.EncodePuzzle(MaximumPuzzle);

  [Fact]
  public void Ping_answers_with_version()
  {
    var s = new WebSession().Send("ping", "");
    Assert.Contains($"Sprachserver Version {EleuLanguageServer.Version}", s.Transcript);
  }

  [Fact]
  public void Function_names_stay_the_same()
  {
    var s = new WebSession().Send("funcnames", "puzzle").Send("funcnames", "native");
    Snapshot.Verify("Api", "funcnames", s.Transcript);
  }

  [Fact]
  public void Pupil_enters_compressed_puzzle_code_and_solves_it()
  {
    var s = new WebSession().SetPuzzle(PupilCode).Run(MaximumSolution);
    Assert.Empty(s.Errors);
    Assert.True(s.LastEnd);
    Assert.Contains(s.PuzzleOutputs, o => o.StartsWith("Puzzle (Test 1) gelöst"));
    Snapshot.Verify("Api", "pupil-solves-test1", s.Transcript);
  }

  [Fact]
  public void Plain_puzzle_text_works_like_compressed_code()
  {
    var plain = new WebSession().SetPuzzle(MaximumPuzzle).Run(MaximumSolution);
    var compressed = new WebSession().SetPuzzle(PupilCode).Run(MaximumSolution);
    Assert.Equal(compressed.Transcript, plain.Transcript);
  }

  [Fact]
  public void Pupil_selects_another_test()
  {
    var s = new WebSession().SetPuzzle(PupilCode, 2).Run(MaximumSolution);
    Assert.Contains(s.PuzzleOutputs, o => o.StartsWith("Puzzle (Test 3) gelöst"));
  }

  [Fact]
  public void Run_all_tests_solves_every_test()
  {
    var s = new WebSession().SetPuzzle(PupilCode).Run(MaximumSolution, runAllTests: true);
    Assert.Empty(s.Errors);
    for (int i = 1; i <= 4; i++)
      Assert.Contains(s.PuzzleOutputs, o => o.StartsWith($"Puzzle (Test {i}) gelöst"));
    Assert.Equal("Alle Tests bestanden!", s.PuzzleOutputs[^1]);
    Snapshot.Verify("Api", "pupil-run-all", s.Transcript);
  }

  [Fact]
  public void Wrong_solution_is_reported()
  {
    var s = new WebSession().SetPuzzle(PupilCode).Run("move();");
    Assert.False(s.LastEnd);
    Assert.Contains(s.Errors, e => e.Contains("wurde nicht gelöst"));
  }

  [Fact]
  public void Forbidden_function_is_an_error()
  {
    var s = new WebSession().SetPuzzle(PupilCode).Run("paint(\"Red\");");
    Assert.False(s.LastEnd);
    Assert.NotEmpty(s.Errors);
  }

  [Fact]
  public void Compile_error_reports_the_position()
  {
    var s = new WebSession().Run("var x = ;");
    Assert.NotEmpty(s.Errors);
    // the web app moves the cursor to the error with this position
    Assert.False(Eleu.Scanning.InputStatus.Parse(s.Errors[0]).IsEmpty);
  }

  [Fact]
  public void Puzzle_rendering_does_not_depend_on_culture()
  {
    string Render(CultureInfo culture)
    {
      var old = CultureInfo.CurrentCulture;
      CultureInfo.CurrentCulture = culture;
      try
      {
        var s = new WebSession().SetPuzzle(PupilCode);
        return s.Renderings[^1];
      }
      finally { CultureInfo.CurrentCulture = old; }
    }
    Assert.Equal(Render(CultureInfo.InvariantCulture), Render(TestSetup.German));
  }
}
