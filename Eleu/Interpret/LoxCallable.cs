namespace Eleu.Interpret;

interface LoxCallable
{
	object Call(Interpreter interpreter, object[] arguments);
	int arity();
}

internal class LoxNative : LoxCallable
{
	public readonly NativeFn function;

	public LoxNative(NativeFn function) 
	{
		this.function = function;
	}

	public int arity() => function.Method.GetParameters().Length - 1;

	public object Call(Interpreter interpreter, object[] arguments)
	{
		return function(arguments);
	}
	public override string ToString() => "<native fn>";

}
class LoxFunction :  LoxCallable
{
	private Stmt.Function declaration;
	private EleuEnvironment closure;
	private bool isInitializer;
	public LoxFunction(Stmt.Function declaration, EleuEnvironment closure, bool isInitializer) 
	{
		this.declaration = declaration;
		this.closure = closure;
		this.isInitializer = isInitializer;
	}
	public int arity() => declaration.Paras.Count;
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

	public LoxFunction bind(LoxInstance instance)
	{
		var environment = new EleuEnvironment(closure);
		environment.Define("this", instance);
		return new LoxFunction(declaration, environment, isInitializer);
	}

}

class LoxClass :  LoxCallable
{
	public readonly string Name;
	public readonly OTable Methods;
	private LoxClass? superclass;
	public LoxClass(string name, LoxClass? superclass) 
	{
		this.Name = name;
		this.superclass = superclass;
		this.Methods = new OTable();
	}
	public int arity()
	{
		if (findMethod("init") is not LoxFunction initializer) return 0;
		return initializer.arity();
	}

	public object Call(Interpreter interpreter, object[] arguments)
	{
		var instance = new LoxInstance(this);
		LoxFunction? initializer = findMethod("init") as LoxFunction;
		if (initializer != null)
		{
			initializer.bind(instance).Call(interpreter, arguments);
		}
		return instance;
	}

	public object findMethod(String name)
	{
		if (Methods.Get(name, out var val))
			return val;
		if (superclass != null)
		{
			return superclass.findMethod(name);
		}
		return Nil;
	}
	public override string ToString() => Name;
}

class LoxInstance 
{
	private LoxClass klass;
	private OTable fields;


	public LoxInstance(LoxClass klass) 
	{
		this.klass = klass;
		this.fields = new OTable();
	}

	public object get(string name)
	{
		if (!fields.Get(name, out var val))
		{
			var method = klass.findMethod(name);
			if (method== Nil) throw new EleuRuntimeError("Undefined property '" + name + "'.");
			var func = method as LoxFunction;
			return func!.bind(this);
		}
		return val;
	}

	public void set(string name, object value) => fields.Set(name, value);
	public override string ToString() => $"{klass.Name} instance";
}

