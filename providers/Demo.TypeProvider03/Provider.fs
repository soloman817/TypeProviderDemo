module Demo.TypeProvider03.Provider

open System
open System.Reflection
open System.Collections.Generic
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Samples.FSharp.ProvidedTypes

[<assembly: TypeProviderAssembly>]
do ()

type VectorType = Map<string, float>

[<TypeProvider>]
type Provider(cfg:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    let asm = Assembly.GetExecutingAssembly()
    let ns = "Demo.TypeProvider03"

    let makeVectorType (axisNames:string list) =
        let dim = List.length axisNames
        let name = sprintf "Vector%dD" dim

        let ty = ProvidedTypeDefinition(asm, ns, name, baseType = Some typeof<VectorType>)
        ty.AddXmlDocDelayed(fun _ -> sprintf "The erased provided type %s." name)

        let ctor =
            let parameters = axisNames |> List.map (fun name -> ProvidedParameter(name, typeof<float>))
            let invokeCode (args:Expr list) =
                let pairs = (axisNames, args) ||> List.map2 (fun name arg -> Expr.NewTuple(Expr.Value(name) :: arg :: []))
                let array = Expr.NewArray(typeof<string * float>, pairs)
                <@@ (%%array : (string * float)[]) |> Map.ofArray @@>
            ProvidedConstructor(parameters = parameters, InvokeCode = invokeCode)
        ctor.AddXmlDocDelayed(fun _ -> sprintf "Constructor of %s." name)
        ty.AddMember ctor

        axisNames |> List.iter (fun name ->
            let propertyType = typeof<float>
            let getterCode (args:Expr list) =
                let thisExpr = args.[0]
                let keyExpr = Expr.Value(name)
                <@@ (%%thisExpr : VectorType).[(%%keyExpr : string)] @@>
            let prop = ProvidedProperty(name, propertyType, GetterCode = getterCode)
            prop.AddXmlDocDelayed(fun _ -> sprintf "Axis %s." name)
            ty.AddMember prop)

        let dotProductMethod =
            let name = "DotProduct"
            let parameters = ProvidedParameter("that", ty) :: []
            let returnType = typeof<float>
            let invokeCode (args:Expr list) =
                let thisExpr = args.[0]
                let thatExpr = args.[1]
                <@@ let data1 = (%%thisExpr : VectorType) |> Seq.map (fun pair -> pair.Value)
                    let data2 = (%%thatExpr : VectorType) |> Seq.map (fun pair -> pair.Value)
                    (data1, data2) ||> Seq.map2 ( * ) |> Seq.sum @@>
            ProvidedMethod(name, parameters, returnType, InvokeCode = invokeCode)
        dotProductMethod.AddXmlDocDelayed(fun _ -> "Dot product of two vector.")
        ty.AddMember dotProductMethod

        ty

    let axisNames =
        [ [ "X" ]
          [ "X"; "Y" ]
          [ "X"; "Y"; "Z" ]
          [ "X"; "Y"; "Z"; "W" ] ]

    let providedTypes = axisNames |> List.map makeVectorType

    do this.AddNamespace(ns, providedTypes)