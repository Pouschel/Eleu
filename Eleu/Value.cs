global using static CsLox.ValueType;
global using static CsLox.ValueStatics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CsLox;

enum ValueType
{
	VAL_NIL, // must go first to ensure, new Values are nil
	VAL_BOOL,
	VAL_NUMBER,
	VAL_STRING,
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
	public static Value BOOL_VAL(bool value) => new(VAL_BOOL, value ? 1 : 0);
	public static Value BOOL_TRUE = BOOL_VAL(true);
	public static readonly Value NIL_VAL = new(VAL_NIL, 0);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Value NUMBER_VAL(double value) => new(VAL_NUMBER, value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Value OBJ_VAL(Obj value) => new(VAL_OBJ, value);
	public static Value OBJ_VAL(string value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Obj AS_OBJ(Value value) => (Obj) value.oValue;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool AS_BOOL( Value value) => value.dValue != 0;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] 
	public static double AS_NUMBER( Value value) => value.dValue;

	public static bool IS_BOOL( Value value) => value.type == VAL_BOOL;
	[MethodImpl(MethodImplOptions.AggressiveInlining)] 
	public static bool IS_NIL( Value value) => value.type == VAL_NIL;
	public static bool IS_NUMBER(Value value) => value.type == VAL_NUMBER;
	public static bool IS_OBJ(Value value) => value.type == VAL_OBJ;
	public static bool isFalsey(Value value) => IS_NIL(value) || (IS_BOOL(value) && !AS_BOOL(value));
	public static bool valuesEqual(Value a, Value b)
	{
		if (a.type != b.type) return false;
		switch (a.type)
		{
			case VAL_BOOL: return AS_BOOL(a) == AS_BOOL(b);
			case VAL_NIL: return true;
			case VAL_NUMBER: return AS_NUMBER(a) == AS_NUMBER(b);
			case VAL_OBJ:
				{
					return AS_OBJ(a).Equals(AS_OBJ(b));
				}
			case VAL_STRING: return  a.oValue.Equals(b.oValue);
			default: return false; // Unreachable.
		}
	}
}

class ValueArray : List<Value>
{
	public void write(Value value) => Add(value);

}
