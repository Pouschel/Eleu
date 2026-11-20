namespace Eleu.Tests;

/// <summary>The test scripts in Eleu/Tests. Files and folders starting with '-' are skipped (as in EleuTester).</summary>
internal static class TestFiles
{
  public static IReadOnlyList<string> All { get; } = Find();

  /// <summary>The scripts that set a puzzle with _puzzle().</summary>
  public static IReadOnlyList<string> WithPuzzle { get; } = All.Where(f => Read(f).Contains("_puzzle(")).ToList();

  public static string FullPath(string relative) => Path.Combine(Repository.TestScripts, relative);

  public static string Read(string relative) => File.ReadAllText(FullPath(relative));

  static List<string> Find()
  {
    var root = Repository.TestScripts;
    var files = new List<string>();
    Collect(root);
    files.Sort(StringComparer.Ordinal);
    return files;

    void Collect(string dir)
    {
      foreach (var file in Directory.GetFiles(dir, "*.eleu"))
      {
        if (Path.GetFileName(file).StartsWith('-')) continue;
        files.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
      }
      foreach (var sub in Directory.GetDirectories(dir))
      {
        if (Path.GetFileName(sub).StartsWith('-')) continue;
        Collect(sub);
      }
    }
  }

  public static TheoryData<string> Data(IEnumerable<string> files)
  {
    var data = new TheoryData<string>();
    foreach (var f in files) data.Add(f);
    return data;
  }
}
