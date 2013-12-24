module Demo.TypeProvider05.Framework

open System
open System.IO
open System.Collections.Generic
open System.Text.RegularExpressions
open Samples.FSharp.ProvidedTypes
open Demo.Common

type [<Sealed>] EntityRegistry() =
    let registry = List<(Type -> bool) * (Package -> Type -> Entity)>()

    member this.Register(filter, create) = registry.Add(filter, create)

    member this.TypeFilters = registry |> Seq.map fst

    member this.IsTypeSupported(ty:Type) =
        // TODO: Omit attribute
        this.TypeFilters |> Seq.exists (fun filter -> filter ty)

    member this.IsTypeNotSupported(ty:Type) =
        not <| this.IsTypeSupported(ty)

    member this.CreateEntity(package:Package, ty:Type) =
        registry |> Seq.tryPick (fun (filter, create) -> filter ty |> function
            | true -> create package ty |> Some
            | false -> None)
        |> function
        | Some entity -> entity
        | None -> failwithf "Not supported type %O" ty

and [<AbstractClass>] Entity(package:Package, ty:Type) =
    member this.Package = package
    member this.Type = ty

    override this.ToString() = sprintf "[%s]" ty.FullName

    abstract Name : string
    default this.Name = ty.Name

    abstract Dump : int -> unit
    default this.Dump(ident) =
        let indentstr = String.replicate ident " "
        printfn "%s* %s" indentstr this.Name

    abstract TryGenerateType : unit -> ProvidedTypeDefinition option

and Package(parent:Package option, name:string) =
    let children = Dictionary<string, Package>()
    let entities = Dictionary<Type, Entity>()

    member this.IsRoot = parent.IsNone
    member this.Parent = match parent with Some parent -> parent | None -> failwith "No parent package."
    member this.Root = parent |> function Some package -> package.Root | None -> this
    member this.Name = name

    member this.Path = this.IsRoot |> function
        | true -> ""
        | false ->
            let parentPath = this.Parent.Path
            if parentPath = "" then name
            else sprintf "%s.%s" parentPath name

    override this.ToString() = sprintf "[%s]" this.Path

    member private this.GetPackage(name:string, create:bool) =
        if children.ContainsKey(name) then children.[name]
        elif create then
            let package = Package(Some this, name)
            children.Add(name, package)
            package
        else failwithf "Child %A not found in %O." name this

    member private this.FindPackage(path:string list, create:bool) =
        match path with
        | [] -> this
        | name :: [] -> this.GetPackage(name, create)
        | name :: path ->
            let package = this.GetPackage(name, create)
            package.FindPackage(path, create)

    member this.FindPackage(path:string, ?create:bool) =
        let create = defaultArg create false
        let path = Regex.Split(path, @"\.") |> List.ofArray
        this.Root.FindPackage(path, create)

    member private this.GetEntity(ty:Type) = 
        if entities.ContainsKey(ty) then entities.[ty]
        else failwithf "Cannot get entity %O in %O" ty this

    member this.FindEntity(ty:Type) =
        this.FindPackage(ty.Namespace).GetEntity(ty)

    abstract EntityRegistry : EntityRegistry
    default this.EntityRegistry = this.Root.EntityRegistry

    member private this.AddEntity(ty:Type, search:bool) =
        match search with
        | true -> this.FindPackage(ty.Namespace, true).AddEntity(ty, false)
        | false -> 
            if ty.Namespace <> this.Path then failwith "Namespace not match."
            let entity = this.EntityRegistry.CreateEntity(this, ty)
            entities.Add(ty, entity)

    member this.AddEntity(ty:Type) =
        this.AddEntity(ty, true)

    member this.Dump(?indent:int) =
        let indent = defaultArg indent 0
        let indentstr = String.replicate indent " "
        printfn "%s%s" indentstr this.Name
        entities.Values |> Seq.iter (fun entity -> entity.Dump(indent + 2))
        children.Values |> Seq.iter (fun package -> package.Dump(indent + 4))

    abstract TryGenerateType : unit -> ProvidedTypeDefinition option

    member this.GenerateNestedTypes() =
        let packageTypes = children.Values |> Seq.choose (fun package -> package.TryGenerateType()) |> Seq.toList
        let entityTypes = entities.Values |> Seq.choose (fun entity -> entity.TryGenerateType()) |> Seq.toList
        packageTypes @ entityTypes

    default this.TryGenerateType() =
        let nestedTypes = this.GenerateNestedTypes()
        if List.isEmpty nestedTypes then None
        else
            let ty = ProvidedTypeDefinition(name, Some typeof<obj>, IsErased = false)
            ty.AddMembers nestedTypes
            Some ty

    member this.GenerateType() =
        match this.TryGenerateType() with
        | Some ty -> ty
        | None -> failwith "No type generated."
