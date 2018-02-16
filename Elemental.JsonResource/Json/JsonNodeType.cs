namespace Elemental.Json
{
	public enum SyntaxKind
	{
		None = 0,
		ArrayStart,
		ArrayEnd,
		ObjectStart,
		ObjectEnd,
		PropertyName,
		StringValue,
		DoubleValue,
		IntegerValue,
		TrueValue,
		FalseValue,
		NullValue,
		Comment,
	}
}
