global using System;
global using Eleu.Scanning;
global using Eleu.Ast;
global using static Eleu.FunctionType;
global using static Eleu.EEleuResult;
global using static Eleu.Globals;
global using static Eleu.Scanning.TokenType;
global using static Eleu.Messages;
using Eleu.Interpret;


namespace Eleu;

public enum FunctionType
{
  FunTypeFunction,
  FunTypeInitializer,
  FunTypeMethod,
  /// <summary>
  /// fake type for top level script
  /// </summary>
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

public class Globals
{
  public static EEleuResult CompileAndRunAst(string fileName, EleuOptions options, bool useVm)
  {
    var source = File.ReadAllText(fileName);
    return CompileAndRunAst(source, fileName, options, useVm);
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

  public static EEleuResult CompileAndRunAst(string source, string fileName, EleuOptions options, bool useVm)
  {
    var (res, vm) = Compile(source, fileName, options);
    if (res != Ok) return res;
    return vm!.Interpret(useVm);
  }
  public static bool RunFile(string path, TextWriter tw, bool useVm = false, string dumpFile = "")
  {
    var opt = new EleuOptions()
    {
      Out = tw,
      Err = tw,
      DumpFileName = dumpFile,
    };
    var result = CompileAndRunAst(path, opt, useVm);
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