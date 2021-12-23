namespace Eleu;

static class NativeFunctions
{
	public static Value clock(Value[] _)
	{
		return CreateNumberVal(DateTime.Now.Ticks / 10000000.0);
	}

}
