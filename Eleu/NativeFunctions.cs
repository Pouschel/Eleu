using System.Reflection;

namespace Eleu;

class NativeException : Exception
{
	public NativeException(string msg) : base(msg)
	{

	}
}

class NativeFunctions
{
	Dictionary<string, Type> typeDict = new Dictionary<string, Type>();
	IInterpreter vm;

	public NativeFunctions(IInterpreter vm)
	{
		this.vm = vm;
		vm.DefineNative("clock", Clock);
		vm.DefineNative("invoke", Invoke);
		vm.DefineNative("len", Len);
	}

	Value RuntimeErrorWithNil(string msg)
	{
		vm.RuntimeError(msg);
		return Nil;
	}

	public Value Clock(Value[] _)
	{
		return CreateNumberVal(DateTime.Now.Ticks / 10000000.0);
	}

	public Value Len(Value[] args)
	{
		if (args.Length != 1) return RuntimeErrorWithNil("len must have 1 argument");
		var arg = args[0];
		if (IsString(arg)) return CreateNumberVal(AsString(arg).Length);
		if (IsList(arg)) return CreateNumberVal(AsList(arg).Count);
		return RuntimeErrorWithNil($"{arg} has no length");
	}

	static Type FindType(string name)
	{
		foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
		{
			foreach (Type t in a.GetTypes())
			{
				if (t.FullName == name)
					return t;
			}
		}
		throw new NativeException($"class [{name}] not found");
	}

	static bool IsNumberTypeCode(TypeCode tcode)
	{
		switch (tcode)
		{
			case TypeCode.Double:
			case TypeCode.Single:
			case TypeCode.Byte:
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Int64:
			case TypeCode.UInt16:
			case TypeCode.UInt32:
			case TypeCode.UInt64:
				return true;
		}
		return false;
	}

	object? MatchParameter(Value value, Type? paraType)
	{
		if (paraType == null) return null;
		if (IsNumber(value))
		{
			var num = AsNumber(value);
			if (!IsNumberTypeCode(Type.GetTypeCode(paraType)))
				return null;
			return Convert.ChangeType(num, paraType);
		}
		if (IsString(value))
		{
			if (paraType == typeof(string)) return AsString(value);
		}
		return null;
	}

	static Value ConvertResult(object? result)
	{
		if (result is null) return Nil;
		var type = result.GetType();
		if (IsNumberTypeCode(Type.GetTypeCode(type)))
		{
			double d = (double)Convert.ChangeType(result, TypeCode.Double);
			return CreateNumberVal(d);
		}
		if (result is string str) 
			return CreateStringVal(str);
		throw new NativeException($"Value conversion error for type {type.Name}");
	}

	object[]? MatchParameter(Value[] args, int index, ParameterInfo[] paras)
	{
		object[] matchedParas = new object[paras.Length];
		for (int i = 0; i < paras.Length; i++)
		{
			var mp = MatchParameter(args[i + index], paras[i].ParameterType);
			if (mp == null) return null;
			matchedParas[i] = mp;
		}
		return matchedParas;
	}

	(bool, Value) MatchAndInvoke(Type type, string methodName, Value[] args)
	{
		foreach (var mthm in type.GetMethods(BindingFlags.Static| BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
		{
			if (mthm.Name != methodName) continue;
			var paras = mthm.GetParameters();
			bool isInstance = !mthm.IsStatic;
			int index = isInstance ? 2: 1;
			if (paras.Length != args.Length - index)
				continue;
			object? _this = null;
			if (isInstance)
				_this = MatchParameter(args[1], mthm.DeclaringType);
			var paraValues = MatchParameter(args, index, paras);
			var result = mthm.Invoke(_this, paraValues);
			var value = ConvertResult(result);
			return (true, value);
		}
		foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
		{
			if (prop.Name != methodName) continue;
			if (args.Length == 2) // getter with this
			{
				var _this = MatchParameter(args[1], prop.DeclaringType);
				if (_this != null)
					return (true, ConvertResult(prop.GetValue(_this)));
			}
		}
		return (false, Nil);
	}

	public Value Invoke(Value[] args)
	{
		var funcName = AsString(args[0]);
		int idx = funcName.LastIndexOf('.');
		var clsName = funcName[..idx];
		if (!typeDict.TryGetValue(clsName, out var type))
		{
			type = FindType(clsName);
			typeDict.Add(clsName, type);
		}
		var mthName = funcName[(idx + 1)..];
		var (res, val) = MatchAndInvoke(type, mthName, args);
		if (!res)
			throw new NativeException($"Invalid arguments calling {funcName}");
		return val;
	}

}
