#r @"bin\Debug\Demo.TypeProvider01.dll"

type Vector2D = Demo.TypeProvider01.Vector<"X", "Y">
type Vector3D = Demo.TypeProvider01.Vector<"X", "Y", "Z">

let a = Vector2D()
a.X <- 100.0
a.Y <- 200.0

let b = Vector2D()
b.X <- 10.0
b.Y <- 20.0

printfn "(100.0, 200.0) * (10.0, 20.0) = %A" (a.DotProduct(b))
