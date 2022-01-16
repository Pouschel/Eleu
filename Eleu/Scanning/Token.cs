namespace Eleu.Scanning;

public struct Token
{
	public TokenType type;
	public int start;
	public int end;
	public InputStatus status;
	public string source;

	public string StringValue => string.Intern(source[start..end]);

	public string StringStringValue => source[(start + 1)..(end - 1)];

	public override string ToString() => $"{type}: {StringValue}";

}




