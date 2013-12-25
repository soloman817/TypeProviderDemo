[<AutoOpen>]
module Demo.Common.Cache

open System.Collections.Generic
open Samples.FSharp.ProvidedTypes

type Cache<'Key when 'Key:equality>() =
    let cache = Dictionary<'Key, ProvidedTypeDefinition>()

    member this.Get (key:'Key) (create:'Key -> ProvidedTypeDefinition) =
        if cache.ContainsKey(key) then cache.[key]
        else
            let ty = create key
            cache.Add(key, ty)
            ty

