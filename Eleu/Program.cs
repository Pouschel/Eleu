using System.Reflection;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("EleuDebugger")]

namespace Eleu;

public class EleuOptions
{
	public bool PrintByteCode;
	public bool DumpStackOnError = true;
	public bool CreateDebugInfo = false;
	public TextWriter Out = TextWriter.Null;
	public TextWriter Err = TextWriter.Null;

}

class Program
{
	public const int Revision = 3;

	static void Main(string[] args)
	{
		//TestDebugger();
		Console.WriteLine($"Eleu 1.1.{Revision}");
		if (args.Length == 0)
			Console.WriteLine(@"Usage:
Eleu -dumpByteCode fileName
-waitAfterRun      waits for an ENTER after running
-dumpByteCode      dumps the byte code of all functions
-debugInfo         creates debug info
fileName           file to compile and run
");

		var options = new EleuOptions()
		{ Out = Console.Out, Err = Console.Error };
		bool waitAfterRun = false;
		string? fileName = null;
		for (int i = 0; i < args.Length; i++)
		{
			var arg = args[i];
			switch (arg)
			{
				case "-waitAfterRun": waitAfterRun = true; break;
				case "-dumpByteCode": options.PrintByteCode = true; break;
				case "-debugInfo": options.CreateDebugInfo = true; break;
				default: fileName = arg; break;
			}
		}
		if (fileName == null)
		{
			Console.WriteLine("No file to run");
			return;
		}
		CompileAndRun(fileName, options);
		if (waitAfterRun) Console.ReadLine();
	}

	static void TestDebugger()
	{
		var fileName = @"C:\Code\Eleu\VsCodeLanguage\sampleWorkspace\Ex1.eleu";
		var options = new EleuOptions
		{
			CreateDebugInfo = true,
			Out = Console.Out,
			Err = Console.Error,
		};
		var source = File.ReadAllText(fileName);
		var compiler = new Compiler(source, fileName, options);
		var cresult = compiler.compile();
		if (cresult.Result != EEleuResult.Ok)
			return;

		VM vm = new VM(options, cresult);
		EleuDebugger debugger = new EleuDebugger(vm);
		debugger.TargetStopped += Debugger_TargetStopped;
		debugger.TargetRuntimeError += Debugger_TargetRuntimeError;
		debugger.TargetHitBreakpoint += Debugger_TargetHitBreakpoint;
		debugger.SetBreakPoints(fileName, new int[] { 3, 4 });
		debugger.Continue();

		Console.ReadLine();
	}

	private static void Debugger_TargetHitBreakpoint(EleuDebugger debugger, string fn, int line)
	{
		Console.WriteLine($"Breakpoint hit: {fn} {line}");
		debugger.Continue();
	}

	private static void Debugger_TargetRuntimeError(EleuDebugger debugger)
	{

	}

	private static void Debugger_TargetStopped(EleuDebugger debugger)
	{
		Console.WriteLine("Debugger stopped!");
	}
}
