using Eleu.Puzzles;

namespace Eleu.Tests;

/// <summary>
/// Every script run the way the web app runs it (see <see cref="WebSession"/>). The transcripts, the HTML and canvas
/// code of the puzzles must stay as approved in Snapshots: the page of the pupils has to work as before.
/// </summary>
public class WebPathTests
{
  public static TheoryData<string> All() => TestFiles.Data(TestFiles.All);
  public static TheoryData<string> WithPuzzle() => TestFiles.Data(TestFiles.WithPuzzle);

  static string Name(string file) => Path.ChangeExtension(file, null);

  [Theory]
  [MemberData(nameof(All))]
  public void Script_runs_in_web_session_as_before(string file)
  {
    var session = new WebSession().Run(TestFiles.Read(file));
    Snapshot.Verify("Web", Name(file), session.Transcript);
  }

  [Theory]
  [MemberData(nameof(WithPuzzle))]
  public void Puzzle_renders_as_before(string file)
  {
    var session = new WebSession().Run(TestFiles.Read(file));
    var r = session.Renderings;
    var text = r.Count == 0 ? "(no puzzle shown)\n"
      : $"----- first of {r.Count} -----\n{r[0]}----- last -----\n{r[^1]}";
    Snapshot.Verify("Render", Name(file), text);
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
