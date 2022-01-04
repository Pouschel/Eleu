using Eleu;
using static Eleu.ValueStatics;

public class Ex1
{
	static Value a = CreateNumberVal(1);
	static Value b = CreateNumberVal(2);
	public static Value Main()
	{
		b = CreateStringVal(@"Hallo");
		a = b;
		Console.WriteLine(b);
		
		return Nil;
	}
}
