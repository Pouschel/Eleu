

global using System;
global using Eleu.Debugger;
global using static Eleu.OpCode;
global using static Eleu.Globals;
using Eleu.Ast;

namespace Eleu;

public class EleuEngine
{


	internal static EleuResult CompileAndRun(string fileName, EleuOptions options)
	{
		var source = File.ReadAllText(fileName);
		return CompileAndRun(source, fileName, options);
	}
	
	internal static EleuResult CompileAndRun(string source, string fileName, EleuOptions options)
	{
		var scanner = new Scanner(source);
		var tokens = scanner.ScanAllTokens();
		var parser = new AstParser(options,fileName,tokens);
		var expr= parser.parse();

		var result= new EleuResult()
		{
			Result= expr==null ? EEleuResult.CompileError: EEleuResult.Ok,
			Expr = expr,
		};
		return result;

		//var compiler = new Compiler(source, fileName, options);
		//var cresult = compiler.Compile();
		//if (cresult.Result != Ok)
		//	return cresult;

		//VM vm = new VM(options, cresult);
		//vm.Interpret();
		//return cresult;
	}
}

public class Globals
{
	internal const int UINT8_COUNT = 256;
	internal static bool identifiersEqual(in Token a, in Token b)
		=> a.StringValue == b.StringValue;
	internal static bool identifiersEqual(in Token a, string b)
	=> a.StringValue == b;

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
		var cres = CompileAndRun(source, "", opt);
		return cres.Result == Ok;
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