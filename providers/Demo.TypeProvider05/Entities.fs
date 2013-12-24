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
open Samples.FSharp.ProvidedTypes
open Demo.Common
open Demo.TypeProvider05.Framework

type GPUTypeBaseAttribute() =
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

[<GPUTypeBaseAttribute>]
type GPUTypeBase() = class end

type RecordEntity(package:Package, ty:Type) =
    inherit Entity(package, ty)

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

    override this.TryGenerateType() =
        let helperType = ProvidedTypeDefinition(sprintf "%sHelper" this.Name, Some typeof<obj>, IsErased = false)

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

        let seqType =
            let ty = ProvidedTypeDefinition("Seq", Some typeof<GPUTypeBase>, IsErased = false)

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

        helperType.AddMember seqType

        Some helperType
