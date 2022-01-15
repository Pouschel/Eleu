using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eleu.Vm;
#nullable enable

namespace Eleu.Debugger;

class DebugWriter : TextWriter
{
	public override Encoding Encoding => Encoding.UTF8;

	Action<string> writeFunc;

	public DebugWriter(Action<string> writeFunc)
	{
		this.writeFunc = writeFunc;
	}

	public override void Write(string? value)
	{
		if (value != null)
			writeFunc(value);
	}

	public override void WriteLine(string? value)
	{
		if (value != null)
			writeFunc.Invoke(value + "\r\n");
	}

	public override void Write(char value)
	{
		writeFunc(value.ToString());
	}
}

internal class EleuDebugSession : DebugSession
{
	TextWriter twDebug;
	public EleuDebugSession()
	{
		twDebug = new DebugWriter(this.WriteToConsole);
	}

	public override void Initialize(Response response, dynamic args)
	{
		var cap = new Capabilities()
		{
			// This debug adapter does not need the configurationDoneRequest.
			supportsConfigurationDoneRequest = false,

			// This debug adapter does not support function breakpoints.
			supportsFunctionBreakpoints = false,

			// This debug adapter doesn't support conditional breakpoints.
			supportsConditionalBreakpoints = false,

			// This debug adapter does not support a side effect free evaluate request for data hovers.
			supportsEvaluateForHovers = false,

			// This debug adapter does not support exception breakpoint filters
			exceptionBreakpointFilters = new dynamic[0]

		};
		response.SetBody(cap);
		twDebug.WriteLine("Eleu Debugger initialized!");
		// Mono Debug is ready to accept breakpoints immediately
		SendEvent(new InitializedEvent());
	}

	public override void Disconnect(Response response, dynamic arguments)
	{
		twDebug.WriteLine(">Disconnect");
		//SendErrorResponse(response, 3001, "Property 'program' is missing or empty.", null);	 // dialog
	}

	public override void SetBreakpoints(Response response, dynamic arguments)
	{

	}

	public override void Threads(Response response, dynamic arguments)
	{
		twDebug.WriteLine(">Threads");
		var threads = new List<Thread>();
		threads.Add(new Thread(1, "<script>"));
		response.SetBody(new ThreadsResponseBody(threads));
	}

	public override void Launch(Response response, dynamic arguments)
	{
		try
		{
			var fileName = (string)arguments.program;
			var options = new EleuOptions
			{
				CreateDebugInfo = true,
				Out = new DebugWriter(this.WriteToStdOut),
				Err = new DebugWriter(this.WriteToStdErr),
			};
			var source = File.ReadAllText(fileName);
			var compiler = new Compiler(source, fileName, options);
			var cresult = compiler.Compile();
			if (cresult.Result != EEleuResult.Ok)
			{
				SendEvent(new TerminatedEvent());
				return;
			}

			VM vm = new VM(options, cresult);
			SendEvent(new StoppedEvent("step"));
			//vm.Interpret();


		}
		catch (Exception ex)
		{
			twDebug.WriteLine(ex.ToString());
			SendErrorResponse(response, 3001, $"Launch error: {ex.Message}", null);
		}
	}
}

