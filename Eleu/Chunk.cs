using System.Globalization;
namespace CsLox;

class Chunk
{
	internal byte[] code;
	public int count;
	internal Value[] constants;
	int consCount;
	internal List<int> lines;

	public string FileName = "";

	public Chunk()
	{
		code = new byte[8];
		constants = new Value[4];
		lines = new();
	}

	public void write(byte by, int line = 0)
	{
		if (count >= code.Length)
			ExpandArray(ref code);
		code[count++] = by;
		lines.Add(line);
	}

	public void write(OpCode oc, int line = 0) => write((byte)oc, line);

	public void writeConstant(Value val)
	{
		write(OP_CONSTANT);
		var cons = addConstant(val);
		write((byte)cons);
	}

	public int addConstant(Value value)
	{
		for (int i = 0; i < consCount; i++)
		{
			if (valuesEqual(constants[i], value))
				return i;
		}
		if (consCount >= constants.Length)
			ExpandArray(ref constants);
		constants[consCount++] = value;
		return consCount - 1;
	}

	public void disassemble(string name, TextWriter? tw = null)
	{
		tw ??= Console.Out;
		tw.WriteLine($"== {name} ==");

		for (int offset = 0; offset < count;)
		{
			offset = disassembleInstruction(offset, tw);
		}
	}
	internal int disassembleInstruction(int offset, TextWriter tw)
	{
		tw.Write($"{offset:0000} ");
		if (offset>=lines.Count || offset > 0 && lines[offset] == lines[offset - 1])
			tw.Write("   | ");
		else
			tw.Write("{0,4} ", lines[offset]);

		var instruction = (OpCode)code[offset];
		var instructionString = $"{instruction.ToString()[3..],-16}";
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
									 offset - 2, isLocal !=0 ? "local" : "upvalue", index);
					}
					return offset;
				}
			case OP_JUMP:
			case OP_JUMP_IF_FALSE:
				return jumpInstruction(1);
			case OP_LOOP:return jumpInstruction(-1);
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
