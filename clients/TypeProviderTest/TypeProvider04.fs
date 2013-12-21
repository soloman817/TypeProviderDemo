module TypeProviderTest.TypeProvider04

open NUnit.Framework

type VectorSet = Demo.TypeProvider04.VectorSet<10>

[<Test>]
let ``test Vector2D``() =
    let a = VectorSet.Vector2D(100.0, 200.0)
    let b = VectorSet.Vector2D(10.0, 20.0)
    let c = a.DotProduct(b)

    printfn "a = (%f, %f)" a.X a.Y
    printfn "b = (%f, %f)" b.X b.Y
    printfn "a * b = %f" c

    Assert.AreEqual(c, 5000.0)
