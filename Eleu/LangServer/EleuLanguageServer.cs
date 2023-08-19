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
	BlockingCollection<Cmd> queue = new(new ConcurrentQueue<Cmd>());
	Action<string, object> responseAction;
	Thread worker;

	Interpreter? _interpreter;
	EEleuResult _lastResult = EEleuResult.NextStep;
	bool _outStateChanged = false;
	Stopwatch _watch = new Stopwatch();
	PuzzleBundle? _bundle;
	int puzzleIndex = 0;

	public EleuLanguageServer(Action<string, object> responseAction)
	{
		this.responseAction = responseAction;
		worker = new Thread(HandlCmdFunc)
		{
			IsBackground = true,
			Name = "EleuLangServer"
		};
		worker.Start();
	}

	public void Dispose()
	{
		SendCommand("quit", "");
		worker.Join();
		queue.Dispose();
	}

	public void SendCommand(string name, object arg) => queue.Add(new(name, arg));
	void Response(string name, object arg)
	{
		responseAction(name, arg);
	}
	void _sendString(string head, string s)
	{
		Array.ForEach(s.Split("\n"), (element) =>
		{
			if (!string.IsNullOrEmpty(element)) Response(head, s);
		});
	}
	void _sendError(String msg) => _sendString("err", msg); // compiler and runtime errors
	void _sendInternalError(String msg) => _sendString("i_err", msg);
	void _sendInfo(String msg) => _sendString("info", msg); // information
	void _sendOutput(String msg) => _sendString("out", msg); // normal output from print
	/// send puzzle output
	void _sendPuzzleInfo(String msg) => _sendString("pout", msg);
	void _sendRunState(bool running) => Response("state", running);
	void _onErrorMsg(String s) => _sendError(s);
	void _onOutput(String s)
	{
		_sendOutput(s);
		_outStateChanged = true;
	}
	void _onPuzzleChanged(Puzzle? puzzle)
	{
		if (puzzle != null) Response("puzzle", puzzle);
		_outStateChanged = true;
	}
	void _onPuzzleSet(PuzzCode code)
	{
		Response("_pcall", code);
	}

	void HandlCmdFunc()
	{
		while (true)
		{
			var cmd = queue.Take();
			var name = cmd.Name;
			var arg = cmd.Arg;
			if (name == "quit") break;
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
						_sendInfo("Eleu Sprachserver bereit.");
						break;
					case "code":
						var fileName = ArgAtIndex<string>(0);
						var code = ArgAtIndex<string>(1);
						_endCodeHandler(fileName, code);
						break;
					case "steps":
						int steps = (int)arg;
						_nextSteps(steps);
						break;
					case "puzzle":
						var puzCode = ArgAtIndex<string>(0);
						var puzIndex = ArgAtIndex<int>(1);
						_endPuzzleHandler(puzCode, puzIndex);
						break;
					case "stop": _stop(); break;
					default: _sendInternalError($"Invalid cmd: {cmd}"); break;
				}
			}
			catch (Exception ex)
			{
				_sendInternalError(ex.Message);
			}

			T ArgAtIndex<T>(int idx) => (T)((object[])arg)[idx];
		}
	}
	void _stop()
	{
		_interpreter = null;
		_lastResult = EEleuResult.CompileError;
		_outStateChanged = false;
		_sendRunState(false);
	}
	void _endPuzzleHandler(string code, int puzIndex)
	{
		try
		{
			var bundle = PuzzleStatics.ParseBundle(code);
			_bundle = bundle;
			puzzleIndex = 0;
			if (bundle.Count > 0) _sendPuzzle(bundle[0]);
		}
		catch (PuzzleParseException ex)
		{
			_bundle = null;
			_sendError(ex.Message);
		}
	}
	void _sendPuzzle(Puzzle puzzle)
	{
		Response("puzzle", puzzle);
	}

	void _endCodeHandler(string fileName, string code)
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
		_lastResult = result;
		if (result == EEleuResult.Ok)
		{
			_interpreter = interp;
			interp!.Puzzle = _bundle?[puzzleIndex];
			interp!.PuzzleChanged = _onPuzzleChanged;
			interp!.puzzleSet = _onPuzzleSet;

			_lastResult = _interpreter!.start();
		}
		_sendRunState(_lastResult == EEleuResult.NextStep);
		_watch.Stop();
		_watch.Reset();
	}

	void _nextSteps(int maxSteps)
	{
		if (_interpreter == null || _lastResult != EEleuResult.NextStep)
		{
			//_sendInternalError("no program running");
			_sendRunState(false);
			return;
		}
		_watch.Start();
		_outStateChanged = false;
		var interp = _interpreter!;
		for (var i = 0; i < maxSteps && _lastResult == EEleuResult.NextStep; i++)
		{
			_lastResult = interp.step();
			if (_outStateChanged) break;
		}
		if (_lastResult == EEleuResult.Ok)
		{
			_sendInfo("Skriptausführung wurde normal beendet.");
			_watch.Stop();
			int statementCount = _interpreter!.ExecutedInstructionCount;
			var speed = statementCount / _watch.Elapsed.TotalSeconds;
			_sendInfo($"{statementCount:###,###,###} Befehle in {_watch.ElapsedMilliseconds} ms verarbeitet ({speed:###,###,###} Bef./s).");
			_checkPuzzleWin();

			_interpreter = null;
		}
		else if (_lastResult != EEleuResult.NextStep)
		{
			_sendError("Bei der Skriptausführung sind Fehler aufgetreten.");
			_interpreter = null;
		}
		_watch.Stop();
		_sendRunState(_lastResult == EEleuResult.NextStep);
	}

	void _checkPuzzleWin()
	{
		var puzzle = _interpreter?.Puzzle;
		if (puzzle == null) return;
		var bwon = puzzle.CheckWin();
		if (bwon == null)
		{
			_sendError($"Fehler beim Auswerten der Siegbedingung: '{puzzle.WinCond}'");
			return;
		}
		if (bwon == true)
		{
			var score = _interpreter!.PuzzleScore;
			_sendPuzzleInfo(
					$"Puzzle (Test {puzzle.BundleIndex + 1}) gelöst (Energie: {puzzle.EnergyUsed}; Länge: {_interpreter!.ProgramLength}; Anweisungen: {_interpreter!.ExecutedInstructionCount}; Score: {score}).");
			return;
		}
		_sendError($"Das Puzzle (Test {puzzle.BundleIndex + 1}) wurde nicht gelöst.");
	}
}


