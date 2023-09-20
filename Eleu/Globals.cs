global using System;
global using Eleu.Scanning;
global using Eleu.Ast;
global using static Eleu.FunctionType;
global using static Eleu.EEleuResult;
global using static Eleu.Globals;
global using static Eleu.Scanning.TokenType;
using Eleu.Interpret;
using Eleu.Puzzles;

namespace Eleu;

public enum FunctionType
{
  FunTypeFunction,
  FunTypeInitializer,
  FunTypeMethod,
  FunTypeScript
}

public enum EEleuResult
{
  Ok,
  CompileError,
  CodeGenError,
  RuntimeError,
  NextStep
}

public delegate object NativeFn(object[] args);

public abstract class IInterpreter
{
  internal EleuOptions options;
  internal InputStatus currentStatus;
  public Puzzle? Puzzle;
  public Action<Puzzle?>? PuzzleStateChanged;
  public Action<string, int>? PuzzleCalled;
  public IInterpreter(EleuOptions options)
  {
    this.options = options;
    NativeFunctionBase.DefineAll<NativeFunctions>(this);
    NativeFunctionBase.DefineAll<PuzzleFunctions>(this);
  }
  public abstract EEleuResult Interpret(bool useVm);
  public abstract void RuntimeError(string msg);
  public abstract void DefineNative(string name, NativeFn function);

  public int InstructionCount = 0;
  public abstract EEleuResult InterpretWithDebug(CancellationToken token);
  public virtual int FrameTimeMs { get; set; }

  internal void NotifyPuzzleChange(Puzzle? newPuzzle, float animateState)
  {
    PuzzleStateChanged?.Invoke(newPuzzle);
  }
}

public class Globals
{
  internal const int UINT8_COUNT = 256;
  internal static EEleuResult CompileAndRunAst(string fileName, EleuOptions options)
  {
    var source = File.ReadAllText(fileName);
    return CompileAndRunAst(source, fileName, options);
  }
  public static List<Stmt>? ScanAndParse(string source, string fileName, EleuOptions options)
  {
    var scanner = new Scanner(source, fileName);
    var tokens = scanner.ScanAllTokens();
    var parser = new AstParser(options, tokens);
    var parseResult = parser.Parse();
    return parseResult;
  }

  public static (EEleuResult, Interpreter?) Compile(string source, string fileName, EleuOptions options)
  {
    var scanner = new Scanner(source, fileName);
    var tokens = scanner.ScanAllTokens();
    var parser = new AstParser(options, tokens);
    var parseResult = parser.Parse();
    var presult = parser.ErrorCount > 0 ? CompileError : Ok;
    if (presult != Ok) return (presult, null);
    var interpreter = new Interpreter(options, parseResult, tokens);
    return (presult, interpreter);
  }

  public static EEleuResult CompileAndRunAst(string source, string fileName, EleuOptions options)
  {
    var (res, vm) = Compile(source, fileName, options);
    if (res != Ok) return res;
    return vm!.Interpret(true);
  }
  public static bool RunFile(string path, TextWriter tw, bool useVm = false)
  {
    var opt = new EleuOptions()
    {
      Out = tw,
      Err = tw,
      UseInterpreter = !useVm
    };
    var result = CompileAndRunAst(path, opt);
    return result == Ok;
  }

  internal static int ExpandArray<T>(ref T[] array)
  {
    int len = array.Length;
    int newLen = len * 3 / 2;
    var newArray = new T[newLen];
    Array.Copy(array, newArray, len);
    array = newArray;
    return newLen;
  }

}