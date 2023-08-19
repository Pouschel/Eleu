using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Eleu.Types;

public struct Number : IEquatable<Number>, IComparable<Number>
{
	public readonly double DVal;

	const NumberStyles FloatStyle = NumberStyles.AllowDecimalPoint  | NumberStyles.AllowLeadingSign;

	public Number(double d)
	{
		if (!double.IsFinite(d))
			throw new InvalidOperationException();
		this.DVal = d;
	}
	public static Number? TryParse(string s)
	{
		if (!double.TryParse(s, FloatStyle, CultureInfo.InvariantCulture, out var d))
			return null;
		if (!double.IsFinite(d))
			return null;
		return new(d);
	}
	internal Number(long l) { this.DVal = l; }
	public bool IsDefined => double.IsFinite(DVal);
	public bool IsZero => DVal == 0;
	public int IntValue => (int)DVal;
	public bool IsInt => IntValue == DVal;
	public override string ToString()
	{
		return DVal.ToString(CultureInfo.InvariantCulture);
	}
	private static int Cmp(in Number a, in Number b) => a.DVal.CompareTo(b.DVal);
	public bool Equals(Number other) => Cmp(this, other) == 0;
	public int CompareTo(Number other) => Cmp(this, other);
	public override bool Equals([NotNullWhen(true)] object? obj)
	{
		if (obj is Number num) return this.Equals(num);
		return false;
	}
	public override int GetHashCode() => DVal.GetHashCode();
	public static Number operator -(in Number a, in Number b)
	{
		var dres = a.DVal - b.DVal;
		return new Number(dres);
	}
	public static Number operator -(in Number a) => new Number(-a.DVal);
	public static Number operator +(in Number a, in Number b) => new(a.DVal + b.DVal);
	public static Number operator *(in Number a, in Number b) => new(a.DVal * b.DVal);
	public static Number operator /(in Number a, in Number b) => new(a.DVal / b.DVal);
	public static Number operator %(in Number a, in Number b) => new(a.DVal % b.DVal);
}


