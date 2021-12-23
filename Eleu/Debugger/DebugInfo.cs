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

		public int GetLineIfNew(int offset)
		{
			if (offset >= lines.Count || offset > 0 && lines[offset] == lines[offset - 1])
				return -1;
			return lines[offset];
		}
	}

	internal class DebugInfo
	{
		internal ChunkDebugInfo? GetChunkInfo(Chunk chunk) => null;
	}
}
