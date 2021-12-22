public class Globals
{
	internal const int UINT8_COUNT = 256;
	internal static bool identifiersEqual(in Token a, in Token b)
		=> a.StringValue == b.StringValue;
	internal static bool identifiersEqual(in Token a, string b)
	=> a.StringValue == b;
	internal static InterpretResult interpret(string source, string fileName, TextWriter tw
		, bool debugPrintCode = false)
	{
		var compiler = new Compiler(source, fileName, tw)
		{
			DEBUG_PRINT_CODE = debugPrintCode
		};
		var function = compiler.compile();
		if (function == null)
			return INTERPRET_COMPILE_ERROR;
		VM vm = new VM(tw);
		return vm.Interpret(function);
	}

	public static bool RunFile(string path, TextWriter tw, bool debugPrintCode = false)
	{
		string source = File.ReadAllText(path);
		InterpretResult result = interpret(source, path, tw, debugPrintCode);
		return result == INTERPRET_OK;
	}

	public static bool RunTestCode(string source, TextWriter tw)
	{
		var compiler = new Compiler(source, "", tw);
		var function = compiler.compile();
		if (function == null)
			return false;
		VM vm = new VM(tw)
		{
			DumpStackOnError = false
		};
		InterpretResult result = vm.Interpret(function);
		return result == INTERPRET_OK;
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