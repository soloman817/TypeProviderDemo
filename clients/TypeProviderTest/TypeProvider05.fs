module TypeProviderTest.TypeProvider05

open Alea.CUDA
open Alea.CUDA.Utilities
open NUnit.Framework

[<Literal>]
let namespaces = "Demo.GPUTypes"

type GPUHelper = Demo.TypeProvider05.GPUHelper<namespaces>

[<Test>]
let test () =
    let ty = typeof<GPUHelper.Demo.GPUTypes.PairHelper.DeviceSeq>
    printfn "%A" (ty.GetCustomAttributes(false))

    let template = cuda {
        let! kernel = 
            <@ fun (output:deviceptr<float>) (input:GPUHelper.Demo.GPUTypes.PairHelper.DeviceSeq) ->
                let tid = threadIdx.x
                output.[tid] <- input.First.[tid]
                () @>
            |> Compiler.DefineKernel

        return () }

    let irm = template |> Compiler.compile

    ()


//[<Test>]
//let ``test Vector2D``() =
//    let a = VectorSet.Vector2D(100.0, 200.0)
//    let b = VectorSet.Vector2D(10.0, 20.0)
//    let c = a.DotProduct(b)
//
//    printfn "a = (%f, %f)" a.X a.Y
//    printfn "b = (%f, %f)" b.X b.Y
//    printfn "a * b = %f" c
//
//    Assert.AreEqual(c, 5000.0)
