//#define DEBUG_TRACE_EXECUTION
using static Eleu.Vm.ValueStatics;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
namespace Eleu.Vm;

class CallFrame
{
	public ObjClosure? closure;
	public int ip;
	public int slotIndex;
};



public class VM: IInterpreter
{
	public int FRAMES_MAX = 1000;

	Value[] stack;
	int stackTop;
	CallFrame[] frames;
	internal int frameCount;
	Table globals;
	string initString;
	ObjUpvalue? openUpvalues;
	//this current frame
	CallFrame frame;
	//the current code chunk
	internal Chunk chunk;

	EleuOptions options;
	internal EleuResult result;
	internal int Ip => frame.ip;
	int instructionCount;
	public int InstructionCount => instructionCount;

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
		var _ = new NativeFunctions(this);
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

	internal void Setup()
	{
		instructionCount = 0;
		ObjFunction function = result.Function!;
		var closure = new ObjClosure(function);
		Push(CreateObjVal(closure));
		Call(closure, 0);
	}
	public EEleuResult Interpret()
	{
		Setup();
		EEleuResult result;
		try
		{
			do
			{
				result = NextStep();
			}
			while (result == EEleuResult.NextStep);
		}
		catch (EleuRuntimeError ex)
		{
			RuntimeError(ex.Message);
			result = EEleuResult.RuntimeError;
		}
		return result;
	}

	public EEleuResult InterpretWithDebug(CancellationToken cts)
	{
		Setup();
		EEleuResult result;
		try
		{
			do
			{
				instructionCount++; 
				result = NextStep();
				if (cts.IsCancellationRequested)
				{
					RuntimeError("Execution aborted by user.");
					cts.ThrowIfCancellationRequested();
					return EEleuResult.RuntimeError;
				}
			}
			while (result == EEleuResult.NextStep);
		}
		catch (EleuRuntimeError ex)
		{
			RuntimeError(ex.Message);
			result = EEleuResult.RuntimeError;
		}

		return result;
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
			frame.closure!.function.chunk.DisassembleInstruction(frame.ip, null, Console.Out);
#endif
		var instruction = (OpCode)ReadByte();
		switch (instruction)
		{
			case OP_NOT:
				Push(!Pop());
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
			case OP_NIL: Push(NilValue); break;
			case OP_TRUE: Push(BoolTrue); break;
			case OP_FALSE: Push(BoolFalse); break;
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
			case OP_EQUAL: Push(Pop() == Pop()); break;
			case OP_GREATER:
				{
					var b = Pop(); Push(Pop() > b);
					break;
				}
			case OP_LESS:
				{
					var b = Pop(); Push(Pop() < b);
					break;
				}
			case OP_ADD:
				{
					var b = Pop(); Push(Pop() + b);
					break;
				}
			case OP_SUBTRACT:
				{
					var b = Pop(); Push(Pop() - b);
					break;
				}
			case OP_MULTIPLY: Push(Pop() * Pop()); break;
			case OP_DIVIDE:
				{
					var b = Pop(); Push(Pop() / b);
					break;
				}
			case OP_REMAINDER:
				{
					var b = Pop(); Push(Pop() % b);
					break;
				}
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
			case OpNewList:
				NewList(); break;
			default:
				RuntimeError($"No such op code: {instruction} ({(int)instruction})");
				break;
		}
		return hasRuntimeError ? EEleuResult.RuntimeError : EEleuResult.NextStep;
	}

