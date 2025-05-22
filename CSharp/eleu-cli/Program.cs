using System.Diagnostics;
using Eleu.LangServer;
using Eleu.Puzzles;

namespace Eleu.Cli;

internal class Program
{
  bool useVm = true;

  static void Main(string[] args)
  {

    var msg = $@"Eleu CLI v{EleuLanguageServer.Version}
Arguments: 
-(vm|int)  use vm or interpreter version (default: vm)
-puzzle    log puzzle output
file       file to run
";


    var prog = new Program();
    if (args.Length < 1) return;

    string file = "";
    for (int i = 0; i < args.Length; i++)
    {
      var arg = args[i];
      switch (arg)
      {
        case "-vm": prog.useVm = true; continue;
        case "-int": prog.useVm = false; continue;
        default: file = arg; break;
      }
    }
    if (string.IsNullOrEmpty(file))
    {
      Console.WriteLine(msg);
      Console.WriteLine("No file given"); return;
    }
    RunFile(file, prog.useVm);
  }
  static Puzzle? dumpedPuzz;
  static void CheckDump(Puzzle? puzzle)
  {
    if (puzzle == null) return;
    if (puzzle.EqualsContent(dumpedPuzz)) return;
    dumpedPuzz = puzzle;
    puzzle.Dump(Console.Out);
  }
  public static void RunFile(string path, bool useVm = false, string dumpFile = "")
  {
    var opt = new EleuOptions()
    {
      Out = Console.Out,
      Err = Console.Error,
      DumpFileName = dumpFile,
      InputStatusFormatter = inp => $"{inp.FileName}:{inp.LineStart}:{inp.ColStart}:{inp.LineEnd}:{inp.ColEnd}"
    };
    Stopwatch stopwatch = Stopwatch.StartNew();
    var source = File.ReadAllText(path);
    var (res, vm) = Globals.Compile(source, path, opt);
    if (res != EEleuResult.Ok) return;
    vm!.PuzzleStateChanged += (puz) =>
    {
      CheckDump(puz);
    };
    var result = vm!.Interpret(useVm);
    stopwatch.Stop();
    if (result == EEleuResult.Ok)
    {
      Console.WriteLine($"Script [{path}] run in {stopwatch.ElapsedMilliseconds} ms");
    }
  }
}
