open System
open System.Xml.Linq

let projectPath =
    IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "src", "SqlHydra.Query.Pgvector", "SqlHydra.Query.Pgvector.fsproj")

let project = XDocument.Load projectPath

let sqlHydraQueryReference =
    project.Descendants(XName.Get "PackageReference")
    |> Seq.find (fun reference -> reference.Attribute(XName.Get "Include").Value = "SqlHydra.Query")

let actualVersion = sqlHydraQueryReference.Attribute(XName.Get "Version").Value
let expectedVersion = "4.1.1"

if actualVersion <> expectedVersion then
    failwith $"Expected SqlHydra.Query dependency floor {expectedVersion}, but found {actualVersion}."

printfn $"SqlHydra.Query dependency floor is {actualVersion}."
