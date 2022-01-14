namespace Eleu.Interpret;




class EleuEnvironment
{
	readonly EleuEnvironment? enclosing;
	readonly Table values = new();

	public EleuEnvironment(EleuEnvironment? enclosing = null)
	{
		this.enclosing = enclosing;
	}
	public void Define(string name, in Value value) => values.Set(name, value);
	public Value Get(string name)
	{
		if (values.Get(name, out var value))
			return value;
		if (enclosing != null)
			return enclosing.Get(name);
		throw new EleuRuntimeError("Undefined variable '" + name + "'.");
	}
	public void Assign(string name, Value value)
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
		throw new EleuRuntimeError("Undefined variable '" + name + "'.");
	}
}
