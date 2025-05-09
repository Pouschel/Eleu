using System.Linq.Expressions;

namespace Eleu.Ast;

public abstract class ExprStmtBase
{
	public InputStatus Status = InputStatus.Empty;
	internal int localDistance = -1;
	public override string ToString()
		=> Status.IsEmpty ?  Status.ReadPartialText() : $"a {this.GetType().Name}";
}


