namespace Demo.TypeProvider04

open System
open System.IO
open System.Reflection
open System.Collections.Generic
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation
open ProviderImplementation.ProvidedTypes

[<assembly: TypeProviderAssembly>]
do ()

module Helper =

    let source = """
    namespace GeneratedTypes
    {
        public class Dummy
        {
            private int _Data;

            public int Data
            {
                get
                {
                    return _Data;
                }
            }

            public Dummy(int data)
            {
                this._Data = data;
            }
        }
    }
    """

    let ( @@ ) a b = Path.Combine(a, b)

open Helper

[<TypeProvider>]
type Provider(cfg:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    let ns = "Demo.TypeProvider04"
    let asm = Assembly.GetExecutingAssembly()
    //let asm, replacer = AssemblyResolver.init cfg
    do printfn "%A" cfg.RuntimeAssembly

    do this.RegisterRuntimeAssemblyLocationAsProbingFolder(cfg)

    let dllpath = (Path.GetDirectoryName(cfg.RuntimeAssembly)) @@ "AAA.dll"
    let generateVectorTypes (numVectorTypes:int) =
        use fsc = new Microsoft.CSharp.CSharpCodeProvider()
        printfn "%A" dllpath
        let options = System.CodeDom.Compiler.CompilerParameters([||], dllpath)
        let result = fsc.CompileAssemblyFromSource(options, [| source |])
        if result.Errors.Count <> 0 then
            for i = 0 to result.Errors.Count - 1 do
                printfn "%A: %A" (result.Errors.Item(i).Line) (result.Errors.Item(i).ErrorText)
            failwith "Compile fail"
        printfn "%A" (result.CompiledAssembly.GetTypes())
        //Diagnostics.Debugger.Break()
        ProvidedAssembly.RegisterGenerated(dllpath)

    let generatedAssembly = generateVectorTypes 10
    let providedAssembly = ProvidedAssembly(dllpath)

    let factoryType =
        let ty = ProvidedTypeDefinition(asm, ns, "Vector", Some typeof<obj>, HideObjectMethods=true)
        let parameters = ProvidedStaticParameter("NumberOfVectorTypes", typeof<int>) :: []
        let instantiate (name:string) (b:obj[]) =
            let numVectorTypes = unbox<int> b.[0]
            //let generatedAssembly = generateVectorTypes numVectorTypes
            let ty = ProvidedTypeDefinition(asm, ns, name, Some typeof<obj>)
            ty.AddAssemblyTypesAsNestedTypes(generatedAssembly)
            //providedAssembly.AddTypes(ty::[])
            ty
        ty.DefineStaticParameters(parameters, instantiate)
        //providedAssembly.AddTypes(ty::[])
        ty

    do this.AddNamespace(ns, factoryType :: [])