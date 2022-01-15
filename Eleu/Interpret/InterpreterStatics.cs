global using static Eleu.Interpret.InterpreterStatics;
using System.Globalization;
namespace Eleu.Interpret;

static class InterpreterStatics
{
	public static object Nil = new object();

	public static bool ObjEquals(object a, object b)
	{
		if (a == b) return true;
		if (a.Equals(b)) return true;
		return false;
	}

	public static int InternalCompare(object a, object b)
	{
		if (ObjEquals(a, b)) return 0;
		var cmp = NumberOp(a, b, (a, b) => a.CompareTo(b));
		return cmp;
	}

	internal static Res NumberOp<Res>(object va, object vb, Func<double, double, Res> func)
	{
		if (va is not double a || vb is not double b)
		{
			throw new EleuRuntimeError("Operands must be numbers.");
		}
		return func(a, b);
	}

	public static object NumStrOp(object a0, object a1, Func<double, double, double> nFunc,
	Func<string, string, string> sFunc)
	{
		var a0Num = a0 is double;
		var a1Num = a1 is double;
		if (a0Num && a1Num)
		{
			double a = (double)a0;
			double b = (double)a1;
			return nFunc(a, b);
		}
		var s0 = a0 is string as0 ? as0 : (a0Num ? ((double)a0).ToString(CultureInfo.InvariantCulture) : null);
		var s1 = a1 is string as1 ? as1 : (a1Num ? ((double)a1).ToString(CultureInfo.InvariantCulture) : null);
		if (s0 == null || s1 == null)
			throw new EleuRuntimeError("Operands must be two numbers or two strings.");
		return sFunc(s0, s1);
	}

	public static bool IsFalsey(object value) => value == Nil || value is bool b && !b;
	public static bool IsTruthy(object value) => !IsFalsey(value);
}

