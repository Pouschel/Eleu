global using static CsLox.ObjType;
global using static CsLox.ObjStatics;
using System.Runtime.CompilerServices;

namespace CsLox;

enum ObjType
{
	OBJ_BOUND_METHOD,
	OBJ_CLASS,
	OBJ_CLOSURE,
	OBJ_FUNCTION,
	OBJ_INSTANCE,
	OBJ_NATIVE,
	OBJ_UPVALUE,
}

internal class Obj
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

	public ObjFunction():base(OBJ_FUNCTION)
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

delegate Value NativeFn(Value[] args);

internal class ObjNative : Obj
{
	public readonly NativeFn function;

	public ObjNative(NativeFn function): base(OBJ_NATIVE)
	{
		this.function = function;
	}

	public override string ToString() => "<native fn>";

}

class ObjUpvalue : Obj
{
	public int slotIndex; // -1 when closed
	public Value closed;  // value when closed
	public ObjUpvalue? next;

	public ObjUpvalue(int local): base(OBJ_UPVALUE)
	{
		this.slotIndex = local;
		this.closed = Nil;
		this.next = null;
	}
	public override string ToString() => "upvalue";
}

class ObjClosure : Obj
{
	public ObjFunction function;
	public ObjUpvalue[] upvalues;
	public int upvalueCount;
	public ObjClosure(ObjFunction function): base(OBJ_CLOSURE)
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

	public ObjClass(string name): base (OBJ_CLASS)
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

	public ObjInstance(ObjClass klass): base(OBJ_INSTANCE)
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

	public ObjBoundMethod(Value receiver, ObjClosure method): base(OBJ_BOUND_METHOD)
	{
		this.receiver = receiver;
		this.method = method;
	}

	public override string ToString() => method.function.ToString();
}

static class ObjStatics
{
	public static ObjType OBJ_TYPE(Value value) => AsObj(value).type;
	public static bool IS_BOUND_METHOD(Value value) => IsObjType(value, OBJ_BOUND_METHOD);
	public static bool IS_CLASS(Value value) => IsObjType(value, OBJ_CLASS);
	public static bool IS_CLOSURE(Value value) => IsObjType(value, OBJ_CLOSURE);
	public static bool IS_FUNCTION(Value value) => IsObjType(value, OBJ_FUNCTION);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IS_INSTANCE(Value value) => IsObjType(value, OBJ_INSTANCE);
	public static bool IS_NATIVE(Value value) => IsObjType(value, OBJ_NATIVE);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IS_STRING(Value value) => value.type==VAL_STRING;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string AS_STRING(Value value) => ((string)value.oValue);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ObjClosure AS_CLOSURE(Value value) => ((ObjClosure)AsObj(value));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ObjBoundMethod AS_BOUND_METHOD(Value value) => ((ObjBoundMethod)AsObj(value));
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ObjClass AS_CLASS(Value value) => ((ObjClass)AsObj(value));
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