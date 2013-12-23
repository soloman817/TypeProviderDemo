module Demo.TypeProvider05.Provider

open System
open System.IO
open System.Reflection
open System.Collections.Generic
open System.Text.RegularExpressions
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection
open Alea.CUDA
open Alea.CUDA.Utilities
open Samples.FSharp.ProvidedTypes
open Demo.Common

[<assembly: TypeProviderAssembly>]
do ()

let providedNamespace = "Demo.TypeProvider05"

let ( @@ ) a b = Path.Combine(a, b)

type EntityRegistry() =
    let registry = List<(Type -> bool) * (Package -> Type -> Entity)>()

    member this.Register(filter, create) = registry.Add(filter, create)

    member this.TypeFilters = registry |> Seq.map fst

    member this.IsTypeSupported(ty:Type) =
        // TODO: Omit attribute
        this.TypeFilters |> Seq.exists (fun filter -> filter ty)

    member this.IsTypeNotSupported(ty:Type) =
        not <| this.IsTypeSupported(ty)

    member this.CreateEntity(package:Package, ty:Type) =
        let entity = registry |> Seq.tryPick (fun (filter, create) ->
            match filter ty with
            | true -> create package ty |> Some
            | false -> None)
        match entity with
        | Some entity -> entity
        | None -> failwithf "Not supported type %O" ty

and [<AbstractClass>] Entity(package:Package, ty:Type) =
    member this.Package = package
    member this.Type = ty

    abstract Name : string
    default this.Name = ty.Name

    abstract Dump : int -> unit
    default this.Dump(ident) =
        let identstr = String.replicate ident " "
        printfn "%s* %s" identstr this.Name

    abstract WillGenerateSourceCode : bool

    abstract WriteSourceCode : StreamWriter * int -> unit
    default this.WriteSourceCode(file, ident) =
        if this.WillGenerateSourceCode then failwithf "Code generation for %O not implemented." ty

    abstract SeqTypeFullName : string

and Package(parent:Package option, name:string) =
    let children = Dictionary<string, Package>()
    let entities = Dictionary<Type, Entity>()

    member this.IsRootPackage = parent.IsNone
    member this.ParentPackage = match parent with Some parent -> parent | None -> failwith "No parent package."
    member this.Name = name

    member this.RootPackage = parent |> function
        | Some package -> package.RootPackage
        | None -> this

    member this.WillGenerateSourceCode =
        children.Values |> Seq.exists (fun package -> package.WillGenerateSourceCode) ||
        entities.Values |> Seq.exists (fun entity -> entity.WillGenerateSourceCode)

    abstract EntityRegistry : EntityRegistry
    default this.EntityRegistry = this.RootPackage.EntityRegistry

    member this.Namespace = this.IsRootPackage |> function
        | true -> ""
        | false ->
            let parentNamespace = this.ParentPackage.Namespace
            if parentNamespace = "" then name
            else sprintf "%s.%s" parentNamespace name

    member this.GetChildPackage(name:string, create:bool) =
        if children.ContainsKey(name) then children.[name]
        elif create then
            let package = Package(Some this, name)
            children.Add(name, package)
            package
        else failwith "Package not found."

    member this.NamespaceToPackage(ns:string, create:bool) =
        let ns = Regex.Split(ns, "\.") |> List.ofArray
        match ns with
        | [] -> this
        | name :: [] -> this.GetChildPackage(name, create)
        | name :: rest ->
            let package = this.GetChildPackage(name, create)
            let ns = rest |> String.concat "."
            package.NamespaceToPackage(ns, create)

    member private this.GetEntity(ty:Type) = 
        if entities.ContainsKey(ty) then entities.[ty]
        else failwithf "Cannot get entity %O" ty

    member this.FindEntity(ty:Type) =
        if this.IsRootPackage then this.NamespaceToPackage(ty.Namespace, false).GetEntity(ty)
        else this.RootPackage.FindEntity(ty)

    member this.AddEntity(ty:Type) =
        if ty.Namespace <> this.Namespace then failwith "Namespace not match."
        let entity = this.EntityRegistry.CreateEntity(this, ty)
        entities.Add(ty, entity)        

    member this.Dump(?ident:int) =
        let ident = defaultArg ident 0
        let identstr = String.replicate ident " "
        printfn "%s%s" identstr this.Name
        entities.Values |> Seq.iter (fun entity -> entity.Dump(ident + 2))
        children.Values |> Seq.iter (fun package -> package.Dump(ident + 4))

    member this.WriteSourceCode(file:StreamWriter, ident:int) =
        if this.WillGenerateSourceCode then
            let identstr = String.replicate ident " "
            fprintfn file "%spublic sealed class %s" identstr this.Name
            fprintfn file "%s{" identstr

            entities.Values |> Seq.iter (fun entity -> entity.WriteSourceCode(file, ident + 4))
            children.Values |> Seq.iter (fun package -> package.WriteSourceCode(file, ident + 4))

            fprintfn file "%s}" identstr

type BuiltinEntity(package:Package, ty:Type) =
    inherit Entity(package, ty)

    static member Register(registry:EntityRegistry) =
        let filter (ty:Type) =
            match ty with
            | ty when ty = typeof<int> -> true
            | ty when ty = typeof<float> -> true
            | _ -> false

        let create (package:Package) (ty:Type) =
            BuiltinEntity(package, ty) :> Entity

        registry.Register(filter, create)

    override this.WillGenerateSourceCode = false

    override this.SeqTypeFullName =
        match ty with
        | ty when ty = typeof<int> -> "Alea.CUDA.Primitive.deviceptr<int>"
        | ty when ty = typeof<float> -> "Alea.CUDA.Primitive.deviceptr<float>"
        | _ -> failwith "bug"

type RecordEntity(package:Package, ty:Type) =
    inherit Entity(package, ty)

    let fields = FSharpType.GetRecordFields(ty)

    static member Register(registry:EntityRegistry) =
        let filter (ty:Type) =
            FSharpType.IsRecord(ty)

        let create (package:Package) (ty:Type) =
            RecordEntity(package, ty) :> Entity

        registry.Register(filter, create)

    override this.Dump(ident) =
        let identstr = String.replicate ident " "
        printfn "%s* %s [Record(%d fields)]" identstr this.Name fields.Length

    override this.WillGenerateSourceCode = true

    override this.WriteSourceCode(file:StreamWriter, ident:int) =
        let identstr = String.replicate ident " "

        fprintfn file "%spublic sealed class %sHelper" identstr this.Name
        fprintfn file "%s{" identstr

        fprintfn file "%s    public sealed class SeqAttribute : Attribute, Alea.CUDA.Builders.ICustomTypeBuilder" identstr
        fprintfn file "%s    {" identstr
        fprintfn file "%s        FSharpOption<Alea.CUDA.Constructs.IRType> Alea.CUDA.Builders.ICustomTypeBuilder.Build(Alea.CUDA.Contexts.IRModuleBuildingContext ctx, Type clrType)" identstr
        fprintfn file "%s        {" identstr
        fields |> Array.iteri (fun i field ->
            let fieldType = field.PropertyType
            let fieldEntity = package.FindEntity(fieldType)
            fprintfn file "%s            var irField%d = Alea.CUDA.Builders.IRTypeBuilder.Instance.Build(ctx, typeof(%s));" identstr i fieldEntity.SeqTypeFullName)
        fields |> Array.iteri (fun i field ->
            fprintfn file "%s            var irFieldTuple%d = Tuple.Create(%A, irField%d);" identstr i field.Name i)
        fprintfn file "%s            var irFieldTuples = new Tuple<string, Alea.CUDA.Constructs.IRType>[%d] { %s };" identstr fields.Length (fields |> Array.mapi (fun i _ -> sprintf "irFieldTuple%d" i) |> String.concat ", ")
        fprintfn file "%s            var param = Alea.CUDA.Constructs.IRStructOrUnionBuildingParam.Create(irFieldTuples, FSharpOption<Alea.CUDA.Constructs.IRStructOrUnionAlignmentKind>.None, FSharpOption<Alea.CUDA.Constructs.IRStructOrUnionLayoutHint>.None);" identstr
        fprintfn file "%s            var irType = Alea.CUDA.Constructs.IRStructType.Create(ctx.IRContext, param, FSharpOption<Alea.CUDA.Constructs.IRRefTypeHint>.Some(Alea.CUDA.Constructs.IRRefTypeHint.Default));" identstr
        fprintfn file "%s            return FSharpOption<Alea.CUDA.Constructs.IRType>.Some(irType);" identstr
        fprintfn file "%s        }" identstr
        fprintfn file "%s    }" identstr

        fprintfn file "%s    [Seq]" identstr
        fprintfn file "%s    public sealed class Seq" identstr
        fprintfn file "%s    {" identstr

        fields |> Array.iter (fun field ->
            let fieldType = field.PropertyType
            let fieldEntity = package.FindEntity(fieldType)
            fprintfn file "%s        private %s _%s;" identstr fieldEntity.SeqTypeFullName field.Name)

        fprintfn file "%s    }" identstr

        fprintfn file "%s}" identstr

    override this.SeqTypeFullName = sprintf "TODO"

