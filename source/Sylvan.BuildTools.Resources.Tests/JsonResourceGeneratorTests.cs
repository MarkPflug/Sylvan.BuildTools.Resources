using System;
using Xunit;
using Xunit.Abstractions;

namespace Sylvan.BuildTools.Resources;
public class JsonResourceGeneratorTests : MSBuildTestBase
{
	public JsonResourceGeneratorTests(ITestOutputHelper o) : base(o) { }

	[Fact]
	public void BuildCommentsTest()
	{
		var exepath = BuildProject("Data/ProjComments/Proj.csproj");
		Assert.Equal($"Hello, World{Environment.NewLine}", GetOutput(exepath, ""));
	}

	[Fact]
	public void BuildTest()
	{
		var exepath = BuildProject("Data/Proj1/Proj.csproj");
		Assert.Equal($"Hello, World{Environment.NewLine}", GetOutput(exepath, ""));
	}

	[Fact]
	public void BuildTest2()
	{
		var exepath = BuildProject("Data/Proj2/Proj.csproj");
		Assert.Equal($"Hello, World{Environment.NewLine}", GetOutput(exepath, ""));
		Assert.Equal($"Hallo, Welt{Environment.NewLine}", GetOutput(exepath, "de-DE"));
	}

	[Fact]
	public void BuildTestNetCore()
	{
		var exepath = BuildProject("Data/Proj3/Proj.csproj");
		Assert.Equal($"Hello, World{Environment.NewLine}", GetOutput(exepath, ""));
		Assert.Equal($"Hallo, Welt{Environment.NewLine}", GetOutput(exepath, "de-DE"));
	}

	[Fact]
	public void BuildTestNamespace()
	{
		var exepath = BuildProject("Data/ProjNS/Proj.csproj");
		Assert.Equal($"Hello, World{Environment.NewLine}", GetOutput(exepath, ""));
	}
}
