// VL.TestFramework compile checks: loads the package's .vl documents inside a headless
// VL host and asserts they compile without errors. This exercises the forwarding
// document and the C# node import (ImportNamespace attributes) end-to-end.
//
// Setup follows the working pattern of VL.Stride.BepuPhysics.Tests:
//  - entryAssembly is the path to vvvv.exe — vvvv's own standard libraries are then
//    included automatically and stay version-coherent.
//  - preCompilePackages MUST stay true: without it the VL compiler never loads the
//    runtime assemblies of referenced packages and computing imported pin defaults
//    crashes as soon as any referenced assembly declares an enum parameter default.

using NUnit.Framework;
using VL.TestFramework;

namespace VL.Stride.Text3d.Tests;

[TestFixture]
public class VlDocumentTests
{
    private TestEnvironment? testEnvironment;

    // The vvvv installation this library targets; override with the VVVV_DIR environment variable.
    private static string VvvvDir =>
        Environment.GetEnvironmentVariable("VVVV_DIR") ?? @"D:\vvvv\vvvv_gamma_7.4-win-x64";

    // NUnit sync-context issue: keep setup non-async (see vvvv testing docs)
    [OneTimeSetUp]
    public void Setup()
    {
        // Search paths act like vvvv's --package-repositories directories.
        var searchPaths = new List<string>
        {
            Path.GetDirectoryName(TestData.RepoRoot)!, // D:\_dev\_vl-libs (contains this package)
        };
        // The Physical 3d Text help patch depends on VL.Stride.BepuPhysics; its runtime
        // packages (Stride.BepuPhysics, BepuPhysics, BepuUtilities) live in the user
        // nugets folder on machines where the package is installed rather than checked
        // out next to this repo.
        var userNugets = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\vvvv\gamma\nugets");
        if (Directory.Exists(userNugets))
            searchPaths.Add(userNugets);
        testEnvironment = TestEnvironmentLoader.Load(Path.Combine(VvvvDir, "vvvv.exe"), searchPaths, preCompilePackages: true);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        testEnvironment?.Dispose();
        testEnvironment = null;
    }

    [Test]
    public async Task MainDocumentCompiles()
    {
        await testEnvironment!.LoadAndTestAsync(Path.Combine(TestData.RepoRoot, "VL.Stride.Text3d.vl"));
    }

    [Test]
    public async Task HelpText3dCompiles()
    {
        await testEnvironment!.LoadAndTestAsync(Path.Combine(TestData.RepoRoot, "help", "Explanation Overview Text3d.vl"));
    }

    [Test]
    public async Task HelpTextStylesCompiles()
    {
        await testEnvironment!.LoadAndTestAsync(Path.Combine(TestData.RepoRoot, "help", "Explanation Overview TextStyles.vl"));
    }

    // Needs VL.Stride.BepuPhysics resolvable (sibling checkout or installed nuget, see
    // Setup); if that proves fragile on a machine, ignore this one test rather than
    // removing the patch.
    [Test]
    public async Task HelpPhysical3dTextCompiles()
    {
        await testEnvironment!.LoadAndTestAsync(Path.Combine(TestData.RepoRoot, "help", "HowTo Physical 3d Text.vl"));
    }
}