type RootPackage private (name:string) =
    inherit Package(None, name) 

    let registry = EntityRegistry()

    override this.EntityRegistry = registry

    member this.Generate(dllPath:string, cfg:TypeProviderConfig) =
        let sourcePath = Path.ChangeExtension(Path.GetTempFileName(), ".cs")
        do
            use file = new StreamWriter(sourcePath)
            fprintfn file "using System;"
            fprintfn file "using Microsoft.FSharp.Core;"
            fprintfn file "namespace %s" providedNamespace
            fprintfn file "{"
            this.WriteSourceCode(file, 4)
            fprintfn file "}"

        let source = File.ReadAllText(sourcePath)
        printfn "%s" source

        try
            use csc = new Microsoft.CSharp.CSharpCodeProvider()
            let parameters = new System.CodeDom.Compiler.CompilerParameters()
            parameters.OutputAssembly <- dllPath
            parameters.CompilerOptions <- "/t:library"
            parameters.ReferencedAssemblies.Add(Assembly.GetAssembly(typedefof<_ option>).ManifestModule.FullyQualifiedName) |> ignore
            for referenceAssembly in cfg.ReferencedAssemblies do
                parameters.ReferencedAssemblies.Add(referenceAssembly) |> ignore
            let compilerResults = csc.CompileAssemblyFromFile(parameters, [| sourcePath |])
            if compilerResults.Errors.Count > 0 then
                for error in compilerResults.Errors do
                    printfn "%d: %s" error.Line error.ErrorText
                failwith "Compile fail."
            let asm = compilerResults.CompiledAssembly
            asm.GetType(providedNamespace + "." + this.Name)
        finally try File.Delete(sourcePath) with ex -> ()

    static member Create(name:string, namespaces:string[], assemblies:Assembly[]) =
        let namespaces = namespaces |> Set.ofArray
        let rootPackage = RootPackage(name)
        let registry = rootPackage.EntityRegistry

        BuiltinEntity.Register(registry)
        RecordEntity.Register(registry)

        let add (ty:Type) = rootPackage.NamespaceToPackage(ty.Namespace, true).AddEntity(ty)

        [
            typeof<int>
            typeof<float>
        ]
        |> List.iter add

        assemblies
        |> Array.map (fun asm -> asm.GetTypes())
        |> Array.concat
        |> Array.filter (fun ty -> namespaces.Contains(ty.Namespace))
        |> Array.filter rootPackage.EntityRegistry.IsTypeSupported
        |> Array.iter add

        rootPackage

type GPUHelper = class end

[<TypeProvider>]
type Provider(cfg:TypeProviderConfig) =

    do Util.dumpTypeProviderConfig cfg

    let invalidation = new Event<_,_>()
    let cache = Util.Cache2()

    let makeHelpers (nss:string) (name:string) =
        printfn "Generating %s ..." name
        let namespaces = Regex.Split(nss, ";")
        let assemblies = cfg.ReferencedAssemblies |> Array.map Assembly.LoadFile
        let rootPackage = RootPackage.Create(name, namespaces, assemblies)
        rootPackage.Dump()
        let dllPath =
            let name = name.Replace("\"", "'")
            cfg.ResolutionFolder @@ (sprintf "%s.dll" name)
        rootPackage.Generate(dllPath, cfg)

    let providedTypes = [| typeof<GPUHelper> |]
    let staticParameters = [| ProvidedStaticParameter("Namespaces", typeof<string>) :> ParameterInfo |]

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
            staticParameters

        member this.ApplyStaticArguments(typeWithoutArguments, typeNameWithArguments, staticArguments) =
            printfn "ITypeProvider.ApplyStaticArguments(%A, %A, %A)" typeWithoutArguments typeNameWithArguments staticArguments
            let nss = staticArguments.[0] :?> string
            //let name = sprintf "%s_%s" typeNameWithArguments.[typeNameWithArguments.Length-1] (nss.Replace(".", "_"))
            let name = sprintf "%s" typeNameWithArguments.[typeNameWithArguments.Length-1]
            cache.Get name (makeHelpers nss)

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
            providedNamespace

        member this.GetNestedNamespaces() =
            printfn "IProvidedNamespace.GetNestedNamespaces()"
            Array.empty

        member this.GetTypes() =
            printfn "IProvidedNamespace.GetTypes()"
            providedTypes

    interface IDisposable with

        member this.Dispose() =
            printfn "IDisposable.Dispose()"


