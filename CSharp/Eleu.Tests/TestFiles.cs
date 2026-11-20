using Eleu.Testing;

namespace Eleu.Tests;

/// <summary>
/// The test scripts in the folders listed in TestDirs.txt and TestDirs.local.txt (beside this file).
/// A script is named "&lt;folder name&gt;/&lt;path in the folder&gt;", e.g. "Tests/closure/nested_closure.eleu".
/// Files and folders starting with '-' are skipped (as in EleuTester), and scripts with a line "//notest".
/// </summary>
internal static class TestFiles
{
  const string DirsFile = "TestDirs.txt", LocalDirsFile = "TestDirs.local.txt";

  /// <summary>The configured folders (full paths), also those that do not exist.</summary>
  public static IReadOnlyList<string> Dirs { get; } = ReadDirs();

  /// <summary>The existing folders by their label (the first part of a script's name).</summary>
  static readonly Dictionary<string, string> roots = Labels();

  public static IReadOnlyList<string> All { get; } = Find();

  /// <summary>The scripts that set a puzzle with _puzzle().</summary>
  public static IReadOnlyList<string> WithPuzzle { get; } = All.Where(f => Read(f).Contains("_puzzle(")).ToList();

  /// <summary>
  /// The file of a script name – also of a name the Test Explorer remembers from an earlier discovery (the script
  /// may have got "//notest" or be gone since).
  /// </summary>
  public static string FullPath(string name)
  {
    int slash = name.IndexOf('/');
    if (slash < 0 || !roots.TryGetValue(name[..slash], out var root))
      throw new InvalidOperationException($"No test folder for '{name}' (see TestDirs.txt / TestDirs.local.txt). Build the test project to discover the tests again.");
    return Path.Combine(root, name[(slash + 1)..].Replace('/', Path.DirectorySeparatorChar));
  }

  public static string Read(string name) => File.ReadAllText(FullPath(name));

  /// <summary>
  /// The text of a script to test, or null if it is marked "//notest" by now. A script that is gone fails with a
  /// clear message (the Test Explorer still lists it until the project is built again).
  /// </summary>
  public static string? Source(string name)
  {
    var path = FullPath(name);
    if (!File.Exists(path))
      throw new FileNotFoundException($"Test script not found (anymore): {path}. Build the test project to discover the tests again.", path);
    var source = File.ReadAllText(path);
    return ScriptExpectation.IsNoTest(source) ? null : source;
  }

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

  static Dictionary<string, string> Labels()
  {
    var result = new Dictionary<string, string>();
    foreach (var root in Dirs.Where(Directory.Exists))
    {
      // the folder name; a second folder with the same name gets "#2" and so on
      var name = Path.GetFileName(root.TrimEnd('/', '\\'));
      var label = name;
      for (int n = 2; result.ContainsKey(label); n++) label = $"{name}#{n}";
      result[label] = root;
    }
    return result;
  }

  static List<string> Find()
  {
    var result = new List<string>();
    foreach (var (label, root) in roots)
      Collect(root, root, label);
    result.Sort(StringComparer.Ordinal);
    return result;

    void Collect(string root, string dir, string label)
    {
      foreach (var file in Directory.GetFiles(dir, "*.eleu"))
      {
        if (Path.GetFileName(file).StartsWith('-')) continue;
        if (ScriptExpectation.IsNoTest(File.ReadAllText(file))) continue;
        result.Add(label + "/" + Path.GetRelativePath(root, file).Replace('\\', '/'));
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
