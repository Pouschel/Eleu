using System.Text;
using System.Text.RegularExpressions;
using Eleu.LangServer;
using Eleu.Puzzles;

namespace Eleu.Tests;

/// <summary>
/// Talks to <see cref="EleuLanguageServer"/> the way the web app does (EleuWeb/WasmExecuter): direct callbacks,
/// "steps 20" after every "state true", every puzzle rendered with <see cref="PuzzleHtmlCreator"/> as HUI.SetPuzzle
/// does. Writes a transcript of the answers; timings are masked.
/// </summary>
internal sealed partial class WebSession
{
  public const string FileName = "[textfeld]";
  const int MaxCommands = 5_000_000;

  readonly EleuLanguageServer server;
  readonly Queue<(string Name, object Arg)> pending = new();
  readonly StringBuilder transcript = new();
  PuzzleHtmlCreator.RenderState renderState = new();
  bool pumping;
  bool running;
  string? lastGrid;

  public List<string> Renderings { get; } = [];
  public List<PuzzCode> PuzzleCalls { get; } = [];
  public List<string> PuzzleOutputs { get; } = [];
  public List<string> Errors { get; } = [];
  public bool? LastEnd { get; private set; }

  public WebSession()
  {
    server = new EleuLanguageServer(OnResponse, true);
  }

  public string Transcript => transcript.ToString();

  /// <summary>The "Start" button (or "Alle Tests" with <paramref name="runAllTests"/>).</summary>
  public WebSession Run(string code, bool runAllTests = false) => Send("code", new object[] { FileName, code, runAllTests });

  /// <summary>The puzzle field of the web app (plain or compressed text) and the selected test.</summary>
  public WebSession SetPuzzle(string text, int testIndex = 0) => Send("puzzle", new object[] { text, testIndex });

  public WebSession Send(string name, object arg)
  {
    pending.Enqueue((name, arg));
    if (pumping) return this;
    pumping = true;
    try
    {
      int count = 0;
      while (pending.TryDequeue(out var cmd))
      {
        if (++count > MaxCommands) throw new InvalidOperationException($"More than {MaxCommands} commands: the script does not end.");
        server.SendCommand(cmd.Name, cmd.Arg);
      }
    }
    finally { pumping = false; }
    return this;
  }

  void Log(string line) => transcript.Append(line).Append('\n');

  void OnResponse(string name, object arg)
  {
    switch (name)
    {
      case "nativeFuncs":
      case "puzzleFuncs":
        Log($"{name}: {string.Join(' ', (string[])arg)}");
        break;
      case "info":
        Log($"info: {MaskTimes((string)arg)}");
        break;
      case "err":
      case "i_err":
        Errors.Add((string)arg);
        Log($"{name}: {arg}");
        break;
      case "out":
        Log($"out: {arg}");
        break;
      case "pout":
        PuzzleOutputs.Add((string)arg);
        Log($"pout: {arg}");
        break;
      case "state":
        var nowRunning = (bool)arg;
        if (nowRunning != running) Log($"state: {(nowRunning ? "running" : "stopped")}");
        running = nowRunning;
        if (nowRunning) pending.Enqueue(("steps", 20));
        break;
      case "end":
        LastEnd = (bool)arg;
        Log($"end: {arg}");
        break;
      case "puzzle":
        var puzzle = (Puzzle)arg;
        var html = RecordingDom.Capture(() => new PuzzleHtmlCreator(puzzle).Render(ref renderState));
        Renderings.Add(html);
        var (state, grid) = PuzzleText.Describe(puzzle);
        Log($"puzzle: {state}; render {Snapshot.Hash(html)}");
        if (grid != lastGrid) Log(grid);
        lastGrid = grid;
        break;
      case "_pcall":
        var pc = (PuzzCode)arg;
        PuzzleCalls.Add(pc);
        Log($"_pcall: test {pc.TestIndex + 1}, show code {pc.ShowCompressedCode}, puzzle text {Snapshot.Hash(pc.Text)}");
        break;
      default:
        Log($"unknown answer {name}: {arg}");
        break;
    }
  }

  [GeneratedRegex(@"\d[\d.,]* ms")]
  private static partial Regex Millis();
  [GeneratedRegex(@"\([^()]*Bef\./s\)")]
  private static partial Regex Speed();

  static string MaskTimes(string s) => Speed().Replace(Millis().Replace(s, "# ms"), "(# Bef./s)");
}
