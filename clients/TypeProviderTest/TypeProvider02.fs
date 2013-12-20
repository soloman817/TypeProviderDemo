module TypeProviderTest.TypeProvider02

open NUnit.Framework

type Vector2D = Demo.TypeProvider02.Vector2D
type Vector3D = Demo.TypeProvider02.Vector3D

[<Test>]
let ``test Vector2D``() =
    let a = Vector2D(100.0, 200.0)
    let b = Vector2D(10.0, 20.0)
    let c = a.DotProduct(b)

    printfn "a = %O" a
    printfn "b = %O" b
    printfn "a * b = %f" c

    Assert.AreEqual(c, 5000.0)
