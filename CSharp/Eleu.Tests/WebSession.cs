using Eleu.LangServer;
using Eleu.Puzzles;

namespace Eleu.Tests;

/// <summary>
/// Talks to <see cref="EleuLanguageServer"/> the way the web app does (EleuWeb/WasmExecuter): direct callbacks,
/// "steps 20" after every "state true", every puzzle rendered with <see cref="PuzzleHtmlCreator"/> as HUI.SetPuzzle
/// does (into a <see cref="RecordingDom"/>).
/// </summary>
internal sealed class WebSession
{
  public const string FileName = "[textfeld]";
  const int MaxCommands = 5_000_000;

  readonly EleuLanguageServer server;
  readonly Queue<(string Name, object Arg)> pending = new();
  PuzzleHtmlCreator.RenderState renderState = new();
  bool pumping;

  /// <summary>The answers as the log of the page would show them: "out: ...", "err: ...", "pout: ...", "info: ...".</summary>
  public List<string> Log { get; } = [];
  public List<string> Errors { get; } = [];
  public List<string> InternalErrors { get; } = [];
  public List<string> PuzzleOutputs { get; } = [];
  public List<string> Renderings { get; } = [];
  public List<PuzzCode> PuzzleCalls { get; } = [];
  public bool? LastEnd { get; private set; }

  public WebSession()
  {
    server = new EleuLanguageServer(OnResponse, true);
  }

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

  /// <summary>The page log as text, for failure messages.</summary>
  public override string ToString() => string.Join('\n', Log);

  void OnResponse(string name, object arg)
  {
    switch (name)
    {
      case "nativeFuncs":
      case "puzzleFuncs":
        Log.Add($"{name}: {string.Join(' ', (string[])arg)}");
        break;
      case "info":
      case "out":
        Log.Add($"{name}: {arg}");
        break;
      case "err":
        Errors.Add((string)arg);
        Log.Add($"err: {arg}");
        break;
      case "i_err":
        // also exceptions thrown while rendering: the server catches them
        InternalErrors.Add((string)arg);
        Log.Add($"i_err: {arg}");
        break;
      case "pout":
        PuzzleOutputs.Add((string)arg);
        Log.Add($"pout: {arg}");
        break;
      case "state":
        if ((bool)arg) pending.Enqueue(("steps", 20));
        break;
      case "end":
        LastEnd = (bool)arg;
        break;
      case "puzzle":
        var puzzle = (Puzzle)arg;
        Renderings.Add(RecordingDom.Capture(() => new PuzzleHtmlCreator(puzzle).Render(ref renderState)));
        break;
      case "_pcall":
        PuzzleCalls.Add((PuzzCode)arg);
        break;
      default:
        InternalErrors.Add($"unknown answer {name}: {arg}");
        break;
    }
  }
}
