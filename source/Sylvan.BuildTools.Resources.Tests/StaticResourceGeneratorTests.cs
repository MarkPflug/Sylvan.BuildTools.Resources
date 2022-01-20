using Xunit;
using Xunit.Abstractions;

namespace Sylvan.BuildTools.Resources;

public class StaticResourceGeneratorTests : MSBuildTestBase
{
	public StaticResourceGeneratorTests(ITestOutputHelper o) : base(o) { }

	[Fact]
	public void Build()
	{
		var exepath = BuildProject("Data/Static1/Proj.csproj");
		Assert.Equal($"select * from my_table", GetOutput(exepath, ""));
	}

	[Fact]
	public void Escape()
	{
		var exepath = BuildProject("Data/StaticEscape/Proj.csproj");
		Assert.Equal($"™\r\n\t", GetOutput(exepath, ""));
	}

	[Fact]
	public void Name()
	{
		var exepath = BuildProject("Data/StaticName/Proj.csproj");
		Assert.Equal($"success", GetOutput(exepath, ""));
	}

	[Fact]
	public void Nested()
	{
		var exepath = BuildProject("Data/StaticNested/Proj.csproj");
		Assert.Equal($"abc", GetOutput(exepath, ""));
	}
}

