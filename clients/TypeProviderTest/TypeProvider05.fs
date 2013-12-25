module TypeProviderTest.TypeProvider05

open Alea.CUDA
open Alea.CUDA.Utilities
open NUnit.Framework

[<Literal>]
let namespaces = "Demo.GPUTypes"

type GPUHelper = Demo.TypeProvider05.HelperProvider<namespaces>
type GPUPair = Demo.GPUTypes.Pair
type GPUPairSeq = GPUHelper.Demo.GPUTypes.PairHelper.Seq
type GPUTriple = Demo.GPUTypes.Triple
type GPUTripleSeq = GPUHelper.Demo.GPUTypes.TripleHelper.Seq

let assertArrayEqual (eps:float option) (A:'T[]) (B:'T[]) =
    (A, B) ||> Array.iter2 (fun a b -> eps |> function
        | None -> Assert.AreEqual(a, b)
        | Some eps -> Assert.That(b, Is.EqualTo(a).Within(eps)))

[<Test>]
let ``___warmup___`` () =
    let template = cuda {
        let! kernel =
            <@ fun (output:deviceptr<float>) (first:deviceptr<float>) (second:deviceptr<float>) ->
                let tid = threadIdx.x
                output.[tid] <- first.[tid] + second.[tid] @>
            |> Compiler.DefineKernel

        return Entry(fun program ->
            let worker = program.Worker
            let kernel = program.Apply kernel

            let run() =
                let n = 512
                let first = Array.init n (TestUtil.genRandomDouble -100.0 100.0)
                let second = Array.init n (TestUtil.genRandomDouble -50.0 50.0)
                let hOutput = (first, second) ||> Array.map2 ( + )
                
                use first = worker.Malloc(first)
                use second = worker.Malloc(second)
                use dOutput = worker.Malloc<float>(n)
                let pair = GPUHelper.Demo.GPUTypes.PairHelper.Seq(n, first.Ptr, second.Ptr)
                
                let lp = LaunchParam(1, pair.Length)
                kernel.Launch lp dOutput.Ptr pair.First pair.Second
                let dOutput = dOutput.Gather()

                assertArrayEqual None hOutput dOutput

            run ) }

    use program = template |> Compiler.load Worker.Default
    program.Run()

[<Test>]
let ``Pair directly`` () =
    let template = cuda {
        let! kernel =
            <@ fun (output:deviceptr<float>) (input:GPUPairSeq) ->
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
                let first = Array.init n (TestUtil.genRandomDouble -100.0 100.0)
                let second = Array.init n (TestUtil.genRandomDouble -50.0 50.0)
                let hOutput = (first, second) ||> Array.map2 ( + )
                
                use first = worker.Malloc(first)
                use second = worker.Malloc(second)
                use dOutput = worker.Malloc<float>(n)
                let pairs = GPUPairSeq(n, first.Ptr, second.Ptr)
                
                let lp = LaunchParam(16, 512)
                kernel.Launch lp dOutput.Ptr pairs
                let dOutput = dOutput.Gather()

                assertArrayEqual None hOutput dOutput

            run ) }

    use program = template |> Compiler.load Worker.Default
    program.Run (1<<<20)
    program.Run (1<<<24)

[<Test>]
let ``Pair blob`` () =
    let template = cuda {
        let! kernel =
            <@ fun (output:deviceptr<float>) (input:GPUPairSeq) ->
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

            let run (logger:ITimingLogger) (n:int) =
                let hInput = Array.init n (fun i ->
                    { Demo.GPUTypes.Pair.First = TestUtil.genRandomDouble -100.0 100.0 i
                      Demo.GPUTypes.Pair.Second = TestUtil.genRandomDouble -50.0 50.0 i })
                let hOutput = hInput |> Array.map (fun pair -> pair.First + pair.Second)

                use blob = new Blob(worker, logger)
                let length, dInput = GPUPairSeq.CreateBlob(blob, hInput)
                let dOutput = blob.CreateArray<float>(length)
                let lp = LaunchParam(16, 512)
                kernel.Launch lp dOutput.Ptr (GPUPairSeq.TriggerBlob(dInput))
                let dOutput = dOutput.Gather()

                assertArrayEqual None hOutput dOutput

            run ) }

    use program = template |> Compiler.load Worker.Default
    let logger = TimingLogger("Blob")
    program.Run logger (1<<<24)
    //program.Run logger (1<<<24)
    logger.DumpLogs()
