namespace Elemental.Json
{
	enum TokenKind
	{
		EndOfText = -1,
		None = 0,
		StartObject = 1,
		EndObject,
		StartArray,
		EndArray,
		Name,
		NumberInteger,
		NumberFloat,
		String,
		Colon,
		Comma,
		LineComment,
		BlockComment,
	}
}
