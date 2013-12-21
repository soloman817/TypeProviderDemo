module Demo.TypeProvider05.Provider

open System
open System.Reflection
open System.Collections.Generic
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open Demo.Common

[<assembly: TypeProviderAssembly>]
do ()

type VectorType = float[]

[<TypeProvider>]
type Provider(cfg:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    do printfn "%A" cfg

    let asm = Assembly.GetExecutingAssembly()
    let ns = "Demo.TypeProvider05"

    let factoryType =
        let ty = ProvidedTypeDefinition(asm, ns, "VectorSet", Some typeof<obj>)
        let parameters = ProvidedStaticParameter("Type", typeof<System.Type>) :: []
        let instantiate (name:string) (parameters:obj[]) =
            failwith "TODO"
        ty.DefineStaticParameters(parameters, instantiate)
        ty

    do this.AddNamespace(ns, factoryType :: [])

