namespace Eleu.Interpret;

interface LoxCallable
{
	Value Call(Interpreter interpreter, Value[] arguments);
	int arity();

}

class LoxFunction : Obj, LoxCallable
{
	private Stmt.Function declaration;
	private EleuEnvironment closure;
	private bool isInitializer;
	public LoxFunction(Stmt.Function declaration, EleuEnvironment closure, bool isInitializer) 
		: base(OBJ_FUNCTION)
	{
		this.declaration = declaration;
		this.closure = closure;
		this.isInitializer = isInitializer;
	}
	public int arity() => declaration.Paras.Count;
	public Value Call(Interpreter interpreter, Value[] arguments)
	{
		var environment = new EleuEnvironment(closure);
		for (int i = 0; i < declaration.Paras.Count; i++)
		{
			environment.Define(declaration.Paras[i].StringValue, arguments[i]);
		}
		var retVal= interpreter.executeBlock(declaration.Body, environment).Value;
		if (isInitializer) return closure.getAt(0, "this");
		return retVal;
	}
	public override string ToString() => $"<fn {declaration.Name}>";

	public LoxFunction bind(LoxInstance instance)
	{
		var environment = new EleuEnvironment(closure);
		environment.Define("this", CreateObjVal(instance));
		return new LoxFunction(declaration, environment, isInitializer);
	}

}

class LoxClass : ObjClass, LoxCallable
{
	private LoxClass? superclass;
	public LoxClass(string name, LoxClass? superclass) : base(name)
	{
		this.superclass = superclass;
	}
	public int arity()
	{
		if (findMethod("init").oValue is not LoxFunction initializer) return 0;
		return initializer.arity();
	}

	public Value Call(Interpreter interpreter, Value[] arguments)
	{
		var instance = new LoxInstance(this);
		LoxFunction? initializer = findMethod("init").oValue as LoxFunction;
		if (initializer != null)
		{
			initializer.bind(instance).Call(interpreter, arguments);
		}
		return CreateObjVal(instance);
	}

	public Value findMethod(String name)
	{
		if (methods.Get(name, out var val))
			return val;
		if (superclass != null)
		{
			return superclass.findMethod(name);
		}
		return NilValue;
	}

}

class LoxInstance : ObjInstance
{
	private new LoxClass klass => (LoxClass)base.klass;

	public LoxInstance(LoxClass klass) : base(klass)
	{
	}

	public Value get(string name)
	{
		if (!fields.Get(name, out var val))
		{
			var method = klass.findMethod(name);
			if (IsNil(method)) throw new EleuRuntimeError("Undefined property '" + name + "'.");
			var func = method.oValue as LoxFunction;
			return CreateObjVal(func!.bind(this));
		}
		return val;
	}

	public void set(string name, Value value) => fields.Set(name, value);

}

