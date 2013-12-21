#r @"bin\Debug\Demo.TypeProvider04.dll"

type VectorSet3 = Demo.TypeProvider04.VectorSet<3>
type VectorSet5 = Demo.TypeProvider04.VectorSet<5>

let a = VectorSet3.Vector2D(100.0, 200.0)
let b = VectorSet3.Vector2D(10.0, 20.0)
let c = a.DotProduct(b)
printfn "%f" c

