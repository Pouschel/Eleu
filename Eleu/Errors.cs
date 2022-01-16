namespace Eleu;

public class EleuException : Exception
{
	public EleuException(string msg) : base(msg)
	{
	}
}

public class EleuParseError : EleuException
{
	public EleuParseError() : base("")
	{
	}
}

public class EleuRuntimeError : EleuException
{
	public EleuRuntimeError(string msg) : base(msg)
	{
	}
}


