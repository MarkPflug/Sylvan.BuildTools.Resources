using System;

namespace Sylvan.BuildTools.Resources
{
	public class JsonParseException : Exception
	{
		public JsonErrorCode Error { get; }
		public Location Location { get; }

		internal JsonParseException(JsonErrorCode error, Location location)
		{
			this.Error = error;
			this.Location = location;
		}
	}
}
