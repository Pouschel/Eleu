
using System.Threading;
using Eleu.LangServer;
using Eleu.Puzzles;
namespace EleuStudio;

public class WasmExecuter
{
  EleuLanguageServer proc;
  const string SendColor = "cyan", ReceiveColor = "magenta";
  bool hasLogConsole;
  private bool isRunning;
  public string[] NativeFuncNames = Array.Empty<string>(), PuzzleFuncNames = Array.Empty<string>();
  int puzzleDelay = 0;
  /// <summary>
  /// true, if a script is running even when in break
  /// </summary>
  public bool IsAScriptRunning
  {
    get { return isRunning && !stopRequested; }
    set { isRunning = value; }
  }
  public bool InBreak { get; internal set; } = false;
  public bool CanResume { get; internal set; }
  bool stopRequested = false;

  public WasmExecuter()
  {
    hasLogConsole = true;

    proc = new(ProcResponseDelayed, true);
  }
  void Log(string s, string color, bool trim = false)
  {
    lock (this)
    {
      if (!hasLogConsole) return;
      if (s.StartsWith("§steps") || s.StartsWith("state ")) return;
      if (trim && s.Length > 60)
        s = s[..60] + "...";
      Console.WriteLine(s);
    }
  }
  public void Restart()
  {
    proc.Dispose();
    proc = new(ProcResponseDelayed, true);
    Log("Language server started", "Yellow");
    CallServer("funcnames", "puzzle");
    CallServer("funcnames", "native");
  }

  void CallServer(string s, object arg)
  {
    Log(s, SendColor);
    proc.SendCommand(s, arg);
  }
  void CallServerArray(string cmd, params object[] args) => CallServer(cmd, args);

  public void SendPing() => CallServer("ping", "");


  private void ProcResponseDelayed(string cmd, object arg)
  {
    SetTimeout(() => ProcResponse(cmd, arg),0);
  }

  private void ProcResponse(string cmd, object arg)
  {
    Log(cmd, ReceiveColor, true);
    var viewOptions = App.Options.View;

    switch (cmd)
    {
      case "nativeFuncs":
        NativeFuncNames = (string[])arg;
        break;
      case "puzzleFuncs":
        PuzzleFuncNames = (string[])arg;
        break;
      case "info":
        App.Println(arg.ToString(), viewOptions.LogInfoColor);
        break;
      case "err":
      case "i_err":
        App.Println(text(), viewOptions.LogErrorColor);
        //TODO App.Mvu.EnqueueMessage(new MoveToPosistionInFileMsg(text()));
        break;
      case "out": App.Println(text()); break;
      case "pout": App.Println(text(), viewOptions.LogPuzzleColor); break;
      case "state":
        bool running = IsAScriptRunning;
        IsAScriptRunning = (bool)arg;
        //TODO if (running != IsAScriptRunning)
        //TODO App.Mvu.EnqueueMessage(Msg.EngineStateChanged);
        if (IsAScriptRunning && puzzleDelay > 0)
        {
          Thread.Sleep(puzzleDelay); puzzleDelay = 0;
        }
        if (IsAScriptRunning)
        {
          CallServer("steps", 20);
        }
        break;
      case "puzzle":
        //TODO  App.Mvu.EnqueueMessage(new SetPuzzleResponseMsg(arg as Puzzle));
        puzzleDelay = App.Options.Puzzle.FrameTime;
        break;
      //TODO case "_pcall": App.Mvu.EnqueueMessage(new HandlePCallMsg(text())); break;

      default:
        Log("Unknown response: " + cmd, "red");
        break;
    }

    string text() => (string)arg;
  }


  internal void ClearInputStatus(string fileName)
  {
  }

  internal void Stop()
  {
    stopRequested = true;
    CallServer("stop", "");
  }

  public void Start(string code)
  {
    Stop();
    stopRequested = false;
    CallServerArray("code", "", code);
  }

  public void SendPuzzleText(string text, int selIndex)
  {
    CallServerArray("puzzle", text, selIndex);
  }

}


