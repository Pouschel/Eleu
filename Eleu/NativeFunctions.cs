using System.Reflection;

namespace Eleu;

class EleuNativeError : EleuRuntimeError
{
	public EleuNativeError(string msg) : base(msg)
	{

	}
}

class NativeFunctions
{


	static void CheckArgLen(object[] args, int nExpected)
	{
		if (args.Length == nExpected) return;
		throw new EleuNativeError($"{nExpected} Argumente erwartet. Funktion wurde aber mit {args.Length} aufgerufen.");
	}

	static T CheckArgType<T>(int nr, object arg)
	{
		var type = typeof(T);
		if (arg.GetType() == type) return (T)arg;
		string tn = "";
		if (type == typeof(double)) tn = "Zahl";
		if (type == typeof(string)) tn = "Zeichenkette";
		if (type == typeof(bool)) tn = "Bool";
		throw new EleuNativeError($"Argument {nr} muss vom Typ {tn} sein!");
	}

	static object MathFunc(Func<double, double> func, object[] args)
	{
		CheckArgLen(args, 1);
		var arg = CheckArgType<double>(1, args[0]);
		return func(arg);
	}

	static object clock(object[] _)
	{
		return DateTime.Now.Ticks / 10000000.0;
	}

	static object sqrt(object[] args) => MathFunc(Math.Sqrt, args);

	public static void DefineAll(IInterpreter vm)
	{
		var type = typeof(NativeFunctions);
		foreach (var mi in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic ))
		{
			if (mi.ReturnType != typeof(object)) continue;
			var pars = mi.GetParameters();
			if (pars.Length != 1) continue;
			if (pars[0].ParameterType != typeof(object[])) continue;
			vm.DefineNative(mi.Name, (NativeFn)Delegate.CreateDelegate(typeof(NativeFn), mi));
		}
	}
}
