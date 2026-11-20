namespace Eleu.Testing;

/// <summary>
/// The expected output of a test script, written in its comments (<c>// expect: </c>, <c>//? </c>,
/// <c>// expect runtime error: </c>, <c>//Rerr: </c>, <c>//Cerr: </c>), and the normalized output of a run.
/// Shared by EleuTester and Eleu.Tests.
/// </summary>
public static class ScriptExpectation
{
  static readonly string[] searches = ["// expect: ", "//? ", "// expect runtime error: ", "//Rerr: ", "//Cerr: "];

  public static string ExpectedOutput(string source)
  {
    var sw = new StringWriter();
    var lines = source.Split('\n');
    foreach (var line in lines)
    {
      for (int i = 0; i < searches.Length; i++)
      {
        string search = searches[i];
        int idx = line.IndexOf(search);
        if (idx < 0) continue;
        // "//Cerr: " keeps "Cerr: " in the expectation, like the compiler writes it
        int start = idx + (i < 4 ? search.Length : 2);
        var resString = line[start..].TrimEnd();
        sw.WriteLine(resString);
        break;
      }
    }
    return sw.ToString();
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
