using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;

namespace ClassLibrary1
{
    public class Class1 : Attribute, Alea.CUDA.Builders.ICustomTypeBuilder
    {
        FSharpOption<Alea.CUDA.Constructs.IRType> Alea.CUDA.Builders.ICustomTypeBuilder.Build(Alea.CUDA.Contexts.IRModuleBuildingContext ctx, Type obj1)
        {
            var irField0 = Alea.CUDA.Builders.IRTypeBuilder.Instance.Build(ctx, typeof(Alea.CUDA.Primitive.deviceptr<float>));
            var irField1 = Alea.CUDA.Builders.IRTypeBuilder.Instance.Build(ctx, typeof(Alea.CUDA.Primitive.deviceptr<float>));
            var irFieldTuple0 = Tuple.Create("First", irField0);
            var irFieldTuple1 = Tuple.Create("Second", irField1);
            var irFieldTuples = new Tuple<string, Alea.CUDA.Constructs.IRType>[2] { irFieldTuple0, irFieldTuple1 };
            var param = Alea.CUDA.Constructs.IRStructOrUnionBuildingParam.Create(irFieldTuples, FSharpOption<Alea.CUDA.Constructs.IRStructOrUnionAlignmentKind>.None, FSharpOption<Alea.CUDA.Constructs.IRStructOrUnionLayoutHint>.None);
            var irType = Alea.CUDA.Constructs.IRStructType.Create(ctx.IRContext, param, FSharpOption<Alea.CUDA.Constructs.IRRefTypeHint>.Some(Alea.CUDA.Constructs.IRRefTypeHint.Default));
            return FSharpOption<Alea.CUDA.Constructs.IRType>.Some(irType);
        }
    }
}
