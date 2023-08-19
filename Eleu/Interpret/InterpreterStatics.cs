global using static Eleu.Interpret.InterpreterStatics;
using System.Globalization;
using Eleu.Types;

namespace Eleu.Interpret;

public static class InterpreterStatics
{
	public static readonly object NilValue = NilType.Nil;
	public static bool ObjEquals(object a, object b)
	{
		if (ReferenceEquals(a, b)) return true;
		if (a is Number da && b is Number db) return da.Equals(db);
		if (a.Equals(b)) return true;
		return false;
	}
	public static int InternalCompare(object a, object b)
	{
		if (ObjEquals(a, b)) return 0;
		if (a is string sa && b is string sb) return string.CompareOrdinal(sa, sb);
		if (a is Number na && b is Number nb) return na.CompareTo(nb);
		throw new EleuRuntimeError("Unterschiedliche Datentypen können nicht verglichen werden.");
	}
	internal static Number NumberOp(string op, object va, object vb, Func<double, double, double> func)
	{
		if (va is not Number a || vb is not Number b)
		{
			throw new EleuRuntimeError("Beide Operanden müssen Zahlen sein.");
		}
		var result = func(a.DVal, b.DVal);
		if (double.IsInfinity(result))
			throw new EleuNativeError($"Das Ergebnis von '{a} {op} {b}' ist zu groß (oder klein) für den unterstützten Zahlentyp.");
		if (!double.IsFinite(result))
			throw new EleuNativeError($"Das Ergebnis von '{a} {op} {b}' ist nicht definiert.");
		return new(result);
	}
	public static Number NumSubtract(object lhs, object rhs) => NumberOp("-", lhs, rhs, (a, b) => a - b);
	public static object NumStrAdd(object a0, object a1)
	{
		var a0Num = a0 is Number;
		var a1Num = a1 is Number;
		if (a0Num && a1Num)
		{
			Number a = (Number)a0;
			Number b = (Number)a1;
			return a + b;
		}
		var s0 = a0 is string as0 ? as0 : (a0Num ? ((Number)a0).ToString() : null);
		var s1 = a1 is string as1 ? as1 : (a1Num ? ((Number)a1).ToString() : null);
		if (s0 == null || s1 == null)
			throw new EleuRuntimeError($"Die Operation '{Stringify(a0, true)} + {Stringify(a1, true)}' ist ungültig.");
		return s0 + s1;
	}

	public static bool IsFalsey(object value) => !IsTruthy(value);
	public static bool IsTruthy(object value)
	{
		if (value is not bool b)
			throw new EleuRuntimeError($"{value} ist nicht vom typ boolean");
		return b;
	}

	public static string Stringify(object val, bool quotationMarks = false)
	{
		var s = val switch
		{
			bool b => b ? "true" : "false",
			object o when o == NilValue => "nil",
			double d => d.ToString(CultureInfo.InvariantCulture),
			_ => val.ToString() ?? "",
		};
		if (quotationMarks && val is string)
			s = "\"" + s + "\"";
		return s;
	}
}

