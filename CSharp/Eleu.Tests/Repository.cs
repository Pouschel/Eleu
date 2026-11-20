namespace Eleu.Tests;

/// <summary>Folders of the repository, found from the test assembly upwards.</summary>
internal static class Repository
{
  public static string Root { get; } = FindRoot();

  static string FindRoot()
  {
    string? folder = AppContext.BaseDirectory;
    while (folder != null && !File.Exists(Path.Combine(folder, "CSharp", "Eleu.sln")))
      folder = Path.GetDirectoryName(folder);
    return folder ?? throw new InvalidOperationException("CSharp/Eleu.sln not found above " + AppContext.BaseDirectory);
  }
}
