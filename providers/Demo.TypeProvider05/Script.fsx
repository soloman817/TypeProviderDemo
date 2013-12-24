#r @"..\packages\Alea.cuBase.1.2.723\lib\net40\Alea.CUDA.dll"
#r @"..\Demo.GPUTypes\bin\Debug\Demo.GPUTypes.dll"
#r @"..\Demo.GPUTypes2\bin\Debug\Demo.GPUTypes2.dll"
#r @"bin\Debug\Demo.TypeProvider05.dll"

[<Literal>]
let namespaces = "Demo.GPUTypes"
    
type GPUHelper = Demo.TypeProvider05.GPUHelper<namespaces>

open System.Reflection
open Alea.CUDA
open Alea.CUDA.Utilities

let test () =
    let template = cuda {
        let! kernel1 =
            <@ fun (output:deviceptr<float>) (first:deviceptr<float>) (second:deviceptr<float>) ->
                let tid = threadIdx.x
                output.[tid] <- first.[tid] + second.[tid] @>
            |> Compiler.DefineKernel
            
        let! kernel2 =
            <@ fun (output:deviceptr<float>) (input:GPUHelper.Demo.GPUTypes.PairHelper.Seq) ->
                let tid = threadIdx.x
                output.[tid] <- input.First.[tid] + input.Second.[tid] @>
            |> Compiler.DefineKernel
            
        return Entry(fun program ->
            let worker = program.Worker
            let kernel1 = program.Apply kernel1

            let run1() =
                let n = 512
                let first = Array.init n (TestUtil.genRandomDouble -100.0 100.0)
                let second = Array.init n (TestUtil.genRandomDouble -50.0 50.0)
                let hOutput = (first, second) ||> Array.map2 ( + )
                
                use first = worker.Malloc(first)
                use second = worker.Malloc(second)
                use dOutput = worker.Malloc<float>(n)
                let pair = GPUHelper.Demo.GPUTypes.PairHelper.Seq(n, first.Ptr, second.Ptr)
                
                let lp = LaunchParam(1, pair.Length)
                kernel1.Launch lp dOutput.Ptr pair.First pair.Second
                let dOutput = dOutput.Gather()
                
                printfn "%A" dOutput

            let run() =
                run1()

            run ) }

    use program = template |> Compiler.load Worker.Default
    program.Run()
    
test()