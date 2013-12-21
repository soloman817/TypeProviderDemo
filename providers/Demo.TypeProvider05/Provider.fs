module Demo.TypeProvider05.Provider

open System
open System.Reflection
open System.Collections.Generic
open System.Text.RegularExpressions
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open Demo.Common

[<assembly: TypeProviderAssembly>]
do ()

type Entity(ty:Type) =
    member this.Type = ty

[<TypeProvider>]
type Provider(cfg:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    do Util.dumpTypeProviderConfig cfg

    let asm = Assembly.GetExecutingAssembly()
    let ns = "Demo.TypeProvider05"
    let cache = Util.Cache()

    let makeHelpers (nss:string) (name:string) =
        printfn "Generating %s ..." name
        let nss = Regex.Split(nss, ";")
        printfn "%A" nss
        failwith "TODO"

    let factoryType =
        let ty = ProvidedTypeDefinition(asm, ns, "GPUHelper", Some typeof<obj>)
        let parameters = ProvidedStaticParameter("Namespaces", typeof<string>) :: []
        let instantiate (name:string) (parameters:obj[]) =
            let nss = parameters.[0] :?> string
            cache.Get name (makeHelpers nss)
        ty.DefineStaticParameters(parameters, instantiate)
        ty

    do this.AddNamespace(ns, factoryType :: [])

