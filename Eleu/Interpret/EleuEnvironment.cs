using Eleu.Types;

namespace Eleu.Interpret;



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
		return NilValue;
	}

	public bool ContainsAtDistance0(string name) => values.ContainsKey(name);
	public object GetAt(string name, int distance)
	{
		var tab = Ancestor(distance)?.values;
		var val = NilValue;
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
	private void GetNameAndValues(List<VariableInfo> list, EleuEnvironment? fence)
	{
		if (this == fence) return;
		foreach (var item in values)
		{
			if (item.Value is ICallable) continue;
			list.Add(new VariableInfo(item.Key, item.Value));
		}
	}
	public List<VariableInfo> GetVariableInfos(EleuEnvironment? fence)
	{
		var list = new List<VariableInfo>();
		this.GetNameAndValues(list, fence);
		if (enclosing != null)
			enclosing.GetNameAndValues(list, fence);
		return list;
	}
}
