using static Eleu.FunctionType;

namespace Eleu;

class CompilerState
{
	public CompilerState? enclosing;
	public ObjFunction function;
	public FunctionType type;
	public Local[] locals = new Local[UINT8_COUNT];
	public int localCount;
	public Upvalue[] upvalues = new Upvalue[UINT8_COUNT];
	public int scopeDepth;

	public CompilerState(FunctionType type)
	{
		this.type = type;
		this.function = new ObjFunction();
		ref Local local = ref locals[localCount++];
		local.depth = 0;
		local.isCaptured = false;
		if (type != FunTypeFunction)
			local.name = "this";
		else local.name = "";
	}

	public override string ToString() => $"{function.NameOrScript}[{function.arity}]";
}

