module TypeProviderTest.TypeProvider03

open NUnit.Framework

type Vector2D = Demo.TypeProvider03.Vector2D
type Vector3D = Demo.TypeProvider03.Vector3D

[<Test>]
let ``test Vector2D``() =
    let a = Vector2D(100.0, 200.0)
    let b = Vector2D(10.0, 20.0)
    let c = a.DotProduct(b)

    printfn "a = (%f, %f)" a.X a.Y
    printfn "b = (%f, %f)" b.X b.Y
    printfn "a * b = %f" c

    Assert.AreEqual(c, 5000.0)
