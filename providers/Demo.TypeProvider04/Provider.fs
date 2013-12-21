module Demo.TypeProvider04.Provider

open System
open System.Reflection
open System.Collections.Generic
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes
open Demo.Common

[<assembly: TypeProviderAssembly>]
do ()

type VectorType = float[]

[<TypeProvider>]
type Provider(cfg:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    let asm = Assembly.GetExecutingAssembly()
    let ns = "Demo.TypeProvider04"

    let makeVectorType (dim:int) =
        let name = sprintf "Vector%dD" dim
        let axisNames = [ 1 .. dim ] |> List.map (sprintf "Axis%d")
        printfn "Creating %s ..." name

        let ty = ProvidedTypeDefinition(name, Some typeof<VectorType>)
        ty.AddXmlDocDelayed(fun _ -> sprintf "The erased provided type %s." name)

        let ctor =
            let parameters = axisNames |> List.map (fun name -> ProvidedParameter(name, typeof<float>))
            let invokeCode (args:Expr list) = Expr.NewArray(typeof<float>, args) 
            ProvidedConstructor(parameters, InvokeCode = invokeCode)
        ctor.AddXmlDocDelayed(fun _ -> sprintf "Constructor of %s." name)
        ty.AddMember ctor

        axisNames |> List.iteri (fun i name ->
            let propertyType = typeof<float>
            let getterCode (args:Expr list) =
                let thisExpr = args.[0]
                let indexExpr = Expr.Value(i)
                <@@ (%%thisExpr : VectorType).[(%%indexExpr : int)] @@>
            
            let prop = ProvidedProperty(name, propertyType, GetterCode = getterCode)
            prop.AddXmlDocDelayed(fun _ -> sprintf "Axis %s." name)
            ty.AddMember prop
            
            // addition property
            match i with
            | 0 -> ProvidedProperty("X", propertyType, GetterCode = getterCode) |> Some
            | 1 -> ProvidedProperty("Y", propertyType, GetterCode = getterCode) |> Some
            | 2 -> ProvidedProperty("Z", propertyType, GetterCode = getterCode) |> Some
            | 3 -> ProvidedProperty("W", propertyType, GetterCode = getterCode) |> Some
            | _ -> None
            |> Option.iter (fun prop ->
                prop.AddXmlDocDelayed(fun _ -> sprintf "Axis %s." name)
                ty.AddMember prop) )

        let dotProductMethod =
            let name = "DotProduct"
            let parameters = ProvidedParameter("that", ty) :: []
            let returnType = typeof<float>
            let invokeCode (args:Expr list) =
                let thisExpr = args.[0]
                let thatExpr = args.[1]
                <@@ ((%%thisExpr : VectorType), (%%thatExpr : VectorType)) ||> Array.map2 ( * ) |> Array.sum @@>
            ProvidedMethod(name, parameters, returnType, InvokeCode = invokeCode)
        dotProductMethod.AddXmlDocDelayed(fun _ -> "Dot product of two vector.")
        ty.AddMember dotProductMethod

        ty

    let makeVectorSetType (dims:int) (name:string) =
        printfn "Generating %s ..." name
        let vectorTypes = [ 1 .. dims ] |> List.map makeVectorType
        let vectorSetType = ProvidedTypeDefinition(asm, ns, name, Some typeof<obj>)
        vectorSetType.AddMembers vectorTypes
        vectorSetType

    let cache = Util.Cache()

    let factoryType =
        let ty = ProvidedTypeDefinition(asm, ns, "VectorSet", Some typeof<obj>)
        let parameters = ProvidedStaticParameter("Dims", typeof<int>) :: []
        let instantiate (name:string) (parameters:obj[]) =
            let dims = parameters.[0] :?> int
            cache.Get name (makeVectorSetType dims)
        ty.DefineStaticParameters(parameters, instantiate)
        ty

    do this.AddNamespace(ns, factoryType :: [])

