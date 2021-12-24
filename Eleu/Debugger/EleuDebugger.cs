namespace Eleu.Debugger
{

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
		VM vm;

		State state;
		RunMode runMode;
		ManualResetEventSlim evContinue;
		bool breakBeforeNextOp;
		Dictionary<Chunk, BreakpointInfo> breakpoints;
		Dictionary<string, Chunk[]> breakChunksForFile;

		public event Action<EleuDebugger>? TargetStopped;
		public event Action<EleuDebugger,string, int>? TargetHitBreakpoint;
		public event Action<EleuDebugger>? TargetRuntimeError;

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

		(string?, int) IsBreakpointHit()
		{
			lock (this)
			{
				if (!this.breakpoints.TryGetValue(vm.chunk, out var bp))
					return (null, -1);
				int curLine = bp.CDInfo.GetLine(vm.Ip, false);
				for (int i = 0; i < bp.Lines.Count; i++)
				{
					if (curLine == bp.Lines[i]) return (bp.CDInfo.FileName, curLine);
				}
				return (null, -1);
			}
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
				if (breakBeforeNextOp)
				{
					state = State.Stopped;
					evContinue.Reset();
					TargetStopped?.Invoke(this);
					continue;
				}
				// Check for breakpoint 
				var (file, line) = IsBreakpointHit();
				if (file != null)
				{
					state = State.Stopped;
					evContinue.Reset();
					TargetHitBreakpoint?.Invoke(this,file, line);
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
			runMode = RunMode.Normal;
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
