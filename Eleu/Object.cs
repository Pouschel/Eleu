global using static Eleu.ObjType;
global using static Eleu.ObjStatics;
using System.Runtime.CompilerServices;
using System.Text;
using Eleu.Interpret;

namespace Eleu;

public enum ObjType
{
	OBJ_BOUND_METHOD,
	OBJ_CLASS,
	OBJ_CLOSURE,
	OBJ_FUNCTION,
	OBJ_INSTANCE,
	OBJ_NATIVE,
	OBJ_UPVALUE,
}

public class Obj
{
	public readonly ObjType type;

	public Obj(ObjType type)
	{
		this.type = type;
	}
}

internal class ObjFunction : Obj
{
	public int arity;
	public readonly Chunk chunk;
	public int upvalueCount;
	public string name;

	public ObjFunction() : base(OBJ_FUNCTION)
	{
		this.arity = 0;
		this.name = "";
		this.chunk = new Chunk();
	}
	public override string ToString() => $"<fn {NameOrScript}>";

	public string NameOrScript
	{
		get
		{
			var s = name;
			if (string.IsNullOrEmpty(s)) return "<script>";
			return s;
		}
	}
}

public delegate Value NativeFn(Value[] args);

internal class ObjNative : Obj, LoxCallable 
{
	public readonly NativeFn function;

	public ObjNative(NativeFn function) : base(OBJ_NATIVE)
	{
		this.function = function;
	}

	public int arity() => function.Method.GetParameters().Length-1;
	public Value Call(Interpreter interpreter, Value[] arguments) => this.function.Invoke(arguments);

	public override string ToString() => "<native fn>";

}

class ObjUpvalue : Obj
{
	public int slotIndex; // -1 when closed
	public Value closed;  // value when closed
	public ObjUpvalue? next;

	public ObjUpvalue(int local) : base(OBJ_UPVALUE)
	{
		this.slotIndex = local;
		this.closed = NilValue;
		this.next = null;
	}
	public override string ToString() => "upvalue";
}

class ObjClosure : Obj
{
	public ObjFunction function;
	public ObjUpvalue[] upvalues;
	public int upvalueCount;
	public ObjClosure(ObjFunction function) : base(OBJ_CLOSURE)
	{
		this.function = function;
		this.upvalueCount = function.upvalueCount;
		upvalues = new ObjUpvalue[function.upvalueCount];
	}

	public override string ToString() => function.ToString();

}

class ObjClass : Obj
{
	public string name;
	public Table methods;

	public ObjClass(string name) : base(OBJ_CLASS)
	{
		this.name = name;
		this.methods = new Table();
	}



	public override string ToString() => name;

}


class ObjInstance : Obj
{
	public ObjClass klass;
	public Table fields;

	public ObjInstance(ObjClass klass) : base(OBJ_INSTANCE)
	{
		this.klass = klass;
		fields = new Table();
	}

	public override string ToString() => $"{klass.name} instance";

}

class ObjBoundMethod : Obj
{
	public Value receiver;
	public ObjClosure method;

	public ObjBoundMethod(Value receiver, ObjClosure method) : base(OBJ_BOUND_METHOD)
	{
		this.receiver = receiver;
		this.method = method;
	}

	public override string ToString() => method.function.ToString();
}



class ValList: List<Value>
{
	
	public ValList(int initLen): base(initLen)
	{
	}

	public override string ToString()
	{
		var sb = new StringBuilder();
		sb.Append('[');
		for (int i = 0; i < Count; i++)
		{
			if (sb.Length>200)
			{
				sb.Append("..."); break;
			}
			if (i > 0) sb.Append(',');
			sb.Append(this[i].ToString());
		}
		sb.Append(']');
		return sb.ToString();
	}
}

static class ObjStatics
{
	public static ObjType OBJ_TYPE(Value value) => AsObj(value).type;
	public static bool IS_BOUND_METHOD(Value value) => IsObjType(value, OBJ_BOUND_METHOD);
	public static bool IsClass(Value value) => IsObjType(value, OBJ_CLASS);
	public static bool IS_CLOSURE(Value value) => IsObjType(value, OBJ_CLOSURE);
	public static bool IS_FUNCTION(Value value) => IsObjType(value, OBJ_FUNCTION);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsInstance(Value value) => value.oValue is ObjInstance;
	public static bool IS_NATIVE(Value value) => value.oValue is ObjNative;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsString(Value value) => value.type == VAL_STRING;

	public static bool IsList(Value value) => value.type == VAL_LIST;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string AsString(Value value) => ((string)value.oValue);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ObjClosure AS_CLOSURE(Value value) => ((ObjClosure)AsObj(value));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ObjBoundMethod AS_BOUND_METHOD(Value value) => ((ObjBoundMethod)AsObj(value));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ObjClass AsClass(Value value) => (ObjClass)AsObj(value);
	public static ValList AsList(Value value) => (ValList)value.oValue;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ObjFunction AS_FUNCTION(Value value) => ((ObjFunction)AsObj(value));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ObjInstance AS_INSTANCE(Value value) => ((ObjInstance)AsObj(value));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static NativeFn AS_NATIVE(Value value) => ((ObjNative)AsObj(value)).function;


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool IsObjType(Value value, ObjType type)
	{
		return IsObj(value) && AsObj(value).type == type;
	}

}