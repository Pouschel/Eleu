using Eleu.Vm;

namespace Eleu.Debugger
{
	struct FileLineInfo
	{
		public string FileName;
		public int LineNr;
		public int FrameCount;
		public bool HasBreakpoint;
	}

	class BreakpointInfo
	{
		public ChunkDebugInfo CDInfo;

		public List<int> Lines;

		public BreakpointInfo(ChunkDebugInfo cinfo)
		{
			this.CDInfo = cinfo;
			this.Lines = new();
		}
	}

	public class EleuDebugger
	{
		enum State
		{
			Running,
			Stopped,
			Ended
		}

		enum RunMode
		{
			Normal,
			StepOver,
			StepIn,
		}

		VM vm;

		State state;
		//RunMode runMode;
		ManualResetEventSlim evContinue;
		bool breakBeforeNextOp;
		Dictionary<Chunk, BreakpointInfo> breakpoints;
		Dictionary<string, Chunk[]> breakChunksForFile;
		ChunkDebugInfo? currentChunkDebug;

		public event Action<EleuDebugger>? TargetStopped;
		public event Action<EleuDebugger, string, int>? TargetHitBreakpoint;
		public event Action<EleuDebugger>? TargetRuntimeError;
		FileLineInfo currentPos = new ();
		FileLineInfo skipPos = new ();


		public EleuDebugger(VM vm)
		{
			this.evContinue = new ManualResetEventSlim();
			this.vm = vm;
			this.state = State.Stopped;
			this.evContinue.Reset();
			this.breakpoints = new();
			this.breakChunksForFile = new();
			Task.Run(Runner);
		}

		void SetHasBreakpoint(ref FileLineInfo fli)
		{
			fli.HasBreakpoint = false;
			if (!this.breakpoints.TryGetValue(vm.chunk, out var bp))
				return;
			if (bp.CDInfo.FileName != fli.FileName)
				return;
			if (bp.Lines.Contains(fli.LineNr))
				fli.HasBreakpoint = true;
		}

		bool CheckMustBreak()
		{
			if (skipPos.Equals(currentPos)) 
				return false;
			return currentPos.HasBreakpoint || breakBeforeNextOp;
		}

		void Runner()
		{
			vm.Setup();

			while (true)
			{
				if (state == State.Ended)
					break;
				if (state == State.Stopped)
				{
					evContinue.Wait();
					evContinue.Reset();
					state = State.Running;
				}

				if (vm.chunk != currentChunkDebug?.Chunk)
					currentChunkDebug = vm.result.DebugInfo?.GetChunkInfo(vm.chunk);
				if (currentChunkDebug != null)
				{
					int line = currentChunkDebug.GetLine(vm.Ip, true);
					if (line > 0)
					{
						currentPos.FileName = currentChunkDebug.FileName;
						currentPos.LineNr = line;
						currentPos.FrameCount = vm.frameCount;
						SetHasBreakpoint(ref currentPos);
					}
				}
				// Check for breakpoint 
				if (CheckMustBreak())
				{
					state = State.Stopped;
					evContinue.Reset();
					if (currentPos.HasBreakpoint)
					  TargetHitBreakpoint?.Invoke(this, currentPos.FileName, currentPos.LineNr);
					else
						TargetStopped?.Invoke(this);
					continue;
				}
				var result = vm.NextStep();
				if (result != EEleuResult.NextStep)
				{
					state = State.Ended;
					if (result == EEleuResult.RuntimeError)
						TargetRuntimeError?.Invoke(this);
					break;
				}
			}
		}
		public bool Continue()
		{
			if (state != State.Stopped) return false;
			skipPos = currentPos;
			//runMode = RunMode.Normal;
			evContinue.Set();
			return true;
		}
		public bool Break()
		{
			if (state != State.Running) return false;
			breakBeforeNextOp = true;
			while (state == State.Running)
			{
				Thread.Sleep(5);
			}
			breakBeforeNextOp = false;
			return true;
		}

		public void SetBreakPoints(string file, int[] lines)
		{
			lock (this)
			{
				var dinfo = vm.result.DebugInfo;
				if (dinfo == null) return;
				// remove the old break points
				if (this.breakChunksForFile.TryGetValue(file, out var bchunks))
				{
					foreach (var chunk in bchunks)
					{
						this.breakpoints.Remove(chunk);
					}
					this.breakChunksForFile.Remove(file);
				}
				// find the chunks for the new ones
				List<Chunk> breakChunks = new List<Chunk>();
				foreach (var line in lines)
				{
					var chunkDi = dinfo.GetChunkInfo(file, line);
					if (chunkDi == null)
						continue;
					if (!this.breakpoints.TryGetValue(chunkDi.Chunk, out var binfo))
					{
						binfo = new BreakpointInfo(chunkDi);
						this.breakpoints.Add(chunkDi.Chunk, binfo);
					}
					binfo.Lines.Add(line);
					if (!breakChunks.Contains(chunkDi.Chunk))
						breakChunks.Add(chunkDi.Chunk);
				}
				this.breakChunksForFile[file] = breakChunks.ToArray();
			}
		}
	}
}
