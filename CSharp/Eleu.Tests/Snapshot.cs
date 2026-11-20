using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Eleu.Tests;

/// <summary>
/// Approved outputs, one file per group: CSharp/Eleu.Tests/Snapshots/&lt;group&gt;.txt, with one entry per name
/// (a line "##### name", then the text). Differing outputs are collected in &lt;group&gt;.received.txt (ignored by
/// git) and the test fails. With the environment variable ELEU_UPDATE_SNAPSHOTS=1 the tested entries are written
/// into the snapshot files instead – only do that when the change is intended.
/// </summary>
internal static class Snapshot
{
  public static bool Update => Environment.GetEnvironmentVariable("ELEU_UPDATE_SNAPSHOTS") == "1";

  static readonly ConcurrentDictionary<string, SnapshotFile> files = new();

  static Snapshot()
  {
    AppDomain.CurrentDomain.ProcessExit += (_, _) =>
    {
      foreach (var f in files.Values) f.Save();
    };
  }

  public static void Verify(string group, string name, string actual)
  {
    var file = files.GetOrAdd(group, g => new SnapshotFile(Path.Combine(Repository.Snapshots, g + ".txt")));
    file.Verify(name, actual);
  }

  /// <summary>A short hash for large texts in a snapshot.</summary>
  public static string Hash(string text) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ReplaceLineEndings("\n"))))[..12].ToLowerInvariant();

  sealed class SnapshotFile
  {
    const string Header = "##### ";
    readonly string path, receivedPath;
    readonly Dictionary<string, string> approved;
    readonly ConcurrentDictionary<string, string> updated = new(), received = new();

    public SnapshotFile(string path)
    {
      this.path = path;
      receivedPath = Path.ChangeExtension(path, ".received.txt");
      approved = File.Exists(path) ? Parse(File.ReadAllText(path)) : [];
    }

    static string Normalize(string text) => text.ReplaceLineEndings("\n").TrimEnd('\n') + "\n";

    static Dictionary<string, string> Parse(string text)
    {
      var result = new Dictionary<string, string>();
      string? name = null;
      var sb = new StringBuilder();
      foreach (var line in Normalize(text).Split('\n'))
      {
        if (line.StartsWith(Header))
        {
          if (name != null) result[name] = Normalize(sb.ToString());
          name = line[Header.Length..];
          sb.Clear();
        }
        else if (name != null) sb.Append(line).Append('\n');
      }
      if (name != null) result[name] = Normalize(sb.ToString());
      return result;
    }

    public void Verify(string name, string actual)
    {
      actual = Normalize(actual);
      if (actual.Contains("\n" + Header) || actual.StartsWith(Header))
        throw new InvalidOperationException($"Output of {name} contains a line starting with '{Header}'.");
      if (Update)
      {
        updated[name] = actual;
        return;
      }
      if (!approved.TryGetValue(name, out var expected))
      {
        received[name] = actual;
        Assert.Fail($"No snapshot '{name}' in {Path.GetFileName(path)} yet. Check it in {Path.GetFileName(receivedPath)} and approve it with ELEU_UPDATE_SNAPSHOTS=1.");
      }
      if (expected == actual) return;
      received[name] = actual;
      Assert.Equal(expected, actual);
    }

    public void Save()
    {
      if (updated.Count > 0)
      {
        var all = new Dictionary<string, string>(approved);
        foreach (var (k, v) in updated) all[k] = v;
        Write(path, all);
      }
      if (received.Count > 0) Write(receivedPath, received);
      else if (File.Exists(receivedPath)) File.Delete(receivedPath);
    }

    static void Write(string file, IEnumerable<KeyValuePair<string, string>> entries)
    {
      var sb = new StringBuilder();
      foreach (var (k, v) in entries.OrderBy(e => e.Key, StringComparer.Ordinal))
        sb.Append(Header).Append(k).Append('\n').Append(v);
      Directory.CreateDirectory(Path.GetDirectoryName(file)!);
      File.WriteAllText(file, sb.ToString());
    }
  }
}
