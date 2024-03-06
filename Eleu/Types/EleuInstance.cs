namespace Eleu.Types;


class EleuInstance
{
	internal readonly EleuClass klass;
	private readonly OTable fields;

	public EleuInstance(EleuClass klass)
	{
		this.klass = klass;
		this.fields = new OTable();
	}
	public object Get(string name, bool bindInstructions)
	{
		if (!fields.Get(name, out var val))
		{
			var method = klass.FindMethod(name);
			if (method == NilValue) throw new EleuRuntimeError("Undefined property '" + name + "'.");
			var func = method as EleuFunction;
			return func!.bind(this,bindInstructions);
		}
		return val;
	}

	public void Set(string name, object value) => fields.Set(name, value);
	public override string ToString() => $"{klass.Name} instance";
}

