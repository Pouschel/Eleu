using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eleu.Vm;

namespace Eleu.Debugger
{
	
	class ChunkDebugInfo
	{
		public readonly string FileName;

		private readonly List<int> lines;

		internal readonly ObjFunction Function;

		public Chunk Chunk => Function.chunk;

		internal ChunkDebugInfo(string fileName, ObjFunction function)
		{
			this.FileName = fileName;
			this.Function = function;
			lines = new List<int>();
		}

		public int GetLine(int offset, bool onlyIfFirstOffset)
		{
			if (offset >= lines.Count || offset < 0) return -1;
			if (onlyIfFirstOffset && offset > 0 && lines[offset] == lines[offset - 1])
				return -1;
			return lines[offset];
		}

		internal void AddLine(int line) => lines.Add(line);

		public bool HasLine(int line) => lines.Contains(line);
	}

	public class DebugInfo
	{

		Dictionary<Chunk, ChunkDebugInfo> chunkInfos;

		public DebugInfo()
		{
			chunkInfos = new Dictionary<Chunk, ChunkDebugInfo>();
		}

		internal ChunkDebugInfo? GetChunkInfo(Chunk chunk)
		{
			chunkInfos.TryGetValue(chunk, out var result);
			return result;
		}
		internal ChunkDebugInfo? GetChunkInfo(string file, int line)
		{
			foreach (var cdi in chunkInfos.Values)
			{
				if (cdi.FileName != file) continue;
				if (cdi.HasLine(line)) return cdi;
			}
			return null;
		}

		internal void Add(ChunkDebugInfo chDebugInfo)
		{
			chunkInfos.Add(chDebugInfo.Function.chunk, chDebugInfo);
		}
	}
}
