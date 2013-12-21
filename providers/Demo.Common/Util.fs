module Demo.Common.Util

open System
open System.Collections.Generic
open Microsoft.FSharp.Core.CompilerServices
open Samples.FSharp.ProvidedTypes

let __break() = Diagnostics.Debugger.Break()

type Cache() =
    let cache = Dictionary<string, ProvidedTypeDefinition>()

    member this.Get (key:string) (create:string -> ProvidedTypeDefinition) =
        if cache.ContainsKey(key) then cache.[key]
        else
            let ty = create key
            cache.Add(key, ty)
            ty

let dumpTypeProviderConfig(cfg:TypeProviderConfig) =
    printfn "TypeProviderConfig ==========="
    printfn "IsHostedExecution: %A" cfg.IsHostedExecution
    printfn "IsInvalidationSupported: %A" cfg.IsInvalidationSupported
    printfn "ReferencedAssemblies:"
    for x in cfg.ReferencedAssemblies do
        printfn "  %s" x
    printfn "ResolutionFolder: %s" cfg.ResolutionFolder
    printfn "RuntimeAssembly: %s" cfg.RuntimeAssembly
    printfn "SystemRuntimeAssemblyVersion: %A" cfg.SystemRuntimeAssemblyVersion
    printfn "TemporaryFolder: %s" cfg.TemporaryFolder
    printfn "=============================="