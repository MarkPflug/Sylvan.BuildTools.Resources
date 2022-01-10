using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Immutable;

namespace Sylvan.BuildTools.Resources
{
	class MsBuildFixture : IDisposable
	{
		static ImmutableArray<int> hack = new ImmutableArray<int>();

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
	public class JsonResourceGeneratorTests
	{
		XUnitTestLogger logger;
		ITestOutputHelper o;		

		static Dictionary<string, string> gp =
			new Dictionary<string, string> {
				["Configuration"] = "Debug",
				["Platform"] = "AnyCPU",
			};

		public JsonResourceGeneratorTests(ITestOutputHelper o)
		{
			this.o = o;
			this.logger = new XUnitTestLogger(o);
		}

		string GetOutput(string dllPath, string args)
		{
			args = $"{dllPath} {args}";

			var psi = new ProcessStartInfo("dotnet", args) {
				UseShellExecute = false,
				RedirectStandardOutput = true,
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

		string BuildProject(string projFile)
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
}
