[<AutoOpen>]
module Demo.Common.Operators

open System.IO

let ( @@ ) a b = Path.Combine(a, b)

