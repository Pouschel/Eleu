using System.Globalization;
using System.Text;

namespace Eleu;

class Chunk
{
	internal byte[] code;
	public int count;
	internal Value[] constants;
	int consCount;

	public Chunk()
	{
		code = new byte[8];
		constants = new Value[4];
	}

	public void Write(byte by)
	{
		if (count >= code.Length)
			ExpandArray(ref code);
		code[count++] = by;
	}

	public void Write(OpCode oc) => Write((byte)oc);

	public int AddConstant(Value value)
	{
		for (int i = 0; i < consCount; i++)
		{
			if (ValuesEqual(constants[i], value))
				return i;
		}
		if (consCount >= constants.Length)
			ExpandArray(ref constants);
		constants[consCount++] = value;
		return consCount - 1;
	}

	public void Disassemble(string name, DebugInfo? dinfo, TextWriter? tw = null)
	{
		try
		{
			tw ??= Console.Out;
			tw.WriteLine($"== {name} ==");
			var cinfo = dinfo?.GetChunkInfo(this);
			for (int offset = 0; offset < count;)
			{
				offset = DisassembleInstruction(offset, cinfo, tw);
			}
		}
		catch (Exception)
		{ }
	}

	static string GetInstructionString(string s)
	{
		if (s.IndexOf('_') < 0) return s;
		var sb = new StringBuilder();
		bool nextUpper = false;
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			if (c == '_')
			{
				nextUpper = true; continue;
			}
			if (nextUpper)
				c = char.ToUpper(c);
			else
				c = char.ToLower(c);
			nextUpper = false;
			sb.Append(c);
		}
		return sb.ToString();
	}
	internal int DisassembleInstruction(int offset, ChunkDebugInfo? dinfo, TextWriter tw)
	{
		tw.Write($"{offset:0000} ");
		int line = dinfo?.GetLine(offset,true) ?? -1;
		if (line<0)
			tw.Write("   | ");
		else
			tw.Write("{0,4} ", line);

		var instruction = (OpCode)code[offset];

		var instructionString = GetInstructionString(instruction.ToString()[2..]);
		instructionString	= $"{instructionString,-16}";
		switch (instruction)
		{
			case OP_RETURN:
			case OP_NEGATE:
			case OP_ADD:
			case OP_SUBTRACT:
			case OP_MULTIPLY:
			case OP_DIVIDE:
			case OP_NIL:
			case OP_TRUE:
			case OP_FALSE:
			case OP_NOT:
			case OP_POP:
			case OP_EQUAL:
			case OP_GREATER:
			case OP_LESS:
			case OP_PRINT:
			case OP_CLOSE_UPVALUE:
			case OP_INHERIT:
			case OP_GET_SUPER:
			case OpNewList:
				tw.WriteLine(instructionString);
				return offset + 1;
			case OP_CONSTANT:
			case OP_DEFINE_GLOBAL:
			case OP_GET_GLOBAL:
			case OP_SET_GLOBAL:
			case OP_CLASS:
			case OP_GET_PROPERTY:
			case OP_SET_PROPERTY:
			case OP_METHOD:
				{
					var constant = code[offset + 1];
					tw.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} {1,4} '{2}'",
						instructionString, constant, constants[constant]));
					return offset + 2;
				}
			case OP_GET_LOCAL:
			case OP_SET_LOCAL:
			case OP_GET_UPVALUE:
			case OP_SET_UPVALUE:
			case OP_CALL:
				var slot = code[offset + 1];
				tw.WriteLine($"{instructionString} {slot}");
				return offset + 2;
			case OP_INVOKE:
			case OP_SUPER_INVOKE:
				{
					byte constant = code[offset + 1];
					byte argCount = code[offset + 2];
					tw.WriteLine($"{instructionString} ({argCount} args) {constants[constant]}");
					return offset + 3;
				}
			case OP_CLOSURE:
				{
					offset++;
					var constant = code[offset++];
					tw.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} {1,4} '{2}'",
						instructionString, constant, constants[constant]));
					ObjFunction function = AS_FUNCTION(constants[constant]);
					for (int j = 0; j < function.upvalueCount; j++)
					{
						int isLocal = code[offset++];
						int index = code[offset++];
						tw.WriteLine("{0:0000}    |                     {1} {2}",
									 offset - 2, isLocal != 0 ? "local" : "upvalue", index);
					}
					return offset;
				}
			case OP_JUMP:
			case OP_JUMP_IF_FALSE:
				return jumpInstruction(1);
			case OP_LOOP: return jumpInstruction(-1);
			default:
				tw.WriteLine($"Unknown opcode {instruction}");
				return offset + 1;
		}

		int jumpInstruction(int sign)
		{
			ushort jump = (ushort)(code[offset + 1] << 8);
			jump |= code[offset + 2];
			tw.WriteLine($"{instructionString} {offset} -> {offset + 3 + sign * jump}");
			return offset + 3;
		}

	}
}
