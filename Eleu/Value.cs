global using static Eleu.ValueType;
global using static Eleu.ValueStatics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Eleu;

enum ValueType
{
	VAL_NIL, // must go first to ensure, new Values are nil
	VAL_BOOL,
	VAL_NUMBER,
	VAL_STRING,
	VAL_LIST,  // List<Value>
	VAL_OBJ
}
struct Value
{
	static object DummyObject = new ();
	
	public ValueType type;
	public double dValue;
	public object oValue;

	public Value(ValueType type, object oValue)
	{
		this.type = type;
		this.dValue = double.NaN;
		this.oValue = oValue;
	}

	public Value(ValueType type, double dValue)
	{
		this.type = type;
		this.dValue = dValue;
		this.oValue = DummyObject;
	}

	public Value(string s)
	{
		this.type = VAL_STRING;
		this.oValue = s;
		this.dValue = double.NaN;
	}

	public override string ToString()
	{
		switch (type)
		{
			case VAL_NIL: return "nil";
			case VAL_BOOL: return dValue != 0 ? "true" : "false";
			case VAL_NUMBER: return dValue.ToString(CultureInfo.InvariantCulture);
			case VAL_OBJ: 
			case VAL_STRING: return oValue.ToString()!;
			default: return $"invalid value type {type}";
		}
	}
}

static class ValueStatics
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Value CreateBoolVal(bool value) => new(VAL_BOOL, value ? 1 : 0);
	public static Value BoolTrue = CreateBoolVal(true);
	public static Value BoolFalse = CreateBoolVal(false);
	public static readonly Value Nil = new(VAL_NIL, 0);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Value CreateNumberVal(double value) => new(VAL_NUMBER, value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Value CreateObjVal(Obj value) => new(VAL_OBJ, value);
	public static Value CreateStringVal(string value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Obj AsObj(Value value) => (Obj) value.oValue;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool AsBool( Value value) => value.dValue != 0;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] 
	public static double AsNumber( Value value) => value.dValue;

	public static bool IsBool( Value value) => value.type == VAL_BOOL;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] 
	public static bool IsNil( Value value) => value.type == VAL_NIL;
	public static bool IsNumber(Value value) => value.type == VAL_NUMBER;
	public static bool IsObj(Value value) => value.type == VAL_OBJ;
	public static bool IsFalsey(Value value) => IsNil(value) || (IsBool(value) && !AsBool(value));
	public static bool ValuesEqual(Value a, Value b)
	{
		if (a.type != b.type) return false;
		switch (a.type)
		{
			case VAL_BOOL: return AsBool(a) == AsBool(b);
			case VAL_NIL: return true;
			case VAL_NUMBER: return AsNumber(a) == AsNumber(b);
			case VAL_OBJ:
				{
					return AsObj(a).Equals(AsObj(b));
				}
			case VAL_STRING: return  a.oValue.Equals(b.oValue);
			default: return false; // Unreachable.
		}
	}
}

