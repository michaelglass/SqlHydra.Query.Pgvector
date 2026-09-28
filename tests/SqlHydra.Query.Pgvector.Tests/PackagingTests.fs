module SqlHydra.Query.Pgvector.Tests.PackagingTests

open System
open System.Diagnostics
open System.IO
open Xunit
open Swensen.Unquote

let private repoRoot =
    Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

/// Runs `dotnet` in `dir`, failing with its output when it exits non-zero. The MSBuild variables
/// `dotnet test` sets for this process are dropped, so the child resolves its own SDK.
let private dotnet (dir: string) (args: string list) =
    let info = ProcessStartInfo("dotnet", args, WorkingDirectory = dir)
    info.RedirectStandardOutput <- true
    info.RedirectStandardError <- true

    for key in List.ofSeq info.Environment.Keys do
        if key.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase) then
            info.Environment.Remove key |> ignore

    use proc = Process.Start info
    let stdout = proc.StandardOutput.ReadToEndAsync()
    let stderr = proc.StandardError.ReadToEndAsync()
    proc.WaitForExit()

    if proc.ExitCode <> 0 then
        failwith $"dotnet {String.Join(' ', args)} exited {proc.ExitCode}:\n{stdout.Result}\n{stderr.Result}"

let rec private copyDirectory (source: string) (target: string) =
    Directory.CreateDirectory target |> ignore

    for file in Directory.GetFiles source do
        File.Copy(file, Path.Combine(target, Path.GetFileName file))

    for dir in Directory.GetDirectories source do
        match Path.GetFileName dir with
        | "bin"
        | "obj" -> ()
        | name -> copyDirectory dir (Path.Combine(target, name))

/// `dotnet sqlhydra` loads a `type_mappings` extension from the referencing project's bin/, and a
/// library does not copy package assemblies there. This packs the package from a copy of its
/// sources, so the checkout's build output is left alone, and builds a library against it the way
/// a consumer would, from a package folder of its own, so the throwaway version never reaches the
/// machine's NuGet cache.
[<Fact>]
[<Trait("Category", "Packaging")>]
let ``a library referencing the package gets the extension assembly, and only it, in bin`` () =
    let work =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"pgvector-packaging-{Guid.NewGuid():N}"))

    try
        let project = Path.Combine(work.FullName, "src", "SqlHydra.Query.Pgvector")
        copyDirectory (Path.Combine(repoRoot, "src", "SqlHydra.Query.Pgvector")) project

        // Directory.Build.props stays behind: its RefStamp refuses to pack outside a checkout, and
        // what it stamps is provenance, which a consumer's build never reads.
        File.Copy(Path.Combine(repoRoot, "README.md"), Path.Combine(work.FullName, "README.md"))

        let feed = Path.Combine(work.FullName, "feed")
        let version = "0.0.0-packaging"

        dotnet project [ "pack"; "-c"; "Release"; "-o"; feed; $"-p:Version={version}" ]

        let consumer =
            Directory.CreateDirectory(Path.Combine(work.FullName, "consumer")).FullName

        let globalPackages =
            Environment.GetEnvironmentVariable "NUGET_PACKAGES"
            |> Option.ofObj
            |> Option.defaultValue (
                Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".nuget", "packages")
            )

        // The machine's package cache doubles as a local source, so restore copies what it already has.
        let cacheSource =
            if Directory.Exists globalPackages then
                $"""<add key="cache" value="{globalPackages}" />"""
            else
                ""

        File.WriteAllText(
            Path.Combine(consumer, "nuget.config"),
            $"""<configuration>
  <config><add key="globalPackagesFolder" value="{Path.Combine(work.FullName, "packages")}" /></config>
  <packageSources>
    <clear />
    <add key="feed" value="{feed}" />
    {cacheSource}
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>"""
        )

        File.WriteAllText(
            Path.Combine(consumer, "Consumer.fsproj"),
            $"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup><Compile Include="Library.fs" /></ItemGroup>
  <ItemGroup><PackageReference Include="SqlHydra.Query.Pgvector" Version="{version}" /></ItemGroup>
</Project>"""
        )

        File.WriteAllText(Path.Combine(consumer, "Library.fs"), "module Library")

        dotnet consumer [ "build" ]

        let bin = Path.Combine(consumer, "bin", "Debug", "net10.0")
        test <@ File.Exists(Path.Combine(bin, "SqlHydra.Query.Pgvector.dll")) @>
        // The package's own dependencies still stay out, as they do for any library.
        test <@ not (File.Exists(Path.Combine(bin, "SqlHydra.Query.dll"))) @>
    finally
        work.Delete true
