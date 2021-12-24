//#define DEBUG_TRACE_EXECUTION
global using static Eleu.EEleuResult;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Eleu.NativeFunctions;
namespace Eleu;

public enum EEleuResult
{
	Ok,
	CompileError,
	RuntimeError,
	NextStep
}

class CallFrame
{
	public ObjClosure? closure;
	public int ip;
	public int slotIndex;
};

public class VM
{
	public int FRAMES_MAX = 1000;

	Value[] stack;
	int stackTop;
	CallFrame[] frames;
	int frameCount;
	Table globals;
	string initString;
	ObjUpvalue? openUpvalues;
	//this current frame
	CallFrame frame;

	//the current code chunk
	Chunk chunk;

	EleuOptions options;
	EleuResult result;

	internal VM(EleuOptions options, EleuResult result)
	{
		this.options = options;
		this.result = result;
		stack = new Value[1000];
		stackTop = 0;
		globals = new();
		frames = new CallFrame[FRAMES_MAX];
		for (int i = 0; i < FRAMES_MAX; i++)
		{
			frames[i] = new CallFrame();
		}

		initString = string.Intern("init");
		DefineNative("clock", clock);
		// to avoid warnings
		frame = new CallFrame();
		chunk = new Chunk();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Push(Value val)
	{
		if (stackTop == stack.Length)
			ExpandArray(ref stack);
		stack[stackTop++] = val;
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Value Pop() => stack[--stackTop];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	Value Peek(int distance) => stack[stackTop - 1 - distance];
	void PushFrame(ObjClosure closure, int argCount)
	{
		frame = frames[frameCount++];
		frame.closure = closure;
		frame.ip = 0;
		frame.slotIndex = stackTop - argCount;
		chunk = frame.closure!.function!.chunk;
	}

	void PopFrame()
	{
		--frameCount;
		frame = frames[frameCount - 1];
		chunk = frame.closure!.function!.chunk;
	}

	internal void Interpret()
	{
		ObjFunction function = result.Function!;
		var closure = new ObjClosure(function);
		Push(CreateObjVal(closure));
		Call(closure, 0);
		result.Result = Run();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	byte ReadByte() => chunk.code[frame.ip++];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	ushort ReadShort()
	{
		frame.ip += 2;
		return (ushort)((chunk.code[frame.ip - 2] << 8) | chunk.code[frame.ip - 1]);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	Value ReadConstant() => chunk.constants[ReadByte()];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	string ReadString() => AsString(ReadConstant());

	bool hasRuntimeError;

	public EEleuResult NextStep()
	{
#if DEBUG_TRACE_EXECUTION
			Console.Write("          ");
			for (int i = 0; i < stackTop; i++)
			{
				var slot = stack[i];
				if (i == frame.slotIndex)
					Console.Write(" | ");
				Console.Write($"[{slot}]");
			}
			Console.WriteLine();
			frame.closure!.function.chunk.disassembleInstruction(frame.ip, Console.Out);
#endif
		var instruction = (OpCode)ReadByte();
		switch (instruction)
		{
			case OP_NOT:
				Push(BoolVal(IsFalsey(Pop())));
				break;
			case OP_NEGATE: Negate(); break;
			case OP_JUMP:
				{
					ushort offset = ReadShort();
					frame.ip += offset;
					break;
				}
			case OP_JUMP_IF_FALSE:
				{
					ushort offset = ReadShort();
					if (IsFalsey(Peek(0))) frame.ip += offset;
					break;
				}
			case OP_LOOP:
				{
					ushort offset = ReadShort();
					frame.ip -= offset;
					break;
				}
			case OP_PRINT:
				options.Out.WriteLine(Pop());
				break;
			case OP_CONSTANT:
				Value constant = ReadConstant();
				Push(constant);
				break;
			case OP_NIL: Push(Nil); break;
			case OP_TRUE: Push(BoolVal(true)); break;
			case OP_FALSE: Push(BoolVal(false)); break;
			case OP_POP: Pop(); break;
			case OP_GET_LOCAL:
				{
					byte slot = ReadByte();
					Push(stack[frame.slotIndex + slot]);
					break;
				}
			case OP_SET_LOCAL:
				{
					byte slot = ReadByte();
					stack[frame.slotIndex + slot] = Peek(0);
					break;
				}
			case OP_GET_GLOBAL: GetGlobal(); break;
			case OP_DEFINE_GLOBAL: DefineGlobal(); break;
			case OP_SET_GLOBAL: SetGlobal(); break;
			case OP_GET_UPVALUE: GetUpValue(); break;
			case OP_SET_UPVALUE: SetUpValue(); break;
			case OP_GET_PROPERTY: GetProperty(); break;
			case OP_SET_PROPERTY: SetProperty(); break;
			case OP_GET_SUPER: GetSuper(); break;
			case OP_EQUAL: Equal(); break;
			case OP_GREATER: PopAndOp((a, b) => BoolVal(a > b)); break;
			case OP_LESS: PopAndOp((a, b) => BoolVal(a < b)); break;
			case OP_ADD: Add(); break;
			case OP_SUBTRACT: PopAndOp((a, b) => CreateNumberVal(a - b)); break;
			case OP_MULTIPLY: PopAndOp((a, b) => CreateNumberVal(a * b)); break;
			case OP_DIVIDE: PopAndOp((a, b) => CreateNumberVal(a / b)); break;
			case OP_CALL: Call(); break;
			case OP_INVOKE: Invoke(); break;
			case OP_CLOSURE: Closure(); break;
			case OP_SUPER_INVOKE: SuperInvoke(); break;
			case OP_CLOSE_UPVALUE:
				CloseUpvalues(stackTop - 1);
				Pop();
				break;
			case OP_RETURN:
				if (Return()) return Ok;
				break;
			case OP_CLASS:
				Push(CreateObjVal(new ObjClass(ReadString())));
				break;
			case OP_INHERIT:
				Inherit();
				break;
			case OP_METHOD:
				DefineMethod(ReadString());
				break;
		}
		return hasRuntimeError ? EEleuResult.RuntimeError: EEleuResult.NextStep;
	}

	public EEleuResult Run()
	{
		EEleuResult result;
		do
		{
			result = NextStep();
		}
		while (result == EEleuResult.NextStep);
		return result;
	}
	void Negate()
	{
		if (!IsNumber(Peek(0)))
		{
			RuntimeError("Operand must be a number.");
			return;
		}
		Push(CreateNumberVal(-AsNumber(Pop())));
	}
	void GetGlobal()
	{
		string name = ReadString();
		if (!tableGet(globals, name, out var value))
		{
			RuntimeError($"Undefined variable '{ name}'.");
			return;
		}
		Push(value);
	}
	void DefineGlobal()
	{
		string name = ReadString();
		tableSet(globals, name, Peek(0));
		Pop();
	}
	void SetGlobal()
	{
		string name = ReadString();
		if (tableSet(globals, name, Peek(0)))
		{
			tableDelete(globals, name);
			RuntimeError($"Undefined variable '{name}'.");
		}
	}
	void GetUpValue()
	{
		byte slot = ReadByte();
		var upvalue = frame.closure!.upvalues[slot];
		int slotIndex = upvalue.slotIndex;
		Push(slotIndex >= 0 ? stack[slotIndex] : upvalue.closed);
	}
	void SetUpValue()
	{
		byte slot = ReadByte();
		var upvalue = frame.closure!.upvalues[slot];
		int slotIndex = upvalue.slotIndex;
		if (slotIndex >= 0) stack[slotIndex] = Peek(0);
		else upvalue.closed = Peek(0);
	}
	void GetProperty()
	{
		if (!IsInstance(Peek(0)))
		{
			RuntimeError("Only instances have properties.");
			return;
		}
		ObjInstance instance = AS_INSTANCE(Peek(0));
		string name = ReadString();
		if (tableGet(instance.fields, name, out var value))
		{
			Pop(); // Instance.
			Push(value);
			return;
		}
		if (!BindMethod(instance.klass, name))
			hasRuntimeError = true;
	}
	void SetProperty()
	{
		if (!IsInstance(Peek(1)))
		{
			RuntimeError("Only instances have fields.");
			return;
		}
		ObjInstance instance = AS_INSTANCE(Peek(1));
		tableSet(instance.fields, ReadString(), Peek(0));
		Value value = Pop();
		Pop();
		Push(value);
	}
	void GetSuper()
	{
		string name = ReadString();
		ObjClass superclass = AS_CLASS(Pop());
		if (!BindMethod(superclass, name))
			hasRuntimeError = true;
	}
	void Invoke()
	{
		string method = ReadString();
		int argCount = ReadByte();
		if (!Invoke(method, argCount))
			hasRuntimeError = true;
	}
	void Equal()
	{
		Value b = Pop();
		Value a = Pop();
		Push(BoolVal(ValuesEqual(a, b)));
	}
	void Add()
	{
		if (IsString(Peek(0)) && IsString(Peek(1)))
		{
			Concatenate();
		}
		else if (IsNumber(Peek(0)) && IsNumber(Peek(1)))
		{
			double b = AsNumber(Pop());
			double a = AsNumber(Pop());
			Push(CreateNumberVal(a + b));
		}
		else
			RuntimeError("Operands must be two numbers or two strings.");
	}

	void Call()
	{
		int argCount = ReadByte();
		if (!CallValue(Peek(argCount), argCount))
			hasRuntimeError = true;
	}
	void Closure()
	{
		ObjFunction function = AS_FUNCTION(ReadConstant());
		ObjClosure closure = new ObjClosure(function);
		Push(CreateObjVal(closure));
		for (int i = 0; i < closure.upvalueCount; i++)
		{
			byte isLocal = ReadByte();
			byte index = ReadByte();
			if (isLocal != 0)
			{
				closure.upvalues[i] = CaptureUpvalue(frame.slotIndex + index);
			}
			else
			{
				closure.upvalues[i] = frame.closure!.upvalues[index];
			}
		}
	}

	void SuperInvoke()
	{
		string method = ReadString();
		int argCount = ReadByte();
		ObjClass superclass = AS_CLASS(Pop());
		if (!InvokeFromClass(superclass, method, argCount))
			hasRuntimeError = true;
	}
	bool Return()
	{
		Value result = Pop();
		CloseUpvalues(frame.slotIndex);
		if (frameCount == 1)
		{
			Pop();
			return true;
		}
		stackTop = frame.slotIndex;
		PopFrame();
		Push(result);
		return false;
	}

	void Inherit()
	{
		Value superclass = Peek(1);
		if (!IS_CLASS(superclass))
		{
			RuntimeError("Superclass must be a class.");
			return;
		}
		ObjClass subclass = AS_CLASS(Peek(0));
		tableAddAll(AS_CLASS(superclass).methods, subclass.methods);
		Pop(); // Subclass.
	}
	void CloseUpvalues(int last)
	{
		while (openUpvalues != null && openUpvalues.slotIndex >= last)
		{
			ObjUpvalue upvalue = openUpvalues;
			upvalue.closed = stack[upvalue.slotIndex];
			upvalue.slotIndex = -1;
			openUpvalues = upvalue.next;
		}
	}
	void DefineMethod(string name)
	{
		Value method = Peek(0);
		ObjClass klass = AS_CLASS(Peek(1));
		tableSet(klass.methods, name, method);
		Pop();
	}
	ObjUpvalue CaptureUpvalue(int local)
	{
		ObjUpvalue? prevUpvalue = null;
		ObjUpvalue? upvalue = this.openUpvalues;
		while (upvalue != null && upvalue.slotIndex > local)
		{
			prevUpvalue = upvalue;
			upvalue = upvalue.next;
		}
		if (upvalue != null && upvalue.slotIndex == local)
		{
			return upvalue;
		}
		ObjUpvalue createdUpvalue = new ObjUpvalue(local);
		createdUpvalue.next = upvalue;
		if (prevUpvalue == null)
		{
			openUpvalues = createdUpvalue;
		}
		else
		{
			prevUpvalue.next = createdUpvalue;
		}
		return createdUpvalue;
	}
	bool CallValue(Value callee, int argCount)
	{
		if (IsObj(callee))
		{
			switch (OBJ_TYPE(callee))
			{
				case OBJ_BOUND_METHOD:
					{
						ObjBoundMethod bound = AS_BOUND_METHOD(callee);
						stack[stackTop - argCount - 1] = bound.receiver;
						return Call(bound.method, argCount);
					}
				case OBJ_CLASS:
					{
						ObjClass klass = AS_CLASS(callee);
						stack[stackTop - argCount - 1] = CreateObjVal(new ObjInstance(klass));
						Value initializer;
						if (tableGet(klass.methods, initString, out initializer))
						{
							return Call(AS_CLOSURE(initializer), argCount);
						}
						else if (argCount != 0)
						{
							RuntimeError($"Expected 0 arguments but got {argCount}.");
							return false;
						}
						return true;
					}
				case OBJ_CLOSURE:
					return Call(AS_CLOSURE(callee), argCount);
				case OBJ_NATIVE:
					{
						NativeFn native = AS_NATIVE(callee);
						Value[] args = new Value[argCount];
						for (int i = 0; i < argCount; i++)
						{
							args[i] = stack[stackTop - argCount + i];
						}
						Value result = native(args);
						stackTop -= argCount + 1;
						Push(result);
						return true;
					}
				default:
					break; // Non-callable object type.
			}
		}
		RuntimeError("Can only call functions and classes.");
		return false;
	}
	bool InvokeFromClass(ObjClass klass, string name, int argCount)
	{
		if (!tableGet(klass.methods, name, out var method))
		{
			RuntimeError($"Undefined property '{name}'.");
			return false;
		}
		return Call(AS_CLOSURE(method), argCount);
	}
	bool Invoke(string name, int argCount)
	{
		Value receiver = Peek(argCount);
		if (!IsInstance(receiver))
		{
			RuntimeError("Only instances have methods.");
			return false;
		}
		ObjInstance instance = AS_INSTANCE(receiver);
		if (tableGet(instance.fields, name, out var value))
		{
			stack[stackTop - argCount - 1] = value;
			return CallValue(value, argCount);
		}
		return InvokeFromClass(instance.klass, name, argCount);
	}
	bool BindMethod(ObjClass klass, string name)
	{
		Value method;
		if (!tableGet(klass.methods, name, out method))
		{
			RuntimeError($"Undefined property '{name}'.");
			return false;
		}
		ObjBoundMethod bound = new ObjBoundMethod(Peek(0), AS_CLOSURE(method));
		Pop();
		Push(CreateObjVal(bound));
		return true;
	}
	void DefineNative(string name, NativeFn function)
	{
		var oname = name;
		var ofun = CreateObjVal(new ObjNative(function));
		tableSet(globals, oname, ofun);
	}
	bool Call(ObjClosure closure, int argCount)
	{
		var function = closure.function;
		if (argCount != function.arity)
		{
			RuntimeError($"Expected {function.arity} arguments but got {argCount}.");
			return false;
		}
		if (frameCount >= FRAMES_MAX)
		{
			RuntimeError("Stack overflow.");
			return false;
		}
		PushFrame(closure, argCount + 1);
		return true;
	}

	void Concatenate()
	{
		string b = AsString(Pop());
		string a = AsString(Pop());
		var result = a + b;
		Push(CreateStringVal(result));
	}

	void PopAndOp(Func<double, double, Value> func)
	{
		if (!IsNumber(Peek(0)) || !IsNumber(Peek(1)))
		{
			RuntimeError("Operands must be numbers.");
			return;
		}
		double b = AsNumber(Pop());
		double a = AsNumber(Pop());
		Push(func(a, b));
	}

	void RuntimeError(string msg)
	{
		int instruction = frame.ip - 1;
		string text = $"Rerr: {msg}";
		var chunkInfo = result.DebugInfo?.GetChunkInfo(chunk);
		if (chunkInfo != null)
		{
			int line = chunkInfo.GetLine(instruction, false);
			text = $"{chunkInfo.FileName}({line}): {text}";
		}
		options.Err.WriteLine(text);
		Trace.WriteLine(text);
		if (options.DumpStackOnError) DumpStack();
		ResetStack();
		hasRuntimeError = true;
	}

	void DumpStack()
	{
		for (int i = frameCount - 1; i >= 0; i--)
		{
			CallFrame frame = frames[i];
			ObjFunction function = frame.closure!.function;
			int instruction = frame.ip - 1;
			var chDebugInfo = result.DebugInfo?.GetChunkInfo(function.chunk);
			string txt = function.NameOrScript;
			int? line = chDebugInfo?.GetLine(instruction, false);
			if (line.HasValue)
				txt += $" at line {line.Value}";
			options.Err.WriteLine(txt);
		}
	}

	void ResetStack()
	{
		stackTop = 0;
	}
}


