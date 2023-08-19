namespace Eleu.Scanning;

public struct Token
{
	public readonly TokenType Type { get; init; }
	public readonly int Start { get; init; }
	public readonly int End { get; init; }
	public readonly InputStatus Status { get; init; }
	public readonly string Source { get; init; }

	public string StringValue => Source[Start..End];

	public string StringStringValue => Source[(Start + 1)..(End - 1)];

	public override string ToString() => $"{Type}: {StringValue}";

}




