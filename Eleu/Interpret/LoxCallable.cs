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
	public LoxFunction(Stmt.Function declaration, EleuEnvironment closure) : base(OBJ_FUNCTION)
	{
		this.declaration = declaration;
		this.closure = closure;
	}
	public int arity() => declaration.Paras.Count;
	public Value Call(Interpreter interpreter, Value[] arguments)
	{
		var environment = new EleuEnvironment(closure);
		for (int i = 0; i < declaration.Paras.Count; i++)
		{
			environment.Define(declaration.Paras[i].StringValue, arguments[i]);
		}
		return interpreter.executeBlock(declaration.Body, environment).Value;
	}
	public override string ToString() => $"<fn {declaration.Name}>";
}

class LoxClass : ObjClass, LoxCallable
{
	public LoxClass(string name) : base(name)
	{

	}
	public int arity() => 0;
	public Value Call(Interpreter interpreter, Value[] arguments)
	{
		var instance = new LoxInstance(this);
		return CreateObjVal(instance);
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
			throw new EleuRuntimeError("Undefined property '" + name + "'.");
		return val;
	}

	public void set(string name, Value value) => fields.Set(name, value);

}

