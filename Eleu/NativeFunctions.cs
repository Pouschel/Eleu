namespace CsLox;

static class NativeFunctions
{
	public static Value clock(Value[] _)
	{
		return NUMBER_VAL(DateTime.Now.Ticks / 10000000.0);
	}

}
