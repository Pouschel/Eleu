global using System;
global using Eleu.Debugger;
global using static Eleu.OpCode;
global using static Eleu.Globals;
namespace Eleu;

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
		var cresult = compiler.compile();
		if (cresult.Result != INTERPRET_OK)
			return cresult;

		VM vm = new VM(options, cresult);
		vm.Interpret();
		return cresult;
	}

	public static bool RunFile(string path, TextWriter tw, bool debugPrintCode = false)
	{
		var opt = new EleuOptions()
		{
			Output = tw,
			PrintByteCode = debugPrintCode
		};
		var result = CompileAndRun(path, opt);
		return result.Result == INTERPRET_OK;
	}

	public static bool RunTestCode(string source, TextWriter tw)
	{
		var opt = new EleuOptions()
		{
			Output = tw,
			PrintByteCode = false,
			DumpStackOnError = false,
		};
		var cres = CompileAndRun(source, "", opt);
		return cres.Result == INTERPRET_OK;
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