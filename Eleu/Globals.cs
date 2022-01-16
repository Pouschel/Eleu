global using System;
global using Eleu.Scanning;
global using Eleu.Ast;
global using static Eleu.FunctionType;
global using static Eleu.EEleuResult;
global using static Eleu.Globals;
global using static Eleu.Scanning.TokenType;
using Eleu.Interpret;

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
class EleuResult
{
	public EEleuResult Result;
	//public Vm.ObjFunction? Function;
	public List<Stmt>? Expr;
	//public DebugInfo? DebugInfo;
}
public interface IInterpreter
{
	EEleuResult Interpret();
	void RuntimeError(string msg);
	void DefineNative(string name, NativeFn function);
	public int InstructionCount => 0;
	EEleuResult InterpretWithDebug(CancellationToken token);
}

public class Globals
{

	internal static EEleuResult CompileAndRunAst(string fileName, EleuOptions options)
	{
		var source = File.ReadAllText(fileName);
		return CompileAndRunAst(source, fileName, options);
	}

	public static (EEleuResult, IInterpreter?) Compile(string source, string fileName, EleuOptions options)
	{
		var scanner = new Scanner(source);
		var tokens = scanner.ScanAllTokens();
		var parser = new AstParser(options, fileName, tokens);
		var parseResult = parser.parse();

		var result = new EleuResult()
		{
			Result = parser.ErrorCount > 0 ? CompileError : Ok,
			Expr = parseResult,
		};
		if (result.Result != Ok) return (result.Result, null);
		//if (options.UseInterpreter)
		{
			var interpreter = new Interpreter(options, result.Expr!);
			return (result.Result, interpreter);
		}
		//var codeGen = new ByteCodeGenerator(fileName, options, result);
		//result = codeGen.GenCode();
		//if (result.Result != Ok)
		//	return (result.Result, null);
		//VM vm = new VM(options, result);
		//return (result.Result, vm);
	}

	internal static EEleuResult CompileAndRunAst(string source, string fileName, EleuOptions options)
	{
		var (res, vm) = Compile(source, fileName, options);
		if (res != Ok) return res;
		return vm!.Interpret();
	}

	public static bool RunFile(string path, TextWriter tw, bool debugPrintCode = false)
	{
		var opt = new EleuOptions()
		{
			Out = tw,
			PrintByteCode = debugPrintCode
		};
		var result = CompileAndRunAst(path, opt);
		return result == Ok;
	}

	public static bool RunTestCode(string source, TextWriter tw)
	{
		var opt = new EleuOptions()
		{
			Out = tw,
			Err = tw,
			PrintByteCode = false,
			DumpStackOnError = false,
			UseInterpreter = true,
		};
		var cres = CompileAndRunAst(source, "", opt);
		return cres == Ok;
	}
}