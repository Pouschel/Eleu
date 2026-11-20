namespace Eleu.Tests;

/// <summary>
/// The test scripts in the folders listed in TestDirs.txt and TestDirs.local.txt (beside this file).
/// A script is named "&lt;folder name&gt;/&lt;path in the folder&gt;", e.g. "Tests/closure/nested_closure.eleu".
/// Files and folders starting with '-' are skipped (as in EleuTester).
/// </summary>
internal static class TestFiles
{
  const string DirsFile = "TestDirs.txt", LocalDirsFile = "TestDirs.local.txt";

  /// <summary>The configured folders (full paths), also those that do not exist.</summary>
  public static IReadOnlyList<string> Dirs { get; } = ReadDirs();

  static readonly Dictionary<string, string> paths = Find();

  public static IReadOnlyList<string> All { get; } = paths.Keys.Order(StringComparer.Ordinal).ToList();

  /// <summary>The scripts that set a puzzle with _puzzle().</summary>
  public static IReadOnlyList<string> WithPuzzle { get; } = All.Where(f => Read(f).Contains("_puzzle(")).ToList();

  public static string FullPath(string name) => paths[name];

  public static string Read(string name) => File.ReadAllText(FullPath(name));

  static List<string> ReadDirs()
  {
    var testProject = Path.Combine(Repository.Root, "CSharp", "Eleu.Tests");
    var dirs = new List<string>();
    foreach (var file in new[] { DirsFile, LocalDirsFile })
    {
      var path = Path.Combine(testProject, file);
      if (!File.Exists(path)) continue;
      foreach (var raw in File.ReadAllLines(path))
      {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#')) continue;
        var dir = Path.GetFullPath(Path.Combine(Repository.Root, line));
        if (!dirs.Contains(dir)) dirs.Add(dir);
      }
    }
    return dirs;
  }

  static Dictionary<string, string> Find()
  {
    var result = new Dictionary<string, string>();
    var labels = new HashSet<string>();
    foreach (var root in Dirs.Where(Directory.Exists))
    {
      // the folder name; a second folder with the same name gets "#2" and so on
      var label = Path.GetFileName(root.TrimEnd('/', '\\'));
      for (int n = 2; !labels.Add(label); n++) label = $"{Path.GetFileName(root.TrimEnd('/', '\\'))}#{n}";
      Collect(root, root, label);
    }
    return result;

    void Collect(string root, string dir, string label)
    {
      foreach (var file in Directory.GetFiles(dir, "*.eleu"))
      {
        if (Path.GetFileName(file).StartsWith('-')) continue;
        result[label + "/" + Path.GetRelativePath(root, file).Replace('\\', '/')] = file;
      }
      foreach (var sub in Directory.GetDirectories(dir))
      {
        if (Path.GetFileName(sub).StartsWith('-')) continue;
        Collect(root, sub, label);
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
