namespace Eleu.Tests;

/// <summary>Folders of the repository, found from the test assembly upwards.</summary>
internal static class Repository
{
  public static string Root { get; } = FindRoot();

  /// <summary>The test scripts: Eleu/Tests.</summary>
  public static string TestScripts => Path.Combine(Root, "Eleu", "Tests");

  /// <summary>The approved snapshots: CSharp/Eleu.Tests/Snapshots.</summary>
  public static string Snapshots => Path.Combine(Root, "CSharp", "Eleu.Tests", "Snapshots");

  static string FindRoot()
  {
    string? folder = AppContext.BaseDirectory;
    while (folder != null && !File.Exists(Path.Combine(folder, "CSharp", "Eleu.sln")))
      folder = Path.GetDirectoryName(folder);
    return folder ?? throw new InvalidOperationException("CSharp/Eleu.sln not found above " + AppContext.BaseDirectory);
  }
}
