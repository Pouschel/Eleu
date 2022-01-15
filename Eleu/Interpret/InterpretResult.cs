namespace Eleu.Interpret;

public struct InterpretResult
{
	public enum Status
	{
		Normal,
		Return,
	}
	public readonly object Value;
	public readonly Status Stat;

	public InterpretResult(object val, Status stat = Status.Normal)
	{
		Value = val;
		this.Stat = stat;
	}

	public static readonly InterpretResult NilResult=new (Nil);

	//public static implicit operator InterpretResult(object val) => new(val);
}
