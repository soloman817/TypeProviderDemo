#r @"..\Demo.GPUTypes\bin\Debug\Demo.GPUTypes.dll"
#r @"..\Demo.GPUTypes2\bin\Debug\Demo.GPUTypes2.dll"
#r @"bin\Debug\Demo.TypeProvider05.dll"

[<Literal>]
let nss = "Demo.GPUTypes;Demo.GPUTypes2"
    
type GPUHelper = Demo.TypeProvider05.GPUHelper<nss>
