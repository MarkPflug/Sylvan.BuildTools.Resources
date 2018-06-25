namespace Elemental.Json
{
	public enum JsonErrorCode : byte
	{
		Unknown = 0,
		UnexpectedCharacter,
		ExpectedStartObjectOrArray,
		ExpectedEndOfText,
		UnexpectedEndOfText,
		MissingComma,
		MissingColon,
		TokenTooLong,
		MultipleRootElements,
		UnexpectedEndObject,
		UnexpectedEndArray,
		ExpectedObjectMember,
		ExpectedColon,
		UnexpectedToken,
		UnterminatedString
	}
}
