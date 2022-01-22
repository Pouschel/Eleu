namespace Eleu;

public abstract class EleuException : Exception
{
	public InputStatus? Status;

	public EleuException(InputStatus? status, string msg) : base(msg)
	{
		this.Status = status;
	}
}

public class EleuParseError : EleuException
{
	public EleuParseError() : base(null, "")
	{
	}
}

public class EleuRuntimeError : EleuException
{
	public EleuRuntimeError(string msg) : this(null, msg)
	{
	}

	public EleuRuntimeError(InputStatus? status, string msg) : base(status, msg)
	{

	}
}


