global using System;
global using System.Collections.Generic;
global using CsLox;
global using static CsLox.OpCode;
global using static Globals;

public class EleuOptions
{
	public bool PrintByteCode;
	public bool DumpStackOnError = true;

	public TextWriter Output = TextWriter.Null;

}

class Program
{
	static void Main(string[] args)
	{
		Console.WriteLine("Eleu v1");
		if (args.Length==0)
			Console.WriteLine(@"Usage:
Eleu -dumpByteCode fileName
-waitAfterRun      waits for an ENTER after running
-dumpByteCode      dumps the byte code of all functions
fileName           file to compile and run
");

		var options = new EleuOptions()
		{ Output = Console.Out };
		bool waitAfterRun = false;
		string? fileName=null;
		for (int i = 0; i < args.Length; i++)
		{
			var arg = args[i];
			switch (arg)
			{
				case "-waitAfterRun": waitAfterRun = true; break;
				case "-dumpByteCode": options.PrintByteCode = true; break;
				default: fileName = arg; break;
			}
		}
		if (fileName==null)
		{
			Console.WriteLine("No file to run");
			return;
		}
		CompileAndRun(fileName, options);
		if (waitAfterRun)	Console.ReadLine();
	}
}
