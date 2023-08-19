namespace Eleu.Interpret;

public struct InterpretResult
{
	public enum Status
	{
		Normal,
		Break,
		Continue,
		Return,
	}

	internal readonly object Value;
	internal readonly Status Stat;

	public InterpretResult(object val, Status stat = Status.Normal)
	{
		Value = val;
		this.Stat = stat;
	}

	public static readonly InterpretResult NilResult = new(NilValue);
	public static readonly InterpretResult BreakResult = new(NilValue, Status.Break);
	public static readonly InterpretResult ContinueResult = new(NilValue, Status.Continue);

	public override string ToString() => $"{Stat}: {Stringify(Value)}";

}
