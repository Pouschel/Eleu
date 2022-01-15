using Eleu;
using static Eleu.ValueStatics;

public class Ex1
{
	static Value a = CreateNumberVal(1);
	
	public static Value Main()
	{
		var b = CreateNumberVal(2);
		a = CreateStringVal(@"Hallo");
		{
			var c = CreateNumberVal(3);
		}
		Console.WriteLine(b);
		Console.WriteLine(a);
		
		return NilValue;
	}
}
