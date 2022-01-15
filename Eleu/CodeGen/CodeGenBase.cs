namespace Eleu.CodeGen;
using Eleu.Vm;

class CodeGenError : Exception
{
	public CodeGenError(string msg = "") : base(msg)
	{

	}
}
internal abstract class CodeGenBase
{
	protected List<Stmt> statements;
	protected EleuOptions options;
	protected abstract CompilerState current { get; }

	public CodeGenBase(EleuOptions options, List<Stmt> statements)
	{
		this.statements = statements;
		this.options = options;
	}

	public void Error(string message)
	{
		options.Err.WriteLine(message);
		throw new CodeGenError();
	}
	protected void DeclareVariable(string name)
	{
		if (current.scopeDepth == 0) 
			return;
		for (int i = current.localCount - 1; i >= 0; i--)
		{
			ref Local local = ref current.locals[i];
			if (local.depth != -1 && local.depth < current.scopeDepth)
				break;
			if (name == local.name)
				Error("Already a variable with this name in this scope.");
		}
		AddLocal(name);
	}
	protected void AddLocal(string name)
	{
		if (current.localCount >= current.locals.Length)
		{
			Error("Too many local variables in function.");
			return;
		}
		ref Local local = ref current.locals[current.localCount++];
		local.name = name;
		local.depth = -1;
		local.isCaptured = false;
	}
	protected int ResolveLocal(CompilerState compiler, string name)
	{
		for (int i = compiler.localCount - 1; i >= 0; i--)
		{
			ref Local local = ref compiler.locals[i];
			if (name == local.name)
			{
				if (local.depth == -1)
					Error("Can't read local variable in its own initializer.");
				return i;
			}
		}
		return -1;
	}
	protected void MarkInitialized()
	{
		if (current.scopeDepth == 0) return;
		current.locals[current.localCount - 1].depth = current.scopeDepth;
	}
	protected void VisitStmtList<R>(List<Stmt> list, Stmt.Visitor<R> visitor)
	{
		foreach (var istmt in list)
		{
			istmt.Accept(visitor);
		}
	}
}
