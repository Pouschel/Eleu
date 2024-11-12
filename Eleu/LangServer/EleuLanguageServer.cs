using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Eleu.Interpret;
using Eleu.Puzzles;

namespace Eleu.LangServer;


class CallbackWriter : TextWriter
{
  Action<string> callback;
  public override Encoding Encoding { get; } = Encoding.UTF8;
  public CallbackWriter(Action<string> callback)
  {
    this.callback = callback;
  }
  // ignore: non_constant_identifier_names
  public override void WriteLine(string? msg)
  {
    if (msg != null) callback(msg);
  }
}

record struct Cmd(string Name, object Arg)
{ }

public class EleuLanguageServer : IDisposable
{
  public const string Version = "24.4";
  BlockingCollection<Cmd> queue = new(new ConcurrentQueue<Cmd>());
  Action<string, object> responseAction;
  Thread? worker;

  Interpreter? interpreter;
  EEleuResult lastResult = EEleuResult.NextStep;
  bool _outStateChanged = false;
  Stopwatch _watch = new Stopwatch();
  PuzzleBundle? _bundle;
  int puzzleIndex = 0;
  bool directCallback = false;
  bool runAllTests = false;
  string? curFileName, curCode;
  List<Type> additionalNativeFunctions = [];
  public EleuLanguageServer(Action<string, object> responseAction, bool directCallback = false)
  {
    this.responseAction = responseAction;
    this.directCallback = directCallback;
    if (directCallback) return;
    worker = new Thread(HandlCmdFunc)
    {
      IsBackground = true,
      Name = "EleuLangServer"
    };
    worker.Start();
  }
  public void RegisterNativeFunctions(Type type)
  {
    additionalNativeFunctions.Add(type);
  }

  public void Dispose()
  {
    SendCommand("quit", "");
    worker?.Join();
    queue.Dispose();
  }

  public void SendCommand(string name, object arg)
  {
    if (directCallback) HandleCmd(new(name, arg));
    else queue.Add(new(name, arg));
  }

  void Response(string name, object arg)
  {
    responseAction(name, arg);
  }
  void _sendString(string head, string s)
  {
    foreach (var element in s.Split("\n"))
    {
      Response(head, element.TrimEnd());
    }
  }
  void _sendError(String msg) => _sendString("err", msg); // compiler and runtime errors
  void _sendInternalError(String msg) => _sendString("i_err", msg);
  void _sendInfo(String msg) => _sendString("info", msg); // information
  void _sendOutput(String msg) => _sendString("out", msg); // normal output from print
  /// send puzzle output
  void _sendPuzzleInfo(String msg) => _sendString("pout", msg);
  void _sendRunState(bool running) => Response("state", running);
  void SendEndProgram(bool noError) => Response("end", noError);
  void _onErrorMsg(String s) => _sendError(s);
  void _onOutput(String s)
  {
    _sendOutput(s);
    _outStateChanged = true;
  }
  void OnPuzzleStateChanged(Puzzle? puzzle)
  {
    if (puzzle != null) Response("puzzle", puzzle);
    _outStateChanged = true;
  }
  void OnPuzzleSet(string code, int testIndex)
  {
    Response("_pcall", new PuzzCode(code, testIndex));
  }

  bool HandleCmd(Cmd cmd)
  {
    var name = cmd.Name;
    var arg = cmd.Arg;
    if (name == "quit") return false;
    try
    {
      switch (name)
      {
        case "funcnames":
          var sarg = arg.ToString();
          var funcs = "native" == sarg ? new NativeFunctions().GetFunctions().Select(mi => mi.name).Union(new string[] { "PI" }).ToArray()
          : new PuzzleFunctions().GetFunctions().Select(mi => mi.name).ToArray();
          Response($"{sarg}Funcs", funcs);
          break;
        case "ping":
          _sendInfo($"Eleu Sprachserver Version {Version} bereit.");
          break;
        case "code":
          var fileName = ArgAtIndex<string>(0);
          var code = ArgAtIndex<string>(1);
          this.runAllTests = ArgAtIndex<bool>(2);
          EndCodeHandler(fileName, code);
          break;
        case "steps":
          int steps = (int)arg;
          NextSteps(steps);
          break;
        case "puzzle":
          var puzCode = ArgAtIndex<string>(0);
          var puzIndex = ArgAtIndex<int>(1);
          EndPuzzleHandler(puzCode, puzIndex);
          break;
        case "stop": Stop(); break;
        default: _sendInternalError($"Invalid cmd: {cmd}"); break;
      }
    }
    catch (Exception ex)
    {
      _sendInternalError(ex.Message);
    }

    T ArgAtIndex<T>(int idx) => (T)((object[])arg)[idx];
    return true;
  }
  void HandlCmdFunc()
  {
    while (true)
    {
      var cmd = queue.Take();
      if (!HandleCmd(cmd)) break;
    }
  }
  void Stop()
  {
    interpreter = null;
    lastResult = EEleuResult.CompileError;
    _outStateChanged = false;
    _sendRunState(false);
    _sendError("Programm abgebrochen!");
  }
  void EndPuzzleHandler(string code, int puzIndex)
  {
    try
    {
      var bundle = PuzzleStatics.ParseBundle(code);
      _bundle = bundle;
      if (puzIndex < 0 || puzIndex >= bundle.Count)
        puzIndex = 0;
      this.puzzleIndex = puzIndex;
      if (bundle.Count > 0) SendPuzzle(bundle[puzIndex]);
      else _bundle = null;
    }
    catch (PuzzleParseException ex)
    {
      _bundle = null;
      _sendError(ex.Message);
    }
  }
  void SendPuzzle(Puzzle puzzle) => Response("puzzle", puzzle);

