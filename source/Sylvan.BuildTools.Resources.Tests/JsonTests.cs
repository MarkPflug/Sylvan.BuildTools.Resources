using System.IO;
using Xunit;

namespace Sylvan.BuildTools.Resources;

public class JsonTests
{
	// these tests are primarily for debugging the json behaviors.
	
	[Fact]
	public void NameAsValue()
	{
		var json = "{name:value}";
		var handler = new JsonParseErrorHandler((c, l) =>
		{
			return true;
		});
		var r = new JsonReader(new StringReader(json), handler);
		var d = JsonDocument.Load(r);
		Assert.NotNull(d);
	}

	[Fact]
	public void BadObject()
	{
		var json = "{{}}";
		var handler = new JsonParseErrorHandler((c, l) =>
		{
			return true;
		});
		var r = new JsonReader(new StringReader(json), handler);
		var d = JsonDocument.Load(r);
		Assert.NotNull(d);
	}
}
