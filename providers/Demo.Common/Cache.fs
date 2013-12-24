[<AutoOpen>]
module Demo.Common.Cache

open System.Collections.Generic
open Samples.FSharp.ProvidedTypes

type Cache() =
    let cache = Dictionary<string, ProvidedTypeDefinition>()

    member this.Get (key:string) (create:string -> ProvidedTypeDefinition) =
        if cache.ContainsKey(key) then cache.[key]
        else
            let ty = create key
            cache.Add(key, ty)
            ty

