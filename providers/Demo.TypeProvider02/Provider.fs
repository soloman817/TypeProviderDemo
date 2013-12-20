module Demo.TypeProvider02.Provider

open System
open System.Reflection
open System.Collections.Generic
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes

[<assembly: TypeProviderAssembly>]
do ()

type VectorBase(data:float[]) =
    member this.Data = data

    member this.Item with get i = data.[i] and set i v = data.[i] <- v

    member this.DotProduct(that:VectorBase) =
        (this.Data, that.Data) ||> Array.map2 ( * ) |> Array.sum
    
    override this.ToString() =
        data
        |> Array.map (sprintf "%f")
        |> String.concat ", "
        |> sprintf "(%s)"

[<TypeProvider>]
type Provider() =
    let invalidation = new Event<_,_>()
    let asm = Assembly.GetExecutingAssembly()
    let ns = "Demo.TypeProvider02"
    let baseType = typeof<VectorBase>

    let makeVectorType (axisNames:string list) =
        let dim = List.length axisNames
        let name = sprintf "Vector%dD" dim

        let ty = ProvidedTypeDefinition(asm, ns, name, baseType = Some baseType)
        ty.AddXmlDocDelayed(fun _ -> sprintf "The erased provided type %s" name)

        ProvidedConstructor(axisNames |> List.map (fun name -> ProvidedParameter(name, typeof<float>)))
        |> ty.AddMember

        ty :> Type

    let axisNames =
        [ [ "X" ]
          [ "X"; "Y" ]
          [ "X"; "Y"; "Z" ]
          [ "X"; "Y"; "Z"; "W" ] ]

    let providedTypes =
        axisNames
        |> List.map makeVectorType
        |> List.toArray
    
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
            Array.empty

        member this.ApplyStaticArguments(typeWithoutArguments, typeNameWithArguments, staticArguments) =
            printfn "ITypeProvider.ApplyStaticArguments(%A, %A, %A)" typeWithoutArguments typeNameWithArguments staticArguments
            typeWithoutArguments

        member this.GetInvokerExpression(syntheticMethodBase, parameters) =
            printfn "ITypeProvider.GetInvokerExpression(%A, %A)" syntheticMethodBase parameters
            let parameters = parameters |> Array.toList
            let numparams = parameters.Length
            match syntheticMethodBase with
            | :? ConstructorInfo ->
                let args = Expr.NewArray(typeof<float>, parameters)
                let ctor = baseType.GetConstructor([| typeof<float[]> |])
                Expr.NewObject(ctor, args :: [])
            | _ -> failwithf "Not Implemented: ITypeProvider.GetInvokerExpression(%A, %A)" syntheticMethodBase parameters

        member this.GetGeneratedAssemblyContents(assembly) =
            printfn "ITypeProvider.GetGeneratedAssemblyContents(%A)" assembly
            printfn "  ReadAllBytes %s" assembly.ManifestModule.FullyQualifiedName
            IO.File.ReadAllBytes assembly.ManifestModule.FullyQualifiedName

        member this.Dispose() =
            printfn "ITypeProvider.Dispose()"

    interface IProvidedNamespace with

        member this.ResolveTypeName(typeName) =
            printfn "IProvidedNamespace.ResolveTypeName(%A)" typeName
            null

        member this.NamespaceName =
            printfn "IProvidedNamespace.NamespaceName.get"
            ns

        member this.GetNestedNamespaces() =
            printfn "IProvidedNamespace.GetNestedNamespaces()"
            Array.empty

        member this.GetTypes() =
            printfn "IProvidedNamespace.GetTypes()"
            providedTypes


