namespace Eleu.Interpret;

interface ICallable
{
	object Call(Interpreter interpreter, object[] arguments);
	int Arity { get; }
}

internal class NativeFunction : ICallable
{
	public readonly NativeFn function;

	public NativeFunction(NativeFn function) 
	{
		this.function = function;
	}

	public int Arity => function.Method.GetParameters().Length - 1;

	public object Call(Interpreter interpreter, object[] arguments)
	{
		return function(arguments);
	}
	public override string ToString() => "<native fn>";

}
class EleuFunction : ICallable
{
	private readonly Stmt.Function declaration;
	private readonly EleuEnvironment closure;
	private readonly bool isInitializer;
	public EleuFunction(Stmt.Function declaration, EleuEnvironment closure, bool isInitializer) 
	{
		this.declaration = declaration;
		this.closure = closure;
		this.isInitializer = isInitializer;
	}
	public int Arity => declaration.Paras.Count;
	public object Call(Interpreter interpreter, object[] arguments)
	{
		var environment = new EleuEnvironment(closure);
		for (int i = 0; i < declaration.Paras.Count; i++)
		{
			environment.Define(declaration.Paras[i].StringValue, arguments[i]);
		}
		var retVal= interpreter.executeBlock(declaration.Body, environment).Value;
		if (isInitializer) return closure.GetAt("this", 0);
		return retVal;
	}
	public override string ToString() => $"<fn {declaration.Name}>";

	public EleuFunction bind(EleuInstance instance)
	{
		var environment = new EleuEnvironment(closure);
		environment.Define("this", instance);
		return new EleuFunction(declaration, environment, isInitializer);
	}

}

class EleuClass :  ICallable
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

	public object Call(Interpreter interpreter, object[] arguments)
	{
		var instance = new EleuInstance(this);
		EleuFunction? initializer = FindMethod("init") as EleuFunction;
		if (initializer != null)
		{
			initializer.bind(instance).Call(interpreter, arguments);
		}
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
		return Nil;
	}
	public override string ToString() => Name;
}

class EleuInstance 
{
	private readonly EleuClass klass;
	private readonly OTable fields;

	public EleuInstance(EleuClass klass) 
	{
		this.klass = klass;
		this.fields = new OTable();
	}
	public object Get(string name)
	{
		if (!fields.Get(name, out var val))
		{
			var method = klass.FindMethod(name);
			if (method== Nil) throw new EleuRuntimeError("Undefined property '" + name + "'.");
			var func = method as EleuFunction;
			return func!.bind(this);
		}
		return val;
	}

	public void Set(string name, object value) => fields.Set(name, value);
	public override string ToString() => $"{klass.Name} instance";
}

