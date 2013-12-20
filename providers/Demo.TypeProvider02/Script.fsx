#r @"bin\Debug\Demo.TypeProvider02.dll"

type Vector2D = Demo.TypeProvider02.Vector2D
type Vector3D = Demo.TypeProvider02.Vector3D

let a = Vector2D(100.0, 200.0)
let b = Vector2D(10.0, 20.0)
printfn "a = %O" a
printfn "b = %O" b
printfn "a * b = %f" (a.DotProduct(b))
