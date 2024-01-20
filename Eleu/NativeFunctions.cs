using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Eleu.Interpret;
using Eleu.Types;

namespace Eleu;
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE1006 // Naming Styles

public class EleuNativeError : EleuRuntimeError
{
	public EleuNativeError(string msg) : base(msg)
	{	}
}

public class NativeFunctionBase
{
	private Interpreter? vm;
	protected Interpreter Vm => vm!;

	public static void CheckArgLen(object[] args, int nMinArgs, int nMaxArgs = -1, [CallerMemberName] string name = "")
	{
		if (name[0] == '@') name = name[1..];
		if (nMaxArgs < 0)
			if (args.Length != nMinArgs)
				throw new EleuNativeError($"Die Funktion '{name}' erwartet genau {nMinArgs} Argumente.");
			else return;
		if (args.Length < nMinArgs || args.Length > nMaxArgs)
			throw new EleuNativeError($"Die Funktion '{name}' erwartet mindestens {nMinArgs} und höchstens {nMaxArgs} Argumente");
	}

	public static T CheckArgType<T>(int zeroIndex, object[] args, [CallerMemberName] string funcName = "")
	{
		if (args.Length > zeroIndex)
		{
			var type = typeof(T);
			var arg = args[zeroIndex];
			if (arg.GetType() == type) return (T)arg;
			if (arg is T t) return t;
			string tn = "";
			if (type == typeof(Number)) tn = "number";
			if (type == typeof(string)) tn = "string";
			if (type == typeof(bool)) tn = "boolean";
			
			throw new EleuNativeError($"In der Funktion {funcName} muss das {zeroIndex + 1}. Argument vom Typ '{tn}' sein.");
		}
		else
			throw new EleuNativeError($"In der Funktion {funcName} muss das {zeroIndex + 1}. Argument vorhanden sein!");
	}

	public static int CheckIntArg(int zeroIndex, object[] args)
	{
		if (args.Length > zeroIndex)
		{
			var arg = args[zeroIndex];
			if (arg is Number d)
			{
				if (d.IsInt) return d.IntValue;
			}
		}
		throw new EleuNativeError($"Argument {zeroIndex + 1} muss eine ganze Zahl sein.");
	}
	public IEnumerable<(string name, MethodInfo method)> GetFunctions()
	{
		var type = this.GetType();
		var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		foreach (var mi in type.GetMethods(flags))
		{
			if (mi.ReturnType != typeof(object)) continue;
			var pars = mi.GetParameters();
			if (pars.Length != 1) continue;
			if (pars[0].ParameterType != typeof(object[])) continue;
			string name = mi.Name;
			if (name[0] == '@')
				name = name[1..];
			yield return (name, mi);
		}
	}

  public static void DefineAll<T>(T funcClass, Interpreter vm) where T : NativeFunctionBase, new()
	{
		funcClass.vm = vm;
		foreach (var (name, method) in funcClass.GetFunctions())
		{
			if (method.IsStatic)
				vm.DefineNative(name, (NativeFn)Delegate.CreateDelegate(typeof(NativeFn), method));
			else
				vm.DefineNative(name, (NativeFn)Delegate.CreateDelegate(typeof(NativeFn), funcClass, method));
		}
	}
}

public class NativeFunctions : NativeFunctionBase
{
	Random rand = new();

	static Number MathFunc(Func<double, double> func, object[] args,
	[CallerMemberName] string name = "")
	{
		CheckArgLen(args, 1, -1, name);
		var arg = CheckArgType<Number>(0, args, name);
		var result = func(arg.DVal);
		if (double.IsInfinity(result))
			throw new EleuNativeError($"Das Ergebnis von '{name}({arg})' ist zu groß (oder klein) für den unterstützten Zahlentyp.");
		if (!double.IsFinite(result))
			throw new EleuNativeError($"Das Ergebnis von '{name}({arg})' ist nicht definiert.");
		return new(result);
	}
	private object clock(object[] _)
	{
		return new Number(DateTime.Now.Ticks / 10000000.0);
	}
	private static object sqrt(object[] args) => MathFunc(Math.Sqrt, args);
	private static object abs(object[] args) => MathFunc(Math.Abs, args);
	private static object acos(object[] args) => MathFunc(Math.Acos, args);
	private static object asin(object[] args) => MathFunc(Math.Asin, args);
	private static object ceil(object[] args) => MathFunc(Math.Ceiling, args);
	private static object cos(object[] args) => MathFunc(Math.Cos, args);
	private static object floor(object[] args) => MathFunc(Math.Floor, args);
	private static object log10(object[] args) => MathFunc(Math.Log10, args);
	private static object sin(object[] args) => MathFunc(Math.Sin, args);
	private static object pow(object[] args)
	{
		CheckArgLen(args, 2);
		var bas = CheckArgType<Number>(0, args);
		var exp = CheckArgType<Number>(1, args);
		var result = Math.Pow(bas.DVal, exp.DVal);
		if (double.IsInfinity(result))
			throw new EleuNativeError($"Das Ergebnis von 'pow({bas},{exp})' ist zu groß (oder klein) für den unterstützten Zahlentyp.");
		if (!double.IsFinite(result))
			throw new EleuNativeError($"Das Ergebnis von 'pow({bas},{exp})' ist nicht definiert.");
		return new Number(result);
	}
	private object random(object[] args)
	{
		CheckArgLen(args, 0);
		return new Number(rand.NextDouble());
	}

