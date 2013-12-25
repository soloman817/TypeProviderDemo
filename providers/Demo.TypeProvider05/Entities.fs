module Demo.TypeProvider05.Entities

open System
open System.IO
open System.Reflection
open System.Collections.Generic
open System.Text.RegularExpressions
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.DerivedPatterns
open Microsoft.FSharp.Quotations.ExprShape
open Alea.CUDA
open Alea.CUDA.Utilities
open Samples.FSharp.ProvidedTypes
open Demo.Common
open Demo.TypeProvider05.Framework

type HelperTypeAttribute() =
    inherit Attribute()

    interface ICustomTypeBuilder with
        member this.Build(ctx, clrType) =
            let irFields =
                clrType.GetFields(BindingFlags.NonPublic ||| BindingFlags.Instance)
                |> Array.map (fun info ->
                    let irFieldName = info.Name.Substring(1)
                    let irFieldType = IRTypeBuilder.Instance.Build(ctx, info.FieldType)
                    irFieldName, irFieldType)

            let param = IRStructOrUnionBuildingParam.Create(irFields)
            let irStructType = IRStructType.Create(ctx.IRContext, param, IRRefTypeHint.Default)
            irStructType |> Some

    interface ICustomToUnmanagedMarshaler with
        member this.Marshal(irType, clrObject, buffer) =
            let clrType = clrObject.GetType()
            let irFieldInfos = irType.Struct.FieldInfos
            let irFieldLayouts = irType.Struct.FieldLayouts

            (irFieldInfos, irFieldLayouts) ||> Array.iter2 (fun irFieldInfo irFieldLayout ->
                let clrField = clrType.GetProperty(irFieldInfo.Name).GetGetMethod().Invoke(clrObject, Array.empty)
                let buffer = buffer + (irFieldLayout.Offset |> nativeint)
                ToUnmanagedMarshaler.Instance.Marshal(irFieldInfo.FieldType, clrField, buffer))

            Some()

[<HelperType>]
type HelperBase() = class end

