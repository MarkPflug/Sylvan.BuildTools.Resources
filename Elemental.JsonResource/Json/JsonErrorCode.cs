namespace Elemental.Json
{
	public enum JsonErrorCode : byte
	{
		Unknown = 0,
		UnexpectedCharacter,
		ExpectedStartObjectOrArray,
		UnexpectedEndOfFile,
		MissingComma,
		MissingColon,
		TokenTooLong,
	}
}
