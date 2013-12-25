#r @"..\packages\Alea.cuBase.1.2.723\lib\net40\Alea.CUDA.dll"
#r @"..\Demo.GPUTypes\bin\Debug\Demo.GPUTypes.dll"
#r @"..\Demo.GPUTypes2\bin\Debug\Demo.GPUTypes2.dll"
#r @"bin\Debug\Demo.TypeProvider05.dll"

[<Literal>]
let namespaces = "Demo.GPUTypes"
    
type GPUHelper = Demo.TypeProvider05.HelperProvider<namespaces>


open System.Reflection
open Alea.CUDA
open Alea.CUDA.Utilities

let test() =
    let template = cuda {
        let! kernel =
            <@ fun (output:deviceptr<float>) (input:GPUHelper.Demo.GPUTypes.PairHelper.Seq) ->
                let start = blockIdx.x * blockDim.x + threadIdx.x
                let stride = gridDim.x * blockDim.x
                let mutable i = start
                while i < input.Length do
                    output.[i] <- input.First.[i] + input.Second.[i]
                    i <- i + stride @>
            |> Compiler.DefineKernel

        return Entry(fun program ->
            let worker = program.Worker
            let kernel = program.Apply kernel

            let run (n:int) =
                let hInput = Array.init n (fun i ->
                    { Demo.GPUTypes.Pair.First = TestUtil.genRandomDouble -100.0 100.0 i
                      Demo.GPUTypes.Pair.Second = TestUtil.genRandomDouble -50.0 50.0 i })
                let hOutput = hInput |> Array.map (fun pair -> pair.First + pair.Second)

                use blob = new Blob(worker)
                let dInput = GPUHelper.CreateSeqBlob(blob, hInput)
//                
//                
//                use first = worker.Malloc(first)
//                use second = worker.Malloc(second)
//                use dOutput = worker.Malloc<float>(n)
//                let pairs = GPUHelper.Demo.GPUTypes.PairHelper.Seq(n, first.Ptr, second.Ptr)
//                
//                let lp = LaunchParam(16, 512)
//                kernel.Launch lp dOutput.Ptr pairs
//                let dOutput = dOutput.Gather()
//
//                assertArrayEqual None hOutput dOutput
                ()

            run ) }

    use program = template |> Compiler.load Worker.Default
    program.Run 100
    
test()