#r @"bin\Debug\Demo.TypeProvider04.dll"

type VectorTypes = Demo.TypeProvider04.Vector<10>
type Dummy = VectorTypes.GeneratedTypes.Dummy

//let a = VectorTypes.GeneratedTypes.Dummy(10)
let a = Dummy(10)

printfn "%A" a
printfn "%A" a.Data