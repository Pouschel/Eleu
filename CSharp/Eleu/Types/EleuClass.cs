using Eleu.Interpret;

namespace Eleu.Types;

class EleuClass : ICallable
{
	public readonly string Name;
	public readonly OTable Methods;
	public readonly EleuClass? Superclass;
	public EleuClass(string name, EleuClass? superclass)
	{
		this.Name = name;
		this.Superclass = superclass;
		this.Methods = new OTable();
	}
	public int Arity
	{
		get
		{
			if (FindMethod("init") is not EleuFunction initializer) return 0;
			return initializer.Arity;
		}
	}
	string ICallable.Name => Name;
	public object Call(Interpreter interpreter, object[] arguments)
	{
		var instance = new EleuInstance(this);
		EleuFunction? initializer = FindMethod("init") as EleuFunction;
		initializer?.Bind(instance, false).Call(interpreter, arguments);
		return instance;
	}
	public object FindMethod(string name)
	{
		if (Methods.Get(name, out var val))
			return val;
		if (Superclass != null)
		{
			return Superclass.FindMethod(name);
		}
		return NilValue;
	}
	public override string ToString() => Name;
	
}