	private void NewList()
	{
		var argCount = (int)AsNumber(Pop());
		var list = new ValList(argCount);
		for (int i = 0; i < argCount; i++)
		{
			list.Add(stack[stackTop - argCount + i]);
		}
		stackTop -= argCount;
		Push(new Value(list));
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
		ObjClass superclass = AsClass(Pop());
		if (!BindMethod(superclass, name))
			hasRuntimeError = true;
	}
	void Invoke()
	{
		string method = ReadString();
		int argCount = ReadByte();
		Invoke(method, argCount);
	}
	void SuperInvoke()
	{
		string method = ReadString();
		int argCount = ReadByte();
		ObjClass superclass = AsClass(Pop());
		InvokeFromClass(superclass, method, argCount);
	}
	void Equal()
	{
		Value b = Pop();
		Value a = Pop();
		Push(CreateBoolVal(ValuesEqual(a, b)));
	}
	void Add()
	{
		var a1 = Pop();
		var a0 = Pop();
		var a0Num = IsNumber(a0);
		var a1Num = IsNumber(a1);
		if (a0Num && a1Num)
		{
			double b = AsNumber(a0);
			double a = AsNumber(a1);
			Push(CreateNumberVal(a + b));
			return;
		}
		var s0 = IsString(a0) ? AsString(a0) : (a0Num ? AsNumber(a0).ToString(CultureInfo.InvariantCulture) : null);
		var s1 = IsString(a1) ? AsString(a1) : (a1Num ? AsNumber(a1).ToString(CultureInfo.InvariantCulture) : null);
		if (s0 == null || s1 == null)
			RuntimeError("Operands must be two numbers or two strings.");
		else
			Push(CreateStringVal(s0 + s1));
	}

	void Call()
	{
		int argCount = ReadByte();
		CallValue(Peek(argCount), argCount);
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
		if (!IsClass(superclass))
		{
			RuntimeError("Superclass must be a class.");
			return;
		}
		ObjClass subclass = AsClass(Peek(0));
		tableAddAll(AsClass(superclass).methods, subclass.methods);
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
		ObjClass klass = AsClass(Peek(1));
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
	void CallValue(Value callee, int argCount)
	{
		if (IsObj(callee))
		{
			switch (OBJ_TYPE(callee))
			{
				case OBJ_BOUND_METHOD:
					{
						ObjBoundMethod bound = AS_BOUND_METHOD(callee);
						stack[stackTop - argCount - 1] = bound.receiver;
						Call(bound.method, argCount);
						return;
					}
				case OBJ_CLASS:
					{
						ObjClass klass = AsClass(callee);
						stack[stackTop - argCount - 1] = CreateObjVal(new ObjInstance(klass));
						Value initializer;
						if (tableGet(klass.methods, initString, out initializer))
						{
							Call(AS_CLOSURE(initializer), argCount);
						}
						else if (argCount != 0)
						{
							RuntimeError($"Expected 0 arguments but got {argCount}.");
						}
						return;
					}
				case OBJ_CLOSURE:
					Call(AS_CLOSURE(callee), argCount); return;
				case OBJ_NATIVE:
					{
						NativeFn native = AS_NATIVE(callee);
						Value[] args = new Value[argCount];
						for (int i = 0; i < argCount; i++)
						{
							args[i] = stack[stackTop - argCount + i];
						}
						try
						{
							Value result = native(args);
							stackTop -= argCount + 1;
							Push(result);
						}
						catch (Exception ex)
						{
							RuntimeError(ex.ToString());
						}
						return;
					}
				default:
					break; // Non-callable object type.
			}
		}
		RuntimeError("Can only call functions and classes.");
	}
	void InvokeFromClass(ObjClass klass, string name, int argCount)
	{
		if (!tableGet(klass.methods, name, out var method))
			RuntimeError($"Undefined property '{name}'.");
		else Call(AS_CLOSURE(method), argCount);
	}
	void Invoke(string name, int argCount)
	{
		Value receiver = Peek(argCount);
		if (!IsInstance(receiver))
		{
			RuntimeError("Only instances have methods."); return;
		}
		ObjInstance instance = AS_INSTANCE(receiver);
		if (tableGet(instance.fields, name, out var value))
		{
			stack[stackTop - argCount - 1] = value;
			CallValue(value, argCount); return;
		}
		InvokeFromClass(instance.klass, name, argCount);
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
	public void DefineNative(string name, NativeFn function)
	{
		var oname = name;
		var ofun = CreateObjVal(new ObjNative(function));
		tableSet(globals, oname, ofun);
	}
	void Call(ObjClosure closure, int argCount)
	{
		var function = closure.function;
		if (argCount != function.arity)
		{
			RuntimeError($"Expected {function.arity} arguments but got {argCount}.");
		}
		else if (frameCount >= FRAMES_MAX)
		{
			RuntimeError("Stack overflow."); return;
		}
		else PushFrame(closure, argCount + 1);
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
	public void RuntimeError(string msg)
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


