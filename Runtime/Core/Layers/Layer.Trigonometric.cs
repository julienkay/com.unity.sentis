using System;

namespace Unity.Sentis.Layers
{
    /// <summary>
    /// Represents an element-wise `Acos` trigonometric layer: f(x) = acos(x).
    /// </summary>
    class Acos : Activation
    {
        public Acos(int output, int input)
            : base(output, input) { }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Acos(X, O);
        }

        internal override string profilerTag => "Acos";
    }

    /// <summary>
    /// Represents an element-wise `Acosh` trigonometric layer: f(x) = acosh(x).
    /// </summary>
    class Acosh : Activation
    {
        public Acosh(int output, int input)
            : base(output, input) { }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Acosh(X, O);
        }

        internal override string profilerTag => "Acosh";
    }

    /// <summary>
    /// Represents an element-wise `Asin` trigonometric layer: f(x) = asin(x).
    /// </summary>
    class Asin : Activation
    {
        public Asin(int output, int input)
            : base(output, input) { }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Asin(X, O);
        }

        internal override string profilerTag => "Asin";
    }

    /// <summary>
    /// Represents an element-wise `Asinh` trigonometric layer: f(x) = asinh(x).
    /// </summary>
    class Asinh : Activation
    {
        public Asinh(int output, int input)
            : base(output, input) { }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Asinh(X, O);
        }

        internal override string profilerTag => "Asinh";
    }

    /// <summary>
    /// Represents an element-wise `Atan` trigonometric layer: f(x) = atan(x).
    /// </summary>
    class Atan : Activation
    {
        public Atan(int output, int input)
            : base(output, input) { }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Atan(X, O);
        }

        internal override string profilerTag => "Atan";
    }

    /// <summary>
    /// Represents an element-wise `Atanh` trigonometric layer: f(x) = atanh(x).
    /// </summary>
    class Atanh : Activation
    {
        public Atanh(int output, int input)
            : base(output, input) { }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Atanh(X, O);
        }

        internal override string profilerTag => "Atanh";
    }

    /// <summary>
    /// Represents an element-wise `Cos` trigonometric layer: f(x) = cos(x).
    /// </summary>
    class Cos : Activation
    {
        public Cos(int output, int input)
            : base(output, input) { }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Cos(X, O);
        }

        internal override string profilerTag => "Cos";
    }

    /// <summary>
    /// Represents an element-wise `Cosh` trigonometric layer: f(x) = cosh(x).
    /// </summary>
    class Cosh : Activation
    {
        public Cosh(int output, int input)
            : base(output, input) { }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Cosh(X, O);
        }

        internal override string profilerTag => "Cosh";
    }

    /// <summary>
    /// Represents an element-wise `Sin` trigonometric layer: f(x) = sin(x).
    /// </summary>
    class Sin : Activation
    {
        public Sin(int output, int input)
            : base(output, input) { }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Sin(X, O);
        }

        internal override string profilerTag => "Sin";
    }

    /// <summary>
    /// Represents an element-wise `Sinh` trigonometric layer: f(x) = sinh(x).
    /// </summary>
    class Sinh : Activation
    {
        public Sinh(int output, int input)
            : base(output, input) { }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Sinh(X, O);
        }

        internal override string profilerTag => "Sinh";
    }

    /// <summary>
    /// Represents an element-wise `Tan` trigonometric layer: f(x) = tan(x).
    /// </summary>
    class Tan : Activation
    {
        public Tan(int output, int input)
            : base(output, input) { }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            ctx.backend.Tan(X, O);
        }

        internal override string profilerTag => "Tan";
    }
}
