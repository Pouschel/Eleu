global using System;
global using System.Collections.Generic;
global using CsLox;
global using static CsLox.OpCode;
global using static Globals;

class Program
{
	static void Main(string[] args)
	{
		Console.WriteLine("Eleu v1");
		if (args.Length > 0)
			runFile(args[0]);
		//Console.ReadLine();
	}

	static void runFile(string path)
	{
		string source = File.ReadAllText(path);
		//DumpTokens(source);
		InterpretResult result = interpret(source, path, Console.Out, false);
		if (result == INTERPRET_COMPILE_ERROR) Environment.Exit(65);
		if (result == INTERPRET_RUNTIME_ERROR) Environment.Exit(70);
	}

	static void DumpTokens(string source, TextWriter tw)
	{
		new Compiler(source, "", tw).DumpTokens();
	}
}
