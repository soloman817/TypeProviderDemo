module Demo.TypeProvider01.Provider

open System
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations

[<assembly: TypeProviderAssembly>]
do ()

let ns = "Demo.TypeProvider01"

let stringParameter index defaultVal =
    { new ParameterInfo() with
        override this.Name with get() = sprintf "axis%d" index
        override this.ParameterType with get() = typeof<string>
        override this.Position with get() = 0
        override this.RawDefaultValue with get() = defaultVal
        override this.DefaultValue with get() = defaultVal
        override this.Attributes with get() = ParameterAttributes.Optional
    }

let makeClass body name =
    let code = sprintf "namespace %s { public class %s {%s%s%s} }" ns name Environment.NewLine body Environment.NewLine
    let dllFile = System.IO.Path.GetTempFileName()
    let csc = new Microsoft.CSharp.CSharpCodeProvider()
    let parameters = new System.CodeDom.Compiler.CompilerParameters()
    parameters.OutputAssembly <- dllFile
    parameters.CompilerOptions <- "/t:library"
    let compilerResults = csc.CompileAssemblyFromSource(parameters, [| code |])
    let asm = compilerResults.CompiledAssembly
    asm.GetType(ns + "." + name)

let makeVector name argnames =
    let propNames =
        argnames
        |> Seq.filter (fun arg -> arg <> null && not (String.IsNullOrWhiteSpace(arg.ToString())))
        |> Seq.map (fun arg -> arg.ToString())
        |> Seq.toList
    let props =
        propNames
        |> List.map (fun arg -> "public double " + arg + " { get; set; }")
        |> String.concat Environment.NewLine
    let dotProductBody =
        propNames
        |> List.map (fun arg -> sprintf "this.%s * other.%s" arg arg)
        |> String.concat " + "
    let dotProduct = sprintf "public double DotProduct(%s other) { return %s; }" name dotProductBody
    let body = props + Environment.NewLine + dotProduct
    makeClass body name
 
type Vector() = class end
 
[<TypeProvider>]
type Provider() =

    let invalidation = new Event<_,_>()
    
    interface ITypeProvider with
        
        [<CLIEvent>]
        member this.Invalidate =
            printfn "ITypeProvider.Invalidate"
            invalidation.Publish

        member this.GetNamespaces() =
            printfn "ITypeProvider.GetNamespaces()"
            [| this |]
        
        member this.GetStaticParameters(typeWithoutArguments) =
            printfn "ITypeProvider.GetStaticParameters(%A)" typeWithoutArguments
            [1..7] |> List.map (fun i -> stringParameter i "") |> List.toArray

        member this.ApplyStaticArguments(typeWithoutArguments, typeNameWithArguments, staticArguments) =
            printfn "ITypeProvider.ApplyStaticArguments(%A, %A, %A)" typeWithoutArguments typeNameWithArguments staticArguments
            makeVector typeNameWithArguments.[typeNameWithArguments.Length-1] staticArguments

        member this.GetInvokerExpression(syntheticMethodBase, parameters) =
            printfn "ITypeProvider.GetInvokerExpression(%A, %A)" syntheticMethodBase parameters
            match syntheticMethodBase with
            | :? ConstructorInfo as ctor -> Expr.NewObject(ctor, Array.toList parameters) 
            | :? MethodInfo as mi -> Expr.Call(parameters.[0], mi, Array.toList parameters.[1..])
            | _ -> failwithf "Not Implemented: ITypeProvider.GetInvokerExpression(%A, %A)" syntheticMethodBase parameters

        member this.GetGeneratedAssemblyContents(assembly) =
            printfn "ITypeProvider.GetGeneratedAssemblyContents(%A)" assembly
            printfn "  ReadAllBytes %s" assembly.ManifestModule.FullyQualifiedName
            IO.File.ReadAllBytes assembly.ManifestModule.FullyQualifiedName

    interface IProvidedNamespace with

        member this.ResolveTypeName(typeName) =
            printfn "IProvidedNamespace.ResolveTypeName(%A)" typeName
            null

        member this.NamespaceName =
            printfn "IProvidedNamespace.NamespaceName"
            ns

        member this.GetNestedNamespaces() =
            printfn "IProvidedNamespace.GetNestedNamespaces()"
            Array.empty

        member this.GetTypes() =
            printfn "IProvidedNamespace.GetTypes()"
            [| typeof<Vector> |]

    interface IDisposable with

        member this.Dispose() =
            printfn "IDisposable.Dispose()"

