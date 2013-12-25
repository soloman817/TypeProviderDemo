module Demo.Common.Util

open System
open System.Collections.Generic
open Microsoft.FSharp.Core.CompilerServices
open Samples.FSharp.ProvidedTypes

let __break() = Diagnostics.Debugger.Break()

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

//let tryMakeGenerative1 (ty:ProvidedTypeDefinition) (providedAssembly:ProvidedAssembly option) =
//    providedAssembly |> Option.iter (fun providedAssembly ->
//        ty.IsErased <- false
//        //ty.SuppressRelocation <- false
//        providedAssembly.AddTypes(ty::[]))
//
//let tryMakeGenerative2 (ty:ProvidedTypeDefinition) (providedAssembly:ProvidedAssembly option) =
//    providedAssembly |> Option.iter (fun providedAssembly ->
//        ty.IsErased <- false)

