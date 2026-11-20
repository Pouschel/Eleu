using Eleu.Testing;
using Xunit.Sdk;

namespace Eleu.Tests;

/// <summary>
/// Failure messages of the script tests start with the full path of the script and a link that opens it in VS Code
/// at the line in question.
/// </summary>
internal static class ScriptLocation
{
  public static string Header(string file, int line)
  {
    var path = TestFiles.FullPath(file);
    var uri = "vscode://file/" + Uri.EscapeDataString(path.Replace('\\', '/').TrimStart('/')).Replace("%2F", "/").Replace("%3A", ":") + ":" + line;
    return $"Datei: {path}({line})\nIn VS Code: {uri}\n";
  }

  public static void Fail(string file, int line, string message) => throw new XunitException(Header(file, line) + message);

  /// <summary>Like Assert.Equal of the outputs; on failure the header points at the first expectation that differs.</summary>
  public static void Equal(string file, string source, string expected, string actual)
  {
    if (expected == actual) return;
    var expectedLines = expected.Split(Environment.NewLine);
    var actualLines = actual.Split(Environment.NewLine);
    int i = 0;
    while (i < expectedLines.Length && i < actualLines.Length && expectedLines[i] == actualLines[i]) i++;
    var comments = ScriptExpectation.Expectations(source).Select(e => e.Line).ToList();
    // an expectation that differs, or (more output than expected) the last expectation, or the first line
    var line = i < comments.Count ? comments[i] : comments.Count > 0 ? comments[^1] : 1;
    try
    {
      Assert.Equal(expected, actual);
    }
    catch (XunitException ex)
    {
      throw new XunitException(Header(file, line) + ex.Message);
    }
  }
}