type RecordEntity(package:Package, ty:Type) =
    inherit Entity(package, ty)
    let mutable optSeqHelperType : ProvidedTypeDefinition option = None

    let fields = FSharpType.GetRecordFields(ty)

    static member Register(registry:EntityRegistry) =
        let filter (ty:Type) =
            FSharpType.IsRecord(ty)

        let create (package:Package) (ty:Type) =
            RecordEntity(package, ty) :> Entity

        registry.Register(filter, create)

    member this.Fields = fields

    override this.ToString() =
        sprintf "[Record(%d fields)]" fields.Length

    override this.Dump(indent) =
        let indentstr = String.replicate indent " "
        printfn "%s* %s %O" indentstr this.Name this

    override this.TryGenerateHelperType() =
        let helperType = ProvidedTypeDefinition(sprintf "%sHelper" this.Name, Some typeof<obj>, IsErased = false)

        let seqType =
            let ty = ProvidedTypeDefinition("Seq", Some typeof<HelperBase>, IsErased = false)

            let fields = fields |> Array.map (fun field ->
                let fieldName = field.Name
                let fieldType = field.PropertyType
                let fieldType = fieldType |> function
                    | ty when ty = typeof<float> -> typeof<deviceptr<float>>
                    | ty -> failwithf "Field type %O TODO" ty
                fieldName, fieldType)
            let fields = fields |> Array.toList
            let fields = ("Length", typeof<int>) :: fields
            let fields = fields |> List.map (fun (fieldName, fieldType) ->
                let fieldName = sprintf "_%s" fieldName
                ProvidedField(fieldName, fieldType))

            // fields
            ty.AddMembers fields

            // constructor
            let ctor =
                let parameters = fields |> List.map (fun field -> ProvidedParameter(field.Name, field.FieldType))
                let invokeCode (args:Expr list) =
                    match args with
                    | thisExpr :: valueExprs ->
                        (fields, valueExprs) ||> List.fold2 (fun firstExpr field valueExpr ->
                            let secondExpr = Expr.FieldSet(thisExpr, field, valueExpr)
                            Expr.Sequential(firstExpr, secondExpr)) thisExpr
                    | _ -> failwith "Won't happen"
                ProvidedConstructor(parameters, InvokeCode = invokeCode)
            ty.AddMember ctor

            // properties
            fields |> List.iter (fun field ->
                let propertyName = field.Name.Substring(1)
                let propertyType = field.FieldType
                let getterCode (args:Expr list) =
                    let thisExpr = args.[0]
                    Expr.FieldGet(thisExpr, field)
                let property = ProvidedProperty(propertyName, propertyType, GetterCode = getterCode)
                ty.AddMember property)

            ty

        let createSeqBlobMethod =
            let returnType = typeof<int * obj[]>
            let inputType = this.Type.MakeArrayType()
            let parameters = ProvidedParameter("blob", typeof<Alea.CUDA.Utilities.Blob.Blob>) :: ProvidedParameter("input", inputType) :: []

            let invokeCode (args:Expr list) =
                let blobExpr = args.[0]
                let inputExpr = args.[1]

                let lengthExpr = Expr.PropertyGet(inputExpr, inputType.GetProperty("Length"))

                let mapMethodInfo = typeof<unit>.Assembly.GetType("Microsoft.FSharp.Collections.ArrayModule").GetMethod("Map")

                let blobMethodInfo = 
                    let blobType = typeof<Alea.CUDA.Utilities.Blob.Blob>
                    let methodInfos = blobType.GetMethods()
                    let methodInfos = methodInfos |> Array.filter (fun info -> info.Name = "CreateArray")
                    let methodInfos = methodInfos |> Array.filter (fun (info:MethodInfo) ->
                        let parameters = info.GetParameters()
                        parameters.[0].ParameterType.IsArray)
                    methodInfos.[0]

                let fields = fields |> Array.toList

                let lambdaExprs = fields |> List.map (fun field ->
                    let var = Var("x", this.Type)
                    let bodyExpr = Expr.PropertyGet(Expr.Var(var), this.Type.GetProperty(field.Name))
                    Expr.Lambda(var, bodyExpr))

                let mapExprs = (fields, lambdaExprs) ||> List.map2 (fun field lambdaExpr ->
                    let mapMethodInfo = mapMethodInfo.MakeGenericMethod([| this.Type; field.PropertyType |])
                    Expr.Call(mapMethodInfo, lambdaExpr :: inputExpr :: []))

                let blobExprs = (fields, mapExprs) ||> List.map2 (fun field mapExpr ->
                    let blobMethodInfo = blobMethodInfo.MakeGenericMethod([| field.PropertyType |])
                    Expr.Call(blobExpr, blobMethodInfo, mapExpr :: []))

                let varlist = fields |> List.map (fun field ->
                    let name = field.Name
                    let ty = typedefof<Alea.CUDA.Utilities.Blob.BlobArray<_>>.MakeGenericType([|field.PropertyType|])
                    Var(name, ty))
                let varlist = Var("Length", typeof<int>) :: varlist
                let varmap = varlist |> List.map (fun var -> var.Name, var) |> Map.ofList

                let finalExpr =
                    let lengthExpr = Expr.Var(varmap.["Length"])
                    let blobExprs = varlist |> List.map (fun var -> Expr.Coerce(Expr.Var(var), typeof<obj>))
                    let blobExpr = Expr.NewArray(typeof<obj>, blobExprs)
                    Expr.NewTuple(lengthExpr :: blobExpr :: [])

                let lengthBindingExpr = Expr.Let(varmap.["Length"], lengthExpr, finalExpr)

                (fields, blobExprs) ||> List.fold2 (fun bodyExpr field blobExpr ->
                    let var = varmap.[field.Name]
                    Expr.Let(var, blobExpr, bodyExpr)) lengthBindingExpr

            ProvidedMethod("CreateBlob", parameters, returnType, InvokeCode = invokeCode, IsStaticMethod = true)

        seqType.AddMember createSeqBlobMethod

        let triggerSeqBlobMethod =
            let returnType = seqType
            let inputType = typeof<obj[]>
            let parameters = ProvidedParameter("input", inputType) :: []

            let invokeCode (args:Expr list) =
                let inputExpr = args.[0]

                let argExprs = List.init (fields.Length + 1) (fun i -> <@@ (%%inputExpr:obj[]).[i] @@>)
                printfn "%A" argExprs

                let fields = fields |> Array.map (fun field ->
                    let fieldName = field.Name
                    let fieldType = field.PropertyType
                    let fieldType = fieldType |> function
                        | ty when ty = typeof<float> -> typeof<Alea.CUDA.Utilities.Blob.BlobArray<float>>
                        | ty -> failwithf "Field type %O TODO" ty
                    fieldName, fieldType)
                let fields = fields |> Array.toList
                let fields = ("Length", typeof<int>) :: fields

                let argExprs = (fields, argExprs) ||> List.map2 (fun (fieldName, fieldType) argExpr ->
                    let unpackExpr = Expr.Coerce(argExpr, fieldType)
                    match fieldName with
                    | "Length" -> unpackExpr
                    | _ ->
                        let ptrProperty = fieldType.GetProperty("Ptr")
                        Expr.PropertyGet(unpackExpr, ptrProperty))

                let ctor = seqType.GetConstructors().[0]
                Expr.NewObject(ctor, argExprs)

            ProvidedMethod("TriggerBlob", parameters, returnType, InvokeCode = invokeCode, IsStaticMethod = true)

        seqType.AddMember triggerSeqBlobMethod                

        helperType.AddMember seqType

        Some helperType

