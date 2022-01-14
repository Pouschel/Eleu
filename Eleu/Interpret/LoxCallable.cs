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
			environment.Define(declaration.Paras[i].StringValue,arguments[i]);
		}
		return interpreter.executeBlock(declaration.Body, environment).Value;
	}
	public override string ToString() => $"<fn {declaration.Name}>";
}