	public static object @typeof(object[] args)
	{
		CheckArgLen(args, 1);
		var arg = args[0];
		return arg switch
		{
			bool => "boolean",
			Number => "number",
			string => "string",
			EleuClass cl => "metaclass " + cl.Name,
			EleuInstance inst => $"class {inst.klass.Name}",
			ICallable => "function",
			_ => "undefined"
		};
	}
	private static object toString(object[] args)
	{
		CheckArgLen(args, 1);
		return Stringify(args[0]);
	}

	private object print(object[] args)
	{
		CheckArgLen(args, 1);
		var s = Stringify(args[0]);
		Vm.options.Out.WriteLine(s);
		return NilValue;
	}
  private static object toFixed(object[] args)
  {
		CheckArgLen(args, 2);
		var x = CheckArgType<Number>(0, args);
		var n = CheckIntArg(1, args);
		if (n < 0 || n > 20)
			throw new EleuNativeError("Die Anzahl der Nachkommastellen muss eine ganze Zahl zwischen 0 und 20 sein.");
		return x.DVal.ToString($"f{n}", CultureInfo.InvariantCulture);
	}
	private static object parseInt(object[] args)
	{
		CheckArgLen(args, 1);
		var s = CheckArgType<string>(0, args);
		var num = Number.TryParse(s);
		if (!num.HasValue) throw new EleuNativeError($"Die Zeichenkette '{s}' kann nicht in eine Zahl umgewandelt werden.");
		if (!double.IsInteger(num.Value.DVal)) throw new EleuNativeError($"Die Zeichenkette '{s}' kann nicht in ganze eine Zahl umgewandelt werden.");
		return num.Value;
	}
	private static object parseFloat(object[] args) => parseNumber(args);
	private static object parseNum(object[] args) => parseNumber(args);
	private static object parseNumber(object[] args)
	{
		CheckArgLen(args, 1);
		var s = CheckArgType<string>(0, args);
		var num = Number.TryParse(s);
		if (!num.HasValue) throw new EleuNativeError($"Die Zeichenkette '{s}' kann nicht in eine Zahl umgewandelt werden.");
		return num.Value;
	}

	private static object len(object[] args)
	{
		CheckArgLen(args, 1);
		var s = CheckArgType<string>(0, args);
		return new Number(s.Length);
	}
	private static object charAt(object[] args)
	{
		CheckArgLen(args, 2);
		var s = CheckArgType<string>(0, args);
		var idx = CheckIntArg(1, args);
		if (idx < 0 || idx >= s.Length)
			throw new EleuNativeError($"Der Index {idx} liegt außerhalb des Strings");
		return s[idx].ToString();
	}

	private static object substr(object[] args)
	{
		CheckArgLen(args, 2, 3);
		var s = CheckArgType<string>(0, args);
		int idx = CheckIntArg(1, args);
		int len = s.Length - idx;
		if (args.Length >= 3)
			len = CheckIntArg(2, args);
		try
		{
			return s.Substring(idx, len);
		}
		catch (ArgumentOutOfRangeException ex)
		{
			throw new EleuNativeError(ex.Message);
		}
	}
	private static object indexOf(object[] args)
	{
		CheckArgLen(args, 2, 3);
		var s = CheckArgType<string>(0, args);
		var such = CheckArgType<string>(1, args);
		int idx = 0;
		if (args.Length >= 3)
			idx = CheckIntArg(2, args);
		try
		{
			return new Number(s.IndexOf(such, idx));
		}
		catch (ArgumentOutOfRangeException ex)
		{
			throw new EleuNativeError(ex.Message);
		}
	}
	private static object lastIndexOf(object[] args)
	{
		CheckArgLen(args, 2, 3);
		var s = CheckArgType<string>(0, args);
		var such = CheckArgType<string>(1, args);
		int idx = s.Length;
		if (args.Length >= 3)
			idx = CheckIntArg(2, args);
		try
		{
			return new Number(s.LastIndexOf(such, idx));
		}
		catch (ArgumentOutOfRangeException )
		{
			throw new EleuNativeError($"lastIndexOf: {idx} liegt außerhalb des zulässigen Bereichs.");
		}
	}
	private static object toLowerCase(object[] args)
	{
		CheckArgLen(args, 1);
		var s = CheckArgType<string>(0, args);
		return s.ToLower();
	}
	private static object toUpperCase(object[] args)
	{
		CheckArgLen(args, 1);
		var s = CheckArgType<string>(0, args);
		return s.ToUpper();
	}
	object _catch(object[] args)
	{
		CheckArgLen(args, 1, 2);
		var func = CheckArgType<ICallable>(0, args);
		var s = "";
		if (args.Length >= 2) s = CheckArgType<string>(1, args);
		if (func.Arity > 0)
			throw new EleuNativeError("Die übergebene Funktion darf keine Argumente haben.");
		try
		{
			func.Call((Interpreter) Vm!, Array.Empty<object>());
		} catch( EleuRuntimeError ex) {
			var msg = ex.Message;
			if (!string.IsNullOrEmpty(s) && s != msg)
				throw new EleuNativeError($"Wrong runtime error! Got: {msg} Expected: {s}");
			return true;
		}
		// no exceptione
		throw new EleuNativeError("Runtime errror expected: $s");
	}
}
