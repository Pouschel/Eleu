using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

	public override void Write(char value)
	{
		writeFunc(value.ToString());
	}
}

internal class EleuDebugSession: DebugSession
{
	TextWriter twDebug;
	public EleuDebugSession()
	{
		twDebug = new DebugWriter(this.WriteToConsole);
	}

	public override void Initialize(Response response, dynamic args)
	{
		SendResponse(response, new Capabilities()
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
		});
		twDebug.WriteLine("Debugger initialized!");
		// Mono Debug is ready to accept breakpoints immediately
		SendEvent(new InitializedEvent());
	}

	public override void Disconnect(Response response, dynamic arguments)
	{
		//SendErrorResponse(response, 3001, "Property 'program' is missing or empty.", null);	 // dialog
	}

	public override void Launch(Response response, dynamic arguments)
	{
		try
		{
			var array = arguments.args;
			var prog = (string) array[0];
			var options = new EleuOptions
			{
				CreateDebugInfo = true,
				Output = new DebugWriter(this.WriteToStdOut),
			};
			var cresult = Globals.CompileAndRun(prog, options);
			//SendResponse(response);
		}
		catch (Exception ex)
		{
			twDebug.WriteLine(ex.ToString());
			SendErrorResponse(response, 3001, $"Launch error: {ex.Message}", null);
		}
	}
}

