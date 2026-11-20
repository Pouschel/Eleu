namespace Eleu.Testing;

/// <summary>
/// The expected output of a test script, written in its comments (<c>// expect: </c>, <c>//? </c>,
/// <c>// expect runtime error: </c>, <c>//Rerr: </c>, <c>//Cerr: </c>), and the normalized output of a run.
/// Shared by EleuTester and Eleu.Tests.
/// </summary>
public static class ScriptExpectation
{
  static readonly string[] searches = ["// expect: ", "//? ", "// expect runtime error: ", "//Rerr: ", "//Cerr: "];

  /// <summary>
  /// A script with a line "//notest" (also "// notest", any case) is no test, e.g. a small program for the pupils.
  /// </summary>
  public static bool IsNoTest(string source) =>
    source.Split('\n').Any(line => line.Trim().Replace(" ", "").Equals("//notest", StringComparison.OrdinalIgnoreCase));

  public static string ExpectedOutput(string source)
  {
    var sw = new StringWriter();
    foreach (var (_, text) in Expectations(source))
      sw.WriteLine(text);
    return sw.ToString();
  }

  /// <summary>The expectations with the lines (from 1) of their comments.</summary>
  public static IEnumerable<(int Line, string Text)> Expectations(string source)
  {
    var lines = source.Split('\n');
    for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
    {
      var line = lines[lineIndex];
      for (int i = 0; i < searches.Length; i++)
      {
        string search = searches[i];
        int idx = line.IndexOf(search);
        if (idx < 0) continue;
        // "//Cerr: " keeps "Cerr: " in the expectation, like the compiler writes it
        int start = idx + (i < 4 ? search.Length : 2);
        yield return (lineIndex + 1, line[start..].TrimEnd());
        break;
      }
    }
  }

  /// <summary>Removes the position prefixes ("file(line,col): ") and the puzzle override notice.</summary>
  public static string NormalizeOutput(string s)
  {
    var lines = s.Split('\n');
    var sw = new StringWriter();
    foreach (var line in lines)
    {
      if (line.StartsWith("Die _puzzle() Funktion überschreibt das aktive Puzzle"))
        continue;
      var l = line.TrimEnd();
      int idx = l.IndexOf("): ");
      if (idx >= 0)
        l = l[(idx + 3)..];
      sw.WriteLine(l);
    }
    return sw.ToString();
  }
}
