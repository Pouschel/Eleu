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
		if (args.Length > 0)
			RunFile(args[0], Console.Out, false);
		if (System.Diagnostics.Debugger.IsAttached)
			Console.ReadLine();
	}


}
