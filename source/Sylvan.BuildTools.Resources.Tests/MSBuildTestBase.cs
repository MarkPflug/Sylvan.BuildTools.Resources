using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Sylvan.BuildTools;

class MsBuildFixture : IDisposable
{
	// force a reference to ImmutableArray which causes load issues
	// when some subsequent MSBuild assembly loads.
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable CS0414
	static readonly ImmutableArray<int> hack = new ImmutableArray<int>();
#pragma warning restore CS0414
#pragma warning restore IDE0052 // Remove unread private members

	public MsBuildFixture()
	{
		var inst = MSBuildLocator.RegisterDefaults();
	}

	public void Dispose()
	{
		MSBuildLocator.Unregister();
	}
}

[CollectionDefinition("MSBuild")]
public class MSBuildCollection : ICollectionFixture<MsBuildFixture>
{
}

[Collection("MSBuild")]
public abstract class MSBuildTestBase
{

	XUnitTestLogger logger;
	ITestOutputHelper o;

	static Dictionary<string, string> gp =
		new Dictionary<string, string>
		{
			["Configuration"] = "Debug",
			["Platform"] = "AnyCPU",
		};

	public MSBuildTestBase(ITestOutputHelper o)
	{
		this.o = o;
		this.logger = new XUnitTestLogger(o);
	}

	protected string GetOutput(string dllPath, string args)
	{
		args = $"{dllPath} {args}";

		var psi = new ProcessStartInfo("dotnet", args)
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			StandardOutputEncoding = System.Text.Encoding.UTF8,
			CreateNoWindow = true,
		};
		var proc = Process.Start(psi);
		proc.WaitForExit();
		var text = proc.StandardOutput.ReadToEnd();
		return text;
	}

	void LogProps(Project proj)
	{
		foreach (var kvp in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().OrderBy(e => e.Key))
		{
			o.WriteLine(kvp.Key + ": " + kvp.Value);
		}

		foreach (var prop in proj.AllEvaluatedProperties.OrderBy(p => p.Name))
		{
			o.WriteLine(prop.Name + ": " + prop.EvaluatedValue + " (" + prop.UnevaluatedValue + ")");
		}
	}

	protected string BuildProject(string projFile)
	{
		var pc = new ProjectCollection(gp);
		var proj = pc.LoadProject(projFile);
		var restored = proj.Build("Restore", new[] { logger });
		if (!restored)
		{
			LogProps(proj);
		}
		//Assert.True(restored, "Failed to restore packages");
		var result = proj.Build(logger);
		var outputPath = proj.GetPropertyValue("TargetPath");
		Assert.True(result, "Build failed\n" + logger.ErrorMessage);
		return outputPath;
	}
}

