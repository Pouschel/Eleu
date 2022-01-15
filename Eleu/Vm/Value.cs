global using static Eleu.ValueType;
global using static Eleu.ValueStatics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable 660, 661
namespace Eleu;

public enum ValueType
{
	VAL_NIL, // must go first to ensure, new Values are nil
	VAL_BOOL,
	VAL_NUMBER,
	VAL_STRING,
	VAL_LIST,  // List<Value>
	VAL_OBJ
}

public class EleuRuntimeError : Exception
{
	public EleuRuntimeError(string msg) : base(msg)
	{
	}

}

public struct Value
{
	static object DummyObject = new();

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

	internal Value(ValList list)
	{
		this.type = VAL_LIST;
		this.oValue = list;
		this.dValue = double.NaN;
	}

	public static Value operator !(Value val) => CreateBoolVal(IsFalsey(val));
	public static Value operator -(Value val)
	{
		if (!IsNumber(val))
		{
			throw new EleuRuntimeError("Operand must be a number.");
		}
		return CreateNumberVal(-AsNumber(val));
	}
	public static Value operator -(Value a, Value b) => NumberOp(a, b, (a, b) => CreateNumberVal(a - b));
	public static Value operator *(Value a, Value b) => NumberOp(a, b, (a, b) => CreateNumberVal(a * b));
	public static Value operator /(Value a, Value b) => NumberOp(a, b, (a, b) => CreateNumberVal(a / b));
	public static Value operator %(Value a, Value b) => NumberOp(a, b, (a, b) => CreateNumberVal(a % b));
	public static Value operator +(Value a, Value b) =>
		NumStrOp(a, b, (a, b) => CreateNumberVal(a + b), (a, b) => CreateStringVal(a + b));
		
	public static Value operator ==(Value a, Value b) => CreateBoolVal(ValuesEqual(a, b));
	public static Value operator !=(Value a, Value b) => !CreateBoolVal(ValuesEqual(a, b));

	public static Value operator <(Value a, Value b) => CreateBoolVal(InternalCompare(a, b) < 0);
	public static Value operator >(Value a, Value b) => CreateBoolVal(InternalCompare(a, b) > 0);
	public static Value operator <=(Value a, Value b) => CreateBoolVal(InternalCompare(a, b) <= 0);
	public static Value operator >=(Value a, Value b) => CreateBoolVal(InternalCompare(a, b) >= 0);

	static int InternalCompare(Value a, Value b)
	{
		if (ValuesEqual(a, b)) return 0;
		var cmp = NumberOp(a, b, (a, b) => CreateNumberVal(a.CompareTo(b))); 
			//(a, b) => CreateNumberVal(a.CompareTo(b)));
		return (int)cmp.dValue;
	}

	static Value NumberOp(in Value va, in Value vb, Func<double, double, Value> func)
	{
		if (!IsNumber(va) || !IsNumber(vb))
		{
			throw new EleuRuntimeError("Operands must be numbers.");
		}
		double b = AsNumber(vb);
		double a = AsNumber(va);
		return func(a, b);
	}

	static Value NumStrOp(Value a0, Value a1, Func<double, double, Value> nFunc,
		Func<string, string, Value> sFunc)
	{
		var a0Num = IsNumber(a0);
		var a1Num = IsNumber(a1);
		if (a0Num && a1Num)
		{
			double a = AsNumber(a0);
			double b = AsNumber(a1);
			return nFunc(a, b);
		}
		var s0 = IsString(a0) ? AsString(a0) : (a0Num ? AsNumber(a0).ToString(CultureInfo.InvariantCulture) : null);
		var s1 = IsString(a1) ? AsString(a1) : (a1Num ? AsNumber(a1).ToString(CultureInfo.InvariantCulture) : null);
		if (s0 == null || s1 == null)
			throw new EleuRuntimeError("Operands must be two numbers or two strings.");

		return sFunc(s0, s1);
	}

	public override string ToString()
	{
		switch (type)
		{
			case VAL_NIL: return "nil";
			case VAL_BOOL: return dValue != 0 ? "true" : "false";
			case VAL_NUMBER: return dValue.ToString(CultureInfo.InvariantCulture);
			case VAL_OBJ:
			case VAL_LIST:
			case VAL_STRING: return oValue.ToString()!;
			default: return $"invalid value type {type}";
		}
	}
}

public static class ValueStatics
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Value CreateBoolVal(bool value) => value ? BoolTrue : BoolFalse;
	public static readonly Value BoolTrue = new(VAL_BOOL, 1);
	public static readonly Value BoolFalse = new(VAL_BOOL, 0);
	public static readonly Value NilValue = new(VAL_NIL, 0);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Value CreateNumberVal(double value) => new(VAL_NUMBER, value);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Value CreateObjVal(Obj value) => new(VAL_OBJ, value);
	public static Value CreateStringVal(string value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Obj AsObj(Value value) => (Obj)value.oValue;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool AsBool(Value value) => value.dValue != 0;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double AsNumber(Value value) => value.dValue;

	public static bool IsBool(Value value) => value.type == VAL_BOOL;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNil(Value value) => value.type == VAL_NIL;
	public static bool IsNumber(Value value) => value.type == VAL_NUMBER;
	public static bool IsObj(Value value) => value.type == VAL_OBJ;
	public static bool IsFalsey(Value value) => IsNil(value) || (IsBool(value) && !AsBool(value));
	public static bool IsTruthy(Value value) => !IsFalsey(value);

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
			case VAL_STRING: return a.oValue.Equals(b.oValue);
			default: return false; // Unreachable.
		}
	}
}

