namespace Eleu.Interpret;

class OTable : Dictionary<string, object>
{

	public void Set(string name, object val) => this[name] = val;

	public bool Get(string name, out object val)
	{
		bool b = TryGetValue(name, out val!);
		if (val == null)
			val = Nil;
		return b;
	}
}

class EleuEnvironment
{
	public readonly EleuEnvironment? enclosing;
	readonly OTable values = new();

	public EleuEnvironment(EleuEnvironment? enclosing = null)
	{
		this.enclosing = enclosing;
	}
	public void Define(string name, object value) => values.Set(name, value);

	public object GetAtDistance0(string name)
	{
		if (values.Get(name, out var oldVal))
			return oldVal;
		return Nil;
	}

	public bool ContainsAtDistance0(string name) => values.ContainsKey(name);
	public object GetAt(string name, int distance)
	{
		var tab = Ancestor(distance)?.values;
		var val = Nil;
		tab?.Get(name, out val);
		return val;
	}
	public void AssignAt(int distance, string name, object value) 
		=> Ancestor(distance)?.values.Set(name, value);

	EleuEnvironment? Ancestor(int distance)
	{
		var environment = this;
		for (int i = 0; i < distance; i++)
		{
			environment = environment?.enclosing;
		}
		return environment;
	}
	public object Lookup(string name)
	{
		if (values.Get(name, out var value))
			return value;
		if (enclosing != null)
			return enclosing.Lookup(name);
		throw new EleuRuntimeError("Variable nicht definiert '" + name + "'.");
	}
	public void Assign(string name, object value)
	{
		if (values.Get(name, out var _))
		{
			values.Set(name, value);
			return;
		}
		if (enclosing != null)
		{
			enclosing.Assign(name, value);
			return;
		}
		throw new EleuRuntimeError("Variable nicht definiert '" + name + "'.");
	}
}
