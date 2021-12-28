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
	Dictionary<string, Type> typeDict= new Dictionary<string, Type>();
	
	public Value Clock(Value[] _)
	{
		return CreateNumberVal(DateTime.Now.Ticks / 10000000.0);
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
			case TypeCode.Double: case TypeCode.Single:
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

	object? MatchParameter(Value value, Type paraType)
	{
		if (IsNumber(value))
		{
			var num= AsNumber(value);
			if (!IsNumberTypeCode(Type.GetTypeCode(paraType)))
				return null;
			return Convert.ChangeType(num, paraType);
		}
		return null;
	}

	static Value ConvertResult(object? result)
	{
		if (result is null) return Nil;
		var type = result.GetType();
		if (IsNumberTypeCode(Type.GetTypeCode(type)))
		{
			double d = (double) Convert.ChangeType(result, TypeCode.Double);
			return CreateNumberVal(d);
		}
		throw new NativeException($"Value conversion error for type {type.Name}");
	}

	object[]?  MatchParameter(ParameterInfo[] paras, Value[] args)
	{
		object[] matchedParas = new object[paras.Length];
		for (int i = 0; i < paras.Length; i++)
		{
			var mp = MatchParameter(args[i + 1], paras[i].ParameterType);
			if (mp == null) return null;
			matchedParas[i] = mp;
		}
		return matchedParas;
	}

	(bool, Value) MatchAndInvoke(Type type, string methodName, Value[] args)
	{
		
		foreach (var mthm in type.GetMethods(BindingFlags.Static|BindingFlags.NonPublic| BindingFlags.Public))
		{
			if (mthm.Name != methodName) continue;
			var paras = mthm.GetParameters();
			if (paras.Length != args.Length - 1)
				continue;
			var paraValues= MatchParameter(paras, args);
			var result= mthm.Invoke(null, paraValues);
			var value = ConvertResult(result);
			return (true, value);
		}
		return (false, Nil);
	}

	public Value Invoke(Value[] args)
	{
		var funcName = AsString(args[0]);
		int idx = funcName.LastIndexOf('.');
		var clsName = funcName[..idx];
		if (!typeDict.TryGetValue(clsName,out var type))
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
