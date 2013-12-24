module Demo.TypeProvider05.Provider

open System
open System.IO
open System.Reflection
open System.Collections.Generic
open System.Text.RegularExpressions
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open Alea.CUDA
open Alea.CUDA.Utilities
open Samples.FSharp.ProvidedTypes
open Demo.Common
open Demo.TypeProvider05.Framework
open Demo.TypeProvider05.Entities

[<assembly: TypeProviderAssembly>]
do ()

type [<Sealed>] RootPackage private (name:string, thisAssembly:Assembly, providedAssembly:ProvidedAssembly, providedNamespace:string) =
    inherit Package(None, name) 

    let registry = EntityRegistry()

    override this.EntityRegistry = registry

    override this.TryGenerateType() =
        let ty = ProvidedTypeDefinition(thisAssembly, providedNamespace, name, Some typeof<obj>, IsErased = false)
        providedAssembly.AddTypes(ty :: [])

        let nestedTypes = this.GenerateNestedTypes()
        ty.AddMembers nestedTypes

        Some ty

    static member Create(name:string, thisAssembly:Assembly, providedAssembly:ProvidedAssembly, providedNamespace:string, namespaces:string[], assemblies:Assembly[]) =
        let namespaces = namespaces |> Set.ofArray
        let rootPackage = RootPackage(name, thisAssembly, providedAssembly, providedNamespace)
        let registry = rootPackage.EntityRegistry

        RecordEntity.Register(registry)

        assemblies
        |> Array.map (fun asm -> asm.GetTypes())
        |> Array.concat
        |> Array.filter (fun ty -> ty.Namespace <> null)
        |> Array.filter (fun ty -> namespaces.Contains(ty.Namespace))
        |> Array.filter registry.IsTypeSupported
        |> Array.iter rootPackage.AddEntity

        rootPackage

[<TypeProvider>]
type Provider(cfg:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    let thisAssembly = Assembly.GetExecutingAssembly()
    let providedNamespace = "Demo.TypeProvider05"
    let cache = Cache()
    let tmpdlls = List<string>()

    let makeHelpers (namespaces:string) (name:string) =
        printfn "Generating %s ..." name
        //let tmpdll = @"C:\Users\Xiang\Desktop\AAA.dll"
        let tmpdll = Path.ChangeExtension(Path.GetTempFileName(), ".dll")
        tmpdlls.Add(tmpdll)
        printfn "TempDll: %s" tmpdll
        let providedAssembly = ProvidedAssembly(tmpdll)
        let namespaces = Regex.Split(namespaces, ";")
        let assemblies = cfg.ReferencedAssemblies |> Array.map Assembly.LoadFile
        let rootPackage = RootPackage.Create(name, thisAssembly, providedAssembly, providedNamespace, namespaces, assemblies)
        rootPackage.Dump()
        rootPackage.GenerateType()

    let factoryType =
        let ty = ProvidedTypeDefinition(thisAssembly, providedNamespace, "GPUHelper", Some typeof<obj>, IsErased = false)
        let parameters = ProvidedStaticParameter("Namespaces", typeof<string>) :: []
        let instantiate (name:string) (parameters:obj[]) =
            let namespaces = parameters.[0] :?> string
            printfn "Getting %A..." name
            cache.Get name (makeHelpers namespaces)
        ty.DefineStaticParameters(parameters, instantiate)
        ty

    do this.AddNamespace(providedNamespace, factoryType :: [])

    member this.Dispose(disposing:bool) =
        tmpdlls |> Seq.iter (fun tmpdll -> try File.Delete(tmpdll) with ex -> ())
        tmpdlls.Clear()

    member this.Dispose() =
        this.Dispose(true)
        GC.SuppressFinalize(this)

    override this.Finalize() =
        this.Dispose(false)

    interface IDisposable with
        member this.Dispose() = this.Dispose()
