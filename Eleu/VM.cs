//#define DEBUG_TRACE_EXECUTION
global using static CsLox.InterpretResult;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static CsLox.NativeFunctions;
namespace CsLox;

public enum InterpretResult
{
	INTERPRET_OK,
	INTERPRET_COMPILE_ERROR,
	INTERPRET_RUNTIME_ERROR
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
	public bool DumpStackOnError = true;

	Value[] stack;
	int stackTop;
	CallFrame[] frames;
	int frameCount;
	Table globals;
	TextWriter tw;
	string initString;
	ObjUpvalue? openUpvalues;

	internal VM(TextWriter tw)
	{
		stack = new Value[1000]; 
		stackTop = 0;
		globals = new();
		frames = new CallFrame[100];
		this.tw = tw;
		initString = string.Intern("init");
		defineNative("clock", clock);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void push(Value val)
	{
		if (stackTop == stack.Length)
			ExpandArray(ref stack);
		stack[stackTop++] = val;
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Value pop() => stack[--stackTop];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	Value peek(int distance) => stack[stackTop - 1 - distance];

	CallFrame CreateFrame(ObjClosure closure, int argCount)
	{
		if (frameCount >= frames.Length)
			ExpandArray(ref frames);
		var frame = frames[frameCount++];
		if (frame == null)
		{
			frame = new CallFrame();
			frames[frameCount - 1] = frame;
		}
		frame.closure = closure;
		frame.ip = 0;
		frame.slotIndex = stackTop - argCount;
		return frame;
	}
	internal InterpretResult interpret(ObjFunction function)
	{
		var closure = new ObjClosure(function);
		push(OBJ_VAL(closure));
		call(closure, 0);
		return run();
	}

	public InterpretResult run()
	{
		CallFrame frame = frames[frameCount - 1];
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		Chunk chunk() => frame.closure!.function!.chunk;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		byte READ_BYTE() => chunk().code[frame.ip++];
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		ushort READ_SHORT()
		{
			frame.ip += 2;
			return (ushort)((chunk().code[frame.ip - 2] << 8) | chunk().code[frame.ip - 1]);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		Value READ_CONSTANT() => chunk().constants[READ_BYTE()];
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		string READ_STRING() => AS_STRING(READ_CONSTANT());

		InterpretResult iresult = INTERPRET_OK;
		while (true)
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
			var instruction = (OpCode)READ_BYTE();
			switch (instruction)
			{
				case OP_NOT:
					push(BOOL_VAL(isFalsey(pop())));
					break;
				case OP_NEGATE:
					if (!IS_NUMBER(peek(0)))
					{
						runtimeError("Operand must be a number.");
						return INTERPRET_RUNTIME_ERROR;
					}
					push(NUMBER_VAL(-AS_NUMBER(pop())));
					break;
				case OP_JUMP:
					{
						ushort offset = READ_SHORT();
						frame.ip += offset;
						break;
					}
				case OP_JUMP_IF_FALSE:
					{
						ushort offset = READ_SHORT();
						if (isFalsey(peek(0))) frame.ip += offset;
						break;
					}
				case OP_LOOP:
					{
						ushort offset = READ_SHORT();
						frame.ip -= offset;
						break;
					}

				case OP_PRINT:
					tw.WriteLine(pop());
					break;
				case OP_CONSTANT:
					Value constant = READ_CONSTANT();
					push(constant);
					break;
				case OP_NIL: push(NIL_VAL); break;
				case OP_TRUE: push(BOOL_VAL(true)); break;
				case OP_FALSE: push(BOOL_VAL(false)); break;
				case OP_POP: pop(); break;
				case OP_GET_LOCAL:
					{
						byte slot = READ_BYTE();
						push(stack[frame.slotIndex + slot]);
						break;
					}
				case OP_SET_LOCAL:
					{
						byte slot = READ_BYTE();
						stack[frame.slotIndex + slot] = peek(0);
						break;
					}
				case OP_GET_GLOBAL:
					{
						string name = READ_STRING();
						Value value;
						if (!tableGet(globals, name, out value))
						{
							runtimeError($"Undefined variable '{ name}'.");
							return INTERPRET_RUNTIME_ERROR;
						}
						push(value);
						break;
					}
				case OP_DEFINE_GLOBAL:
					{
						string name = READ_STRING();
						tableSet(globals, name, peek(0));
						pop();
						break;
					}
				case OP_SET_GLOBAL:
					{
						string name = READ_STRING();
						if (tableSet(globals, name, peek(0)))
						{
							tableDelete(globals, name);
							runtimeError($"Undefined variable '{name}'.");
							return INTERPRET_RUNTIME_ERROR;
						}
						break;
					}
				case OP_GET_UPVALUE:
					{
						byte slot = READ_BYTE();
						var upvalue = frame.closure!.upvalues[slot];
						int slotIndex = upvalue.slotIndex;
						push(slotIndex >= 0 ? stack[slotIndex] : upvalue.closed);
						break;
					}
				case OP_SET_UPVALUE:
					{
						byte slot = READ_BYTE();
						var upvalue = frame.closure!.upvalues[slot];
						int slotIndex = upvalue.slotIndex;
						if (slotIndex >= 0) stack[slotIndex] = peek(0);
						else upvalue.closed = peek(0);
						break;
					}
				case OP_GET_PROPERTY:
					{
						if (!IS_INSTANCE(peek(0)))
						{
							runtimeError("Only instances have properties.");
							return INTERPRET_RUNTIME_ERROR;
						}
						ObjInstance instance = AS_INSTANCE(peek(0));
						string name = READ_STRING();
						Value value;
						if (tableGet(instance.fields, name, out value))
						{
							pop(); // Instance.
							push(value);
							break;
						}
						if (!bindMethod(instance.klass, name))
						{
							return INTERPRET_RUNTIME_ERROR;
						}
						break; ;
					}
				case OP_SET_PROPERTY:
					{
						if (!IS_INSTANCE(peek(1)))
						{
							runtimeError("Only instances have fields.");
							return INTERPRET_RUNTIME_ERROR;
						}
						ObjInstance instance = AS_INSTANCE(peek(1));
						tableSet(instance.fields, READ_STRING(), peek(0));
						Value value = pop();
						pop();
						push(value);
						break;
					}
				case OP_GET_SUPER:
					{
						string name = READ_STRING();
						ObjClass superclass = AS_CLASS(pop());
						if (!bindMethod(superclass, name))
						{
							return INTERPRET_RUNTIME_ERROR;
						}
						break;
					}
				case OP_EQUAL:
					{
						Value b = pop();
						Value a = pop();
						push(BOOL_VAL(valuesEqual(a, b)));
						break;
					}
				case OP_GREATER: iresult = PopAndOp((a, b) => BOOL_VAL(a > b)); break;
				case OP_LESS: iresult = PopAndOp((a, b) => BOOL_VAL(a < b)); break;
				case OP_ADD:
					{
						if (IS_STRING(peek(0)) && IS_STRING(peek(1)))
						{
							concatenate();
						}
						else if (IS_NUMBER(peek(0)) && IS_NUMBER(peek(1)))
						{
							double b = AS_NUMBER(pop());
							double a = AS_NUMBER(pop());
							push(NUMBER_VAL(a + b));
						}
						else
						{
							runtimeError("Operands must be two numbers or two strings.");
							return INTERPRET_RUNTIME_ERROR;
						}
						break;
					}
				case OP_SUBTRACT: iresult = PopAndOp((a, b) => NUMBER_VAL(a - b)); break;
				case OP_MULTIPLY: iresult = PopAndOp((a, b) => NUMBER_VAL(a * b)); break;
				case OP_DIVIDE: iresult = PopAndOp((a, b) => NUMBER_VAL(a / b)); break;
				case OP_CALL:
					{
						int argCount = READ_BYTE();
						if (!callValue(peek(argCount), argCount))
						{
							return INTERPRET_RUNTIME_ERROR;
						}
						frame = frames[frameCount - 1];
						break;
					}
				case OP_INVOKE:
					{
						string method = READ_STRING();
						int argCount = READ_BYTE();
						if (!invoke(method, argCount))
						{
							return INTERPRET_RUNTIME_ERROR;
						}
						frame = frames[frameCount - 1];
						break;
					}
				case OP_CLOSURE:
					{
						ObjFunction function = AS_FUNCTION(READ_CONSTANT());
						ObjClosure closure = new ObjClosure(function);
						push(OBJ_VAL(closure));
						for (int i = 0; i < closure.upvalueCount; i++)
						{
							byte isLocal = READ_BYTE();
							byte index = READ_BYTE();
							if (isLocal != 0)
							{
								closure.upvalues[i] = captureUpvalue(frame.slotIndex + index);
							}
							else
							{
								closure.upvalues[i] = frame.closure!.upvalues[index];
							}
						}
						break;
					}
				case OP_SUPER_INVOKE:
					{
						string method = READ_STRING();
						int argCount = READ_BYTE();
						ObjClass superclass = AS_CLASS(pop());
						if (!invokeFromClass(superclass, method, argCount))
						{
							return INTERPRET_RUNTIME_ERROR;
						}
						frame = frames[frameCount - 1];
						break;
					}
				case OP_CLOSE_UPVALUE:
					closeUpvalues(stackTop - 1);
					pop();
					break;
				case OP_RETURN:
					{
						Value result = pop();
						closeUpvalues(frame.slotIndex);
						frameCount--;
						if (frameCount == 0)
						{
							pop();
							return INTERPRET_OK;
						}
						stackTop = frame.slotIndex;
						push(result);
						frame = frames[frameCount - 1];
						break;
					}
				case OP_CLASS:
					push(OBJ_VAL(new ObjClass(READ_STRING())));
					break;
				case OP_INHERIT:
					{
						Value superclass = peek(1);
						if (!IS_CLASS(superclass))
						{
							runtimeError("Superclass must be a class.");
							return INTERPRET_RUNTIME_ERROR;
						}
						ObjClass subclass = AS_CLASS(peek(0));
						tableAddAll(AS_CLASS(superclass).methods, subclass.methods);
						pop(); // Subclass.
						break;
					}
				case OP_METHOD:
					defineMethod(READ_STRING());
					break;
			}
			if (iresult != INTERPRET_OK) return iresult;
		}
	}

	void closeUpvalues(int last)
	{
		while (openUpvalues != null && openUpvalues.slotIndex >= last)
		{
			ObjUpvalue upvalue = openUpvalues;
			upvalue.closed = stack[upvalue.slotIndex];
			upvalue.slotIndex = -1;
			openUpvalues = upvalue.next;
		}
	}
	void defineMethod(string name)
	{
		Value method = peek(0);
		ObjClass klass = AS_CLASS(peek(1));
		tableSet(klass.methods, name, method);
		pop();
	}
	ObjUpvalue captureUpvalue(int local)
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
	bool callValue(Value callee, int argCount)
	{
		if (IS_OBJ(callee))
		{
			switch (OBJ_TYPE(callee))
			{
				case OBJ_BOUND_METHOD:
					{
						ObjBoundMethod bound = AS_BOUND_METHOD(callee);
						stack[stackTop - argCount - 1] = bound.receiver;
						return call(bound.method, argCount);
					}
				case OBJ_CLASS:
					{
						ObjClass klass = AS_CLASS(callee);
						stack[stackTop - argCount - 1] = OBJ_VAL(new ObjInstance(klass));
						Value initializer;
						if (tableGet(klass.methods, initString, out initializer))
						{
							return call(AS_CLOSURE(initializer), argCount);
						}
						else if (argCount != 0)
						{
							runtimeError($"Expected 0 arguments but got {argCount}.");
							return false;
						}
						return true;
					}
				case OBJ_CLOSURE:
					return call(AS_CLOSURE(callee), argCount);
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
						push(result);
						return true;
					}
				default:
					break; // Non-callable object type.
			}
		}
		runtimeError("Can only call functions and classes.");
		return false;
	}
	bool invokeFromClass(ObjClass klass, string name, int argCount)
	{
		Value method;
		if (!tableGet(klass.methods, name, out method))
		{
			runtimeError($"Undefined property '{name}'.");
			return false;
		}
		return call(AS_CLOSURE(method), argCount);
	}
	bool invoke(string name, int argCount)
	{
		Value receiver = peek(argCount);
		if (!IS_INSTANCE(receiver))
		{
			runtimeError("Only instances have methods.");
			return false;
		}
		ObjInstance instance = AS_INSTANCE(receiver);
		if (tableGet(instance.fields, name, out var value))
		{
			stack[stackTop - argCount - 1] = value;
			return callValue(value, argCount);
		}
		return invokeFromClass(instance.klass, name, argCount);
	}
	bool bindMethod(ObjClass klass, string name)
	{
		Value method;
		if (!tableGet(klass.methods, name, out method))
		{
			runtimeError($"Undefined property '{name}'.");
			return false;
		}
		ObjBoundMethod bound = new ObjBoundMethod(peek(0), AS_CLOSURE(method));
		pop();
		push(OBJ_VAL(bound));
		return true;
	}
	void defineNative(string name, NativeFn function)
	{
		var oname =name;
		var ofun = OBJ_VAL(new ObjNative(function));
		tableSet(globals, oname, ofun);
	}
	bool call(ObjClosure closure, int argCount)
	{
		var function = closure.function;
		if (argCount != function.arity)
		{
			runtimeError($"Expected {function.arity} arguments but got {argCount}.");
			return false;
		}
		if (frameCount >= FRAMES_MAX)
		{
			runtimeError("Stack overflow.");
			return false;
		}
		CreateFrame(closure, argCount + 1);
		return true;
	}

	void concatenate()
	{
		string b = AS_STRING(pop());
		string a = AS_STRING(pop());
		var result = a + b;
		push(OBJ_VAL(result));
	}

	InterpretResult PopAndOp(Func<double, double, Value> func)
	{
		if (!IS_NUMBER(peek(0)) || !IS_NUMBER(peek(1)))
		{
			runtimeError("Operands must be numbers.");
			return INTERPRET_RUNTIME_ERROR;
		}
		double b = AS_NUMBER(pop());
		double a = AS_NUMBER(pop());
		push(func(a, b));
		return INTERPRET_OK;
	}

	void runtimeError(string msg)
	{
		CallFrame frame = frames[frameCount - 1];
		var chunk = frame.closure!.function.chunk;
		int instruction = frame.ip - 1;
		int line = chunk.lines[instruction];
		var text = string.IsNullOrEmpty(chunk.FileName) ? msg : $"{chunk.FileName}({line}): {msg}";
		tw.WriteLine(text);
		Trace.WriteLine(text);
		if (DumpStackOnError) dumpStack();
		resetStack();
	}

	void dumpStack()
	{
		for (int i = frameCount - 1; i >= 0; i--)
		{
			CallFrame frame = frames[i];
			ObjFunction function = frame.closure!.function;
			int instruction = frame.ip - 1;
			tw.WriteLine($"[line {function.chunk.lines[instruction]}] in {function.NameOrScript}");
		}
	}

	void resetStack()
	{
		stackTop = 0;
	}
}


