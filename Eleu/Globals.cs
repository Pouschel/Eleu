global using System;
global using Eleu.Debugger;
global using static Eleu.OpCode;
global using static Eleu.Globals;
global using Eleu.Ast;
using Eleu.CodeGen;
using Eleu.Interpret;

namespace Eleu;


public class Globals
{
	internal const int UINT8_COUNT = 256;
	internal static bool identifiersEqual(in Token a, in Token b)
		=> a.StringValue == b.StringValue;
	internal static bool identifiersEqual(in Token a, string b)
	=> a.StringValue == b;

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
		if (options.UseInterpreter)
			return (result.Result, new Interpreter(options, result.Expr!));
		var codeGen = new ByteCodeGenerator(fileName, options, result);
		result = codeGen.GenCode();
		if (result.Result != Ok)
			return (result.Result, null);
		VM vm = new VM(options, result);
		return (result.Result, vm);
	}

	internal static EEleuResult CompileAndRunAst(string source, string fileName, EleuOptions options)
	{
		var (res, vm) = Compile(source, fileName, options);
		if (res != Ok) return res;
		return vm!.Interpret();
	}

	internal static EleuResult CompileAndRun(string fileName, EleuOptions options)
	{
		var source = File.ReadAllText(fileName);
		return CompileAndRun(source, fileName, options);
	}

	internal static EleuResult CompileAndRun(string source, string fileName, EleuOptions options)
	{
		var compiler = new Compiler(source, fileName, options);
		var cresult = compiler.Compile();
		if (cresult.Result != Ok)
			return cresult;

		VM vm = new VM(options, cresult);
		vm.Interpret();
		return cresult;
	}

	public static bool RunFile(string path, TextWriter tw, bool debugPrintCode = false)
	{
		var opt = new EleuOptions()
		{
			Out = tw,
			PrintByteCode = debugPrintCode
		};
		var result = CompileAndRun(path, opt);
		return result.Result == Ok;
	}

	public static bool RunTestCode(string source, TextWriter tw)
	{
		var opt = new EleuOptions()
		{
			Out = tw,
			Err = tw,
			PrintByteCode = false,
			DumpStackOnError = false,
		};
		var cres = CompileAndRunAst(source, "", opt);
		return cres == Ok;
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