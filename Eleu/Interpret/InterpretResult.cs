namespace Eleu.Interpret;

public struct InterpretResult
{
	public enum Status
	{
		Normal,
		Return,
	}


	public readonly Value Value;
	public readonly Status Stat;

	public InterpretResult(Value val, Status stat = Status.Normal)
	{
		Value = val;
		this.Stat = stat;
	}

	public static implicit operator InterpretResult(in Value val) => new(val);
}
