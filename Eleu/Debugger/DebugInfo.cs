using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eleu.Debugger
{
	
	public class ChunkDebugInfo
	{
		public readonly string FileName;

		public readonly List<int> lines;

		internal readonly ObjFunction Function;

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

		internal void Add(ChunkDebugInfo chDebugInfo)
		{
			chunkInfos.Add(chDebugInfo.Function.chunk, chDebugInfo);
		}
	}
}
