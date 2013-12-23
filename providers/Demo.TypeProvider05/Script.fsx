#r @"..\packages\Alea.cuBase.1.2.718\lib\net40\Alea.CUDA.dll"
#r @"..\Demo.GPUTypes\bin\Debug\Demo.GPUTypes.dll"
#r @"..\Demo.GPUTypes2\bin\Debug\Demo.GPUTypes2.dll"
#r @"bin\Debug\Demo.TypeProvider05.dll"

[<Literal>]
let nss = "Demo.GPUTypes"
    
type GPUHelper = Demo.TypeProvider05.GPUHelper<nss>

open Alea.CUDA
open Alea.CUDA.Utilities

let template = cuda {
    let! kernel = 
        <@ fun (output:deviceptr<float>) (input:GPUHelper.Demo.GPUTypes.PairHelper.Seq) ->
            let tid = threadIdx.x
            output.[tid] <- input.First.[tid]
            () @>
        |> Compiler.DefineKernel

    return () }

let irm = template |> Compiler.compile