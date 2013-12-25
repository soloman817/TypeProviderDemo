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

let blobErased = false

type [<Sealed>] RootPackage private (name:string) =
    inherit Package(None, name) 

    let registry = EntityRegistry()

    override this.EntityRegistry = registry

    member this.GenerateHelperType(thisAssembly:Assembly, providedNamespace:string, name:string, providedAssembly:ProvidedAssembly) =
        let ty = ProvidedTypeDefinition(thisAssembly, providedNamespace, name, Some typeof<obj>, IsErased = false, SuppressRelocation = false)
        providedAssembly.AddTypes(ty :: [])

        let nestedHelperTypes = this.GenerateNestedHelperTypes()
        ty.AddMembers nestedHelperTypes

        ty

    static member Create(name:string, namespaces:string[], assemblies:Assembly[]) =
        let namespaces = namespaces |> Set.ofArray
        let rootPackage = RootPackage(name)
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

type HelperInfo =
    {
        Name : string
        Namespaces : string
        DllPath : string
        mutable ProvidedType : ProvidedTypeDefinition option
    }

    member this.Dump() =
        printfn "* Name       : %s" this.Name
        printfn "* Namespaces : %s" this.Namespaces
        printfn "* DllPath    : %s" this.DllPath

type BlobInfo =
    {
        Name : string
        DllPath : string
        Helper : HelperInfo
        mutable ProvidedType : ProvidedTypeDefinition option
    }

    member this.Dump() =
        printfn "* Name              : %s" this.Name
        printfn "* DllPath           : %s" this.DllPath
        printfn "* Helper.Name       : %s" this.Helper.Name
        printfn "* Helper.Namespaces : %s" this.Helper.Namespaces
        printfn "* Helper.DllPath    : %s" this.Helper.DllPath

[<TypeProvider>]
type Provider(cfg:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    let thisAssembly = Assembly.GetExecutingAssembly()
    let providedNamespace = "Demo.TypeProvider05"
    let helpers = Dictionary<string, HelperInfo>()

    let generateHelper (name:string) (parameters:obj[]) =
        if helpers.ContainsKey(name) then helpers.[name].ProvidedType.Value
        else
            let namespaces = parameters.[0] :?> string

            printfn "Generating %s ..." name
            let helperInfo : HelperInfo =
                {
                    Name = name
                    Namespaces = namespaces
                    //DllPath = @"C:\Users\Xiang\Desktop\AAA.dll"
                    DllPath = Path.ChangeExtension(Path.GetTempFileName(), ".dll")
                    ProvidedType = None
                }
            helperInfo.Dump()
            helpers.Add(name, helperInfo)

            let providedAssembly = ProvidedAssembly(helperInfo.DllPath)
            let namespaces = Regex.Split(namespaces, ";")
            let assemblies = cfg.ReferencedAssemblies |> Array.map Assembly.LoadFile
            let rootPackage = RootPackage.Create(name, namespaces, assemblies)
            rootPackage.Dump()
            let providedType = rootPackage.GenerateHelperType(thisAssembly, providedNamespace, name, providedAssembly)
            helperInfo.ProvidedType <- Some providedType
            providedType

    let helperProvider =
        let ty = ProvidedTypeDefinition(thisAssembly, providedNamespace, "HelperProvider", Some typeof<obj>, IsErased = false)
        let parameters = ProvidedStaticParameter("Namespaces", typeof<string>) :: []
        ty.DefineStaticParameters(parameters, generateHelper)
        ty

    do this.AddNamespace(providedNamespace, helperProvider :: [])

    member this.Dispose(disposing:bool) =
        helpers.Values |> Seq.iter (fun info -> try File.Delete(info.DllPath) with ex -> ())
        helpers.Clear()

    member this.Dispose() =
        this.Dispose(true)
        GC.SuppressFinalize(this)

    override this.Finalize() =
        this.Dispose(false)

    interface IDisposable with
        member this.Dispose() = this.Dispose()