  void EndCodeHandler(string fileName, string code)
  {
    var opt = new EleuOptions()
    {
      Out = new CallbackWriter(_onOutput),
      Err = new CallbackWriter(_onErrorMsg),
      OnlyFirstError = true,
    };
    _watch.Restart();
    var (result, interp) = Compile(code, fileName, opt);
    _sendInfo($"Skript übersetzt in {_watch.ElapsedMilliseconds} ms");
    lastResult = result;
    if (result == EEleuResult.Ok)
    {
      interpreter = interp!;
      foreach (var type in additionalNativeFunctions)
      {
        if (Activator.CreateInstance(type) is not NativeFunctionBase func)
          _sendInternalError($"type {type.Name} is no native function type");
        else
          NativeFunctionBase.DefineAll(func, interpreter);
      }
      interpreter.Puzzle = _bundle?[puzzleIndex];
      interpreter.PuzzleStateChanged = OnPuzzleStateChanged;
      interpreter.PuzzleCalled = OnPuzzleSet;
      lastResult = interpreter!.Start();
      this.curCode = code;
      this.curFileName = fileName;
    }
    _sendRunState(lastResult == EEleuResult.NextStep);
    _watch.Stop();
    _watch.Reset();
  }

  void NextSteps(int maxSteps)
  {
    if (interpreter == null || lastResult != EEleuResult.NextStep)
    {
      _sendRunState(false);
      return;
    }
    _watch.Start();
    _outStateChanged = false;
    var interp = interpreter!;
    for (var i = 0; i < maxSteps && lastResult == EEleuResult.NextStep; i++)
    {
      lastResult = interp.Step();
      if (_outStateChanged) break;
    }
    if (lastResult == EEleuResult.Ok)
    {
      _sendInfo("Skriptausführung wurde normal beendet.");
      _watch.Stop();
      int statementCount = interpreter!.ExecutedInstructionCount;
      var speed = statementCount / _watch.Elapsed.TotalSeconds;
      _sendInfo($"{statementCount:###,###,###} Befehle in {_watch.ElapsedMilliseconds} ms verarbeitet ({speed:###,###,###} Bef./s).");
      bool bwin = CheckPuzzleWin();
      interpreter = null;
      SendEndProgram(bwin);
      if (bwin && runAllTests && puzzleIndex < this._bundle?.Count - 1 && this.curCode != null)
      {
        puzzleIndex++;
        OnPuzzleSet(_bundle!.Code, puzzleIndex);
        EndCodeHandler(this.curFileName ?? "", this.curCode);
      }
      else runAllTests = false;
    }
    else if (lastResult != EEleuResult.NextStep)
    {
      _sendError("Bei der Skriptausführung sind Fehler aufgetreten.");
      interpreter = null;
      SendEndProgram(false);
      runAllTests = false;
    }
    _watch.Stop();
    _sendRunState(lastResult == EEleuResult.NextStep);
  }

  bool CheckPuzzleWin()
  {
    var puzzle = interpreter?.Puzzle;
    if (puzzle == null) return true;
    var bwon = puzzle.CheckWin();
    if (bwon == null)
    {
      _sendError($"Fehler beim Auswerten der Siegbedingung: '{puzzle.WinCond}'");
      return false;
    }
    if (bwon == true)
    {
      var score = interpreter!.PuzzleScore;
      _sendPuzzleInfo(
          $"Puzzle (Test {puzzle.BundleIndex + 1}) gelöst (Energie: {puzzle.EnergyUsed}; Länge: {interpreter!.ProgramLength}; Anweisungen: {interpreter!.ExecutedInstructionCount}; Score: {score}).");
      return true;
    }
    _sendError($"Das Puzzle (Test {puzzle.BundleIndex + 1}) wurde nicht gelöst.");
    return false;
  }
}


