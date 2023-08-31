// This is auto-generated -- do not modify directly

using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using static Unity.Sentis.ComputeTensorData;
using static Unity.Sentis.ShaderPropertyID;

[assembly: InternalsVisibleTo("Unity.Sentis.EditorTests")]

namespace Unity.Sentis {

/// <summary>
/// Represents a GPUCompute backend ops.
/// </summary>
public partial class GPUCommandBufferBackend : CPUBackend
{
    public CommandBuffer cb;

    /// <summary>
    /// Create `GPUCommandBufferBackend`
    /// </summary>
    /// <param name="allocator">allocator</param>
    /// <param name="cb">commandbuffer</param>
    public GPUCommandBufferBackend(CommandBuffer cb, ITensorAllocator allocator = null) : base(allocator) { this.cb = cb; }

    /// <summary>
    /// Initializes and returns an instance of `GPUComputeOps`.
    /// </summary>
    /// <param name="allocator">The allocator to use when allocating tensors.</param>
    public GPUCommandBufferBackend(ITensorAllocator allocator = null)
        : this(new CommandBuffer(), allocator) { }

    /// <inheritdoc/>
    public override Tensor NewTensor(TensorShape shape, DataType dataType, AllocScope scope)
    {
        return m_Allocator.Alloc(shape, dataType, DeviceType.GPU, scope);
    }

    /// <inheritdoc/>
    public override DeviceType deviceType => DeviceType.CPU;

    /// <inheritdoc/>
    public override TensorFloat MatMul2D(TensorFloat X, bool xTranspose, TensorFloat Y, bool yTranspose)
    {
        var Oshape = ShapeInference.Gemm(X.shape, Y.shape, xTranspose, yTranspose);
        if (X.shape.HasZeroDims() || Y.shape.HasZeroDims())
            return ConstantOfShape(Oshape, 0.0f);
        var O = NewOutputTensorFloat(Oshape);

        ScheduleGEMM(Pin(X), Pin(Y), Pin(O, false), O.shape[0], xTranspose ? X.shape[0] : X.shape[1], O.shape[1], xTranspose, yTranspose);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat MatMul(TensorFloat X, TensorFloat Y)
    {
        var Oshape = X.shape.MatMul(Y.shape);
        if (X.shape.HasZeroDims() || Y.shape.HasZeroDims())
            return ConstantOfShape(Oshape, 0.0f);
        var O = NewOutputTensorFloat(Oshape);

        var xShape = X.shape.rank == 1 ? new TensorShape(1, X.shape[0]) : X.shape;
        var yShape = Y.shape.rank == 1 ? new TensorShape(Y.shape[0], 1) : Y.shape;
        var oShape = X.shape.rank > 1 && Y.shape.rank > 1 ? O.shape : xShape.MatMul(yShape);

        var M = xShape[-2];
        var K = xShape[-1];
        var N = yShape[-1];
        var batch = oShape.Length(0, -2);

        if (batch == 1)
        {
            ScheduleGEMM(Pin(X), Pin(Y), Pin(O, false), M, K, N);
            return O;
        }

        if (xShape.Length(0, -2) == batch && yShape.Length(0, -2) == batch)
        {
            BatchedGemm(X, Y, O, batch, M, K, N);
            return O;
        }

        var fn = new ComputeFunc("MatMul");

        unsafe
        {
            var shapeA = stackalloc int[6];
            var stridesA = stackalloc int[6];
            var shapeB = stackalloc int[6];
            var stridesB = stackalloc int[6];
            var shapeO = stackalloc int[6];
            var stridesO = stackalloc int[6];
            OpsUtils.PinMatMulTensorShapeStrides(xShape, yShape, oShape, shapeA, stridesA, shapeB, stridesB, shapeO, stridesO);

            cb.SetInt6(fn, k_ID_shapeA, shapeA);
            cb.SetInt6(fn, k_ID_stridesA, stridesA);
            cb.SetInt6(fn, k_ID_shapeB, shapeB);
            cb.SetInt6(fn, k_ID_stridesB, stridesB);
            cb.SetInt6(fn, k_ID_shapeO, shapeO);
            cb.SetInt6(fn, k_ID_stridesO, stridesO);
        }

        cb.SetInt(fn, k_ID_AM, M);
        cb.SetInt(fn, k_ID_AN, K);
        cb.SetInt(fn, k_ID_BM, K);
        cb.SetInt(fn, k_ID_BN, N);
        cb.SetInt(fn, k_ID_CB, batch);
        cb.SetInt(fn, k_ID_CM, M);
        cb.SetInt(fn, k_ID_CN, N);
        cb.SetInt(fn, k_ID_rank, oShape.rank);

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(Y));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.Dispatch(fn, batch, M, N);

        return O;
    }

    internal void BatchedGemm(TensorFloat X, TensorFloat Y, TensorFloat O, int batch, int M, int K, int N)
    {
        int workItemsX, workItemsY, workItemsZ;
        string kernel;

        if (N % 64 == 0 && K % 16 == 0)
        {
            kernel = "GemmBatched_T16x16_R4x4";
            workItemsX = ComputeHelper.IDivC(N, 4);
            workItemsY = ComputeHelper.IDivC(M, 4);
            workItemsZ = batch;
        }
        else
        {
            kernel = "GemmBatched_T8x8_R4x4";
            workItemsX = ComputeHelper.IDivC(N, 4);
            workItemsY = ComputeHelper.IDivC(M, 4);
            workItemsZ = batch;
        }

        var fn = new ComputeFunc(kernel);

        cb.SetInt(fn, k_ID_maxXIndex, X.shape.length - 1);
        cb.SetInt(fn, k_ID_maxWIndex, Y.shape.length - 1);
        cb.SetInt(fn, k_ID_X_width, K);
        cb.SetInt(fn, k_ID_W_width, N);
        cb.SetInt(fn, k_ID_O_width, N);
        cb.SetInt(fn, k_ID_O_height, M);
        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Wptr, Pin(Y));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        cb.Dispatch(fn, workItemsX, workItemsY, workItemsZ);
    }

    /// <inheritdoc/>
    public override TensorFloat Dense(TensorFloat X, TensorFloat W, TensorFloat B, Layers.FusableActivation fusedActivation)
    {
        var Oshape = B != null ? ShapeInference.Dense(X.shape, W.shape, B.shape) : X.shape.MatMul(W.shape);
        if (X.shape.HasZeroDims() || W.shape.HasZeroDims())
            return ConstantOfShape(Oshape, 0f);

        var O = (fusedActivation != Layers.FusableActivation.None) ? NewTempTensorFloat(Oshape) : NewOutputTensorFloat(Oshape);
        var M = O.shape.Length(0, -1);
        var K = X.shape[-1];
        var N = O.shape[-1];
        if (B != null)
            ScheduleSGEMM(Pin(X), Pin(W), Pin(B), Pin(O, false), M, K, N);
        else
            ScheduleGEMM(Pin(X), Pin(W), Pin(O, false), M, K, N);

        if (fusedActivation != Layers.FusableActivation.None)
            O = ApplyFusedActivation(O, fusedActivation);

        return O;
    }

    Tensor Trilu(Tensor X, int k, string kernel)
    {
        var O = NewOutputTensor(X.shape, X.dataType);
        if (O.shape.HasZeroDims())
            return O;

        // Warning, for some reason shared mem implementation on intel gpu is x2 faster than regular one
        ComputeFunc fn = new ComputeFunc(kernel);
        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_X_width, X.shape[-1]);
        cb.SetInt(fn, k_ID_X_height, X.shape[-2]);
        cb.SetInt(fn, k_ID_X_length, X.shape.length);
        cb.SetInt(fn, k_ID_diagonalK, k);

        cb.Dispatch(fn, ComputeHelper.IDivC(X.shape.length, 4), 1, 1);

        return O;
    }

    /// <inheritdoc/>
    public override Tensor Tril(Tensor X, int k)
    {
        return Trilu(X, k, "Tril");
    }

    /// <inheritdoc/>
    public override Tensor Triu(Tensor X, int k)
    {
        return Trilu(X, k, "Triu");
    }

    TensorFloat ApplyFusedActivation(TensorFloat X, Layers.FusableActivation fusedActivation)
    {
        switch (fusedActivation)
        {
            case Layers.FusableActivation.None:
                return X;
            case Layers.FusableActivation.Relu:
                return Relu(X);
            default:
                throw new NotImplementedException();
        }
    }

    /// <inheritdoc/>
    public override TensorFloat Conv(TensorFloat X, TensorFloat K, TensorFloat B, int groups, Span<int> strides, Span<int> pads, Span<int> dilations, Layers.FusableActivation fusedActivation)
    {
        if (X.shape.rank > 5)
            return base.Conv(X, K, B, groups, strides, pads, dilations, fusedActivation);

        if (X.shape.rank == 4 && K.shape[0] == groups && K.shape[1] == 1)
            return DepthwiseConv2D(X, K, B, groups, strides, pads, dilations, fusedActivation);

        if (groups != 1)
            return GroupedConv(X, K, B, groups, strides, pads, dilations, fusedActivation);

        var Oshape = ShapeInference.Conv(X.shape, K.shape, groups, strides, pads, dilations);
        if (Oshape.HasZeroDims())
            return NewOutputTensorFloat(Oshape);

        var O = NewOutputTensorFloat(Oshape);
        if (ComputeInfo.IsMobileGPU())
        {
            ConvMobile(O, X, K, B, strides, pads, dilations, fusedActivation);
            return O;
        }

        int workItemsX, workItemsY, workItemsZ;

        ComputeFunc fn;
        if (X.shape.rank == 5)
        {
            var n = Oshape[0];
            var k = Oshape[1];
            var d = Oshape[2];
            var h = Oshape[3];
            var w = Oshape[4];

            fn = new ComputeFunc("Conv3D_T16x16_R4x4");
            if(K.shape.Length(2) == 1)
                fn = new ComputeFunc("Conv3D_1x1_T16x16_R4x4");
            cb.SetInt(fn, k_ID_O_depth, O.shape[2]); cb.SetInt(fn, k_ID_O_height, O.shape[3]); cb.SetInt(fn, k_ID_O_width, O.shape[4]);
            cb.SetInt(fn, k_ID_X_depth, X.shape[2]); cb.SetInt(fn, k_ID_X_height, X.shape[3]); cb.SetInt(fn, k_ID_X_width, X.shape[4]);
            cb.SetInt(fn, k_ID_K_depth, K.shape[2]); cb.SetInt(fn, k_ID_K_height, K.shape[3]); cb.SetInt(fn, k_ID_K_width, K.shape[4]);
            cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
            cb.SetTensorAsBuffer(fn, k_ID_Kptr, Pin(K));
            if (B != null)
            {
                cb.EnableKeyword(fn, "USEBIAS");
                cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(B));
            }
            else
            {
                cb.DisableKeyword(fn, "USEBIAS");
            }
            cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
            cb.SetInt(fn, k_ID_O_batch, O.shape[0]); cb.SetInt(fn, k_ID_O_channels, O.shape[1]);
            cb.SetInt(fn, k_ID_X_channels, X.shape[1]);
            cb.SetInt4(fn, k_ID__Stride, strides);
            cb.SetInt4(fn, k_ID__Pad, pads);
            cb.SetInt4(fn, k_ID__Dilation, dilations);
            workItemsX = ComputeHelper.IDivC(k, 4);
            workItemsY = ComputeHelper.IDivC(d * h * w, 4);
            workItemsZ = n;
        }
        // TODO multiplte dispatch + reduce for thin conv
        else if (X.shape.rank == 4)
        {
            var n = Oshape[0];
            var k = Oshape[1];
            var h = Oshape[2];
            var w = Oshape[3];

            workItemsX = ComputeHelper.IDivC(h * w, 4); workItemsY = ComputeHelper.IDivC(k, 8); workItemsZ = n;

            fn = new ComputeFunc("Conv2D_KxK");
            if (K.shape.Length(2) == 1)
            {
                fn = new ComputeFunc("Conv2D_1x1");
            }
            cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
            cb.SetTensorAsBuffer(fn, k_ID_Wptr, Pin(K));
            if (B != null)
            {
                cb.EnableKeyword(fn, "USEBIAS");
                cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(B));
            }
            else
            {
                cb.DisableKeyword(fn, "USEBIAS");
            }
            cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
            cb.SetInt(fn, k_ID_inputChannels, X.shape[1]);
            cb.SetInt(fn, k_ID_inputHeight, X.shape[2]);
            cb.SetInt(fn, k_ID_inputWidth, X.shape[3]);
            cb.SetInt(fn, k_ID_kernelHeight, K.shape[2]);
            cb.SetInt(fn, k_ID_kernelWidth, K.shape[3]);
            cb.SetInt(fn, k_ID_outputChannels, O.shape[1]);
            cb.SetInt(fn, k_ID_outputHeight, O.shape[2]);
            cb.SetInt(fn, k_ID_outputWidth, O.shape[3]);
            cb.SetInt(fn, k_ID_strideHeight, strides[0]);
            cb.SetInt(fn, k_ID_strideWidth, strides[1]);
            cb.SetInt(fn, k_ID_padHeight, pads[0]);
            cb.SetInt(fn, k_ID_padWidth, pads[1]);
            cb.SetInt(fn, k_ID_dilationHeight, dilations != null ? dilations[0] : 1);
            cb.SetInt(fn, k_ID_dilationWidth, dilations != null ? dilations[1] : 1);
            cb.SetInt(fn, k_ID_inputChannelsSize, X.shape[1] * X.shape[2] * X.shape[3]);
            cb.SetInt(fn, k_ID_outputChannelsSize, O.shape[1] * O.shape[2] * O.shape[3]);
            cb.SetInt(fn, k_ID_kernelChannelSize, K.shape[1] * K.shape[2] * K.shape[3]);
            cb.SetInt(fn, k_ID_inputSize, X.shape[2] * X.shape[3]);
            cb.SetInt(fn, k_ID_outputSize, O.shape[2] * O.shape[3]);
        }
        else //if (X.shape.rank == 3)
        {
            var n = Oshape[0];
            var k = Oshape[1];
            var h = Oshape[2];

            workItemsX = ComputeHelper.IDivC(h, 4); workItemsY = ComputeHelper.IDivC(k, 8); workItemsZ = n;

            fn = new ComputeFunc("Conv1D_KxK");
            if (K.shape.Length(2) == 1)
            {
                fn = new ComputeFunc("Conv1D_1x1");
            }
            cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
            cb.SetTensorAsBuffer(fn, k_ID_Wptr, Pin(K));
            if (B != null)
            {
                cb.EnableKeyword(fn, "USEBIAS");
                cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(B));
            }
            else
            {
                cb.DisableKeyword(fn, "USEBIAS");
            }
            cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
            cb.SetInt(fn, k_ID_inputChannels, X.shape[1]);
            cb.SetInt(fn, k_ID_inputHeight, X.shape[2]);
            cb.SetInt(fn, k_ID_kernelHeight, K.shape[2]);
            cb.SetInt(fn, k_ID_outputChannels, O.shape[1]);
            cb.SetInt(fn, k_ID_outputHeight, O.shape[2]);
            cb.SetInt(fn, k_ID_strideHeight, strides[0]);
            cb.SetInt(fn, k_ID_padHeight, pads[0]);
            cb.SetInt(fn, k_ID_dilationHeight, dilations[0]);
            cb.SetInt(fn, k_ID_inputChannelsSize, X.shape[1] * X.shape[2]);
            cb.SetInt(fn, k_ID_outputChannelsSize, O.shape[1] * O.shape[2]);
            cb.SetInt(fn, k_ID_kernelChannelSize, K.shape[1] * K.shape[2]);
            cb.SetInt(fn, k_ID_inputSize, X.shape[2]);
            cb.SetInt(fn, k_ID_outputSize, O.shape[2]);
        }

        if (fusedActivation == Layers.FusableActivation.Relu)
            cb.SetFloat(fn, k_ID__MinValue, 0.0f);
        else
            cb.SetFloat(fn, k_ID__MinValue, float.MinValue);

        cb.Dispatch(fn, workItemsX, workItemsY, workItemsZ);

        return O;
    }

    internal void ConvMobile(TensorFloat O, TensorFloat X, TensorFloat K, TensorFloat B, Span<int> strides, Span<int> pads, Span<int> dilations, Layers.FusableActivation fusedActivation)
    {
        int workItemsX, workItemsY, workItemsZ;

        ComputeFunc fn;
        // TODO regular conv faster for small spatial/channels size, figure good rule of thumb
        // TODO see when to call T8x8
        if (X.shape.rank == 5)
        {
            var n = O.shape[0];
            var k = O.shape[1];
            var d = O.shape[2];
            var h = O.shape[3];
            var w = O.shape[4];

            fn = new ComputeFunc("Conv3D_T16x16_R4x4");
            if (K.shape.Length(2) == 1)
                fn = new ComputeFunc("Conv3D_1x1_T16x16_R4x4");
            cb.SetInt(fn, k_ID_O_depth, O.shape[2]); cb.SetInt(fn, k_ID_O_height, O.shape[3]); cb.SetInt(fn, k_ID_O_width, O.shape[4]);
            cb.SetInt(fn, k_ID_X_depth, X.shape[2]); cb.SetInt(fn, k_ID_X_height, X.shape[3]); cb.SetInt(fn, k_ID_X_width, X.shape[4]);
            cb.SetInt(fn, k_ID_K_depth, K.shape[2]); cb.SetInt(fn, k_ID_K_height, K.shape[3]); cb.SetInt(fn, k_ID_K_width, K.shape[4]);
            workItemsX = ComputeHelper.IDivC(k, 4); workItemsY = ComputeHelper.IDivC(d * h * w, 4); workItemsZ = n;
        }
        else if (X.shape.rank == 4)
        {
            var n = O.shape[0];
            var k = O.shape[1];
            var h = O.shape[2];
            var w = O.shape[3];

            fn = new ComputeFunc("Conv2D_T16x16_R4x4");
            if (K.shape.Length(2) == 1)
                fn = new ComputeFunc("Conv2D_1x1_T16x16_R4x4");
            cb.SetInt(fn, k_ID_O_height, O.shape[2]); cb.SetInt(fn, k_ID_O_width, O.shape[3]);
            cb.SetInt(fn, k_ID_X_height, X.shape[2]); cb.SetInt(fn, k_ID_X_width, X.shape[3]);
            cb.SetInt(fn, k_ID_K_height, K.shape[2]); cb.SetInt(fn, k_ID_K_width, K.shape[3]);
            workItemsX = ComputeHelper.IDivC(k, 4); workItemsY = ComputeHelper.IDivC(h * w, 4); workItemsZ = n;
        }
        else //if (X.shape.rank == 3)
        {
            var n = O.shape[0];
            var k = O.shape[1];
            var w = O.shape[2];

            fn = new ComputeFunc("Conv1D_T16x16_R4x4");
            if (K.shape.Length(2) == 1)
                fn = new ComputeFunc("Conv1D_1x1_T16x16_R4x4");
            cb.SetInt(fn, k_ID_O_width, O.shape[2]);
            cb.SetInt(fn, k_ID_X_width, X.shape[2]);
            cb.SetInt(fn, k_ID_K_width, K.shape[2]);
            workItemsX = ComputeHelper.IDivC(k, 4); workItemsY = ComputeHelper.IDivC(w, 4); workItemsZ = n;
        }

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Kptr, Pin(K));
        if (B != null)
        {
            cb.EnableKeyword(fn, "USEBIAS");
            cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(B));
        }
        else
        {
            cb.DisableKeyword(fn, "USEBIAS");
        }
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_O_batch, O.shape[0]); cb.SetInt(fn, k_ID_O_channels, O.shape[1]);
        cb.SetInt(fn, k_ID_X_channels, X.shape[1]);
        cb.SetInt4(fn, k_ID__Stride, strides);
        cb.SetInt4(fn, k_ID__Pad, pads);
        cb.SetInt4(fn, k_ID__Dilation, dilations);

        if (fusedActivation == Layers.FusableActivation.Relu)
            cb.SetFloat(fn, k_ID__MinValue, 0.0f);
        else
            cb.SetFloat(fn, k_ID__MinValue, float.MinValue);

        cb.Dispatch(fn, workItemsX, workItemsY, workItemsZ);
    }

    /// <inheritdoc/>
    public override TensorFloat ConvTranspose(TensorFloat X, TensorFloat W, TensorFloat B, Span<int> strides, Span<int> pads, Span<int> outputPadding, Layers.FusableActivation fusedActivation)
    {
        if (X.shape.rank > 5)
            return base.ConvTranspose(X, W, B, strides, pads, outputPadding, fusedActivation);

        var Oshape = ShapeInference.ConvTranspose(X.shape, W.shape, strides, pads, outputPadding);
        if (Oshape.HasZeroDims())
            return NewOutputTensorFloat(Oshape);

        if (ComputeInfo.IsMobileGPU() || X.shape.rank > 4)
            return ConvTransposeMobile(X, W, B, strides, pads, outputPadding, fusedActivation);

        var O = NewOutputTensorFloat(Oshape);

        ComputeFunc fn;

        var numSpatialDims = X.shape.rank - 2;

        if (numSpatialDims == 1)
            fn = new ComputeFunc("ConvTranspose1D_KxK");
        else
            fn = new ComputeFunc("ConvTranspose2D_KxK");

        var workItemsX = ComputeHelper.IDivC(Oshape.Length(2), 4);
        var workItemsY = ComputeHelper.IDivC(Oshape[1], 8);
        var workItemsZ = Oshape[0];

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Wptr, Pin(W));
        if (B != null)
        {
            cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(B));
            cb.EnableKeyword(fn, "USEBIAS");
        }
        else
        {
            cb.DisableKeyword(fn, "USEBIAS");
        }
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_inputChannels, X.shape[1]);
        cb.SetInt(fn, k_ID_outputChannels, O.shape[1]);
        cb.SetInt(fn, k_ID_dilationHeight, 1);
        cb.SetInt(fn, k_ID_dilationWidth, 1);

        var kernelSize = W.shape.Length(2);
        var inputSize = X.shape.Length(2);
        var outputSize = O.shape.Length(2);
        cb.SetInt(fn, k_ID_kernelSize, kernelSize);
        cb.SetInt(fn, k_ID_inputSize, inputSize);
        cb.SetInt(fn, k_ID_outputSize, outputSize);
        cb.SetInt(fn, k_ID_inputChannelsSize, X.shape[1] * inputSize);
        cb.SetInt(fn, k_ID_outputChannelsSize, O.shape[1] * outputSize);
        cb.SetInt(fn, k_ID_kernelChannelSize, W.shape[0] * kernelSize);
        cb.SetInt(fn, k_ID_inputWidth, X.shape[-1]);
        cb.SetInt(fn, k_ID_kernelWidth, W.shape[-1]);
        cb.SetInt(fn, k_ID_outputWidth, O.shape[-1]);
        cb.SetInt(fn, k_ID_padWidth, W.shape[-1] - pads[numSpatialDims - 1] - 1);
        cb.SetInt(fn, k_ID_strideWidth, strides[numSpatialDims - 1]);
        if (numSpatialDims > 1)
        {
            cb.SetInt(fn, k_ID_inputHeight, X.shape[-2]);
            cb.SetInt(fn, k_ID_kernelHeight, W.shape[-2]);
            cb.SetInt(fn, k_ID_outputHeight, O.shape[-2]);
            cb.SetInt(fn, k_ID_padHeight, W.shape[-2] - pads[numSpatialDims - 2] - 1);
            cb.SetInt(fn, k_ID_strideHeight, strides[numSpatialDims - 2]);
        }

        if (fusedActivation == Layers.FusableActivation.Relu)
            cb.SetFloat(fn, k_ID__MinValue, 0.0f);
        else
            cb.SetFloat(fn, k_ID__MinValue, float.MinValue);

        cb.Dispatch(fn, workItemsX, workItemsY, workItemsZ);

        return O;
    }

    TensorFloat ConvTransposeMobile(TensorFloat X, TensorFloat W, TensorFloat B, Span<int> stride, Span<int> pad, Span<int> outputAdjustment, Layers.FusableActivation fusedActivation)
    {
        var Oshape = ShapeInference.ConvTranspose(X.shape, W.shape, stride, pad, outputAdjustment);
        if (Oshape.HasZeroDims())
            return NewOutputTensorFloat(Oshape);

        var O = NewOutputTensorFloat(Oshape);

        ComputeFunc fn;

        var numSpatialDims = X.shape.rank - 2;

        if (numSpatialDims == 1)
            fn = new ComputeFunc("ConvTranspose1D_T16x16_R4x4");
        else if (numSpatialDims == 2)
            fn = new ComputeFunc("ConvTranspose2D_T16x16_R4x4");
        else
            fn = new ComputeFunc("ConvTranspose3D_T16x16_R4x4");

        cb.SetInt(fn, k_ID_O_channels, O.shape[1]);
        cb.SetInt(fn, k_ID_X_channels, X.shape[1]);
        cb.SetInt(fn, k_ID_maxXIndex, X.shape.length - 1);
        cb.SetInt(fn, k_ID_maxKIndex, W.shape.length - 1);
        cb.SetInt4(fn, k_ID__Pad, pad);
        cb.SetInt4(fn, k_ID__Stride, stride);

        cb.SetInt(fn, k_ID_O_width, O.shape[-1]);
        cb.SetInt(fn, k_ID_X_width, X.shape[-1]);
        cb.SetInt(fn, k_ID_K_width, W.shape[-1]);

        if (numSpatialDims > 1)
        {
            cb.SetInt(fn, k_ID_O_height, O.shape[-2]);
            cb.SetInt(fn, k_ID_X_height, X.shape[-2]);
            cb.SetInt(fn, k_ID_K_height, W.shape[-2]);
        }

        if (numSpatialDims > 2)
        {
            cb.SetInt(fn, k_ID_O_depth, O.shape[-3]);
            cb.SetInt(fn, k_ID_X_depth, X.shape[-3]);
            cb.SetInt(fn, k_ID_K_depth, W.shape[-3]);
        }

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Kptr, Pin(W));
        if (B != null)
        {
            cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(B));
            cb.SetInt(fn, k_ID_maxBIndex, B.shape.length - 1);
            cb.EnableKeyword(fn, "USEBIAS");
        }
        else
        {
            cb.DisableKeyword(fn, "USEBIAS");
        }
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        var workItemsX = ComputeHelper.IDivC(O.shape[1], 4);
        var workItemsY = ComputeHelper.IDivC(O.shape.Length(2), 4);
        var workItemsZ = O.shape[0];
        if (fusedActivation == Layers.FusableActivation.Relu)
            cb.SetFloat(fn, k_ID__MinValue, 0.0f);
        else
            cb.SetFloat(fn, k_ID__MinValue, float.MinValue);

        cb.Dispatch(fn, workItemsX, workItemsY, workItemsZ);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Resize(TensorFloat X, ReadOnlySpan<float> scale, Layers.InterpolationMode interpolationMode, Layers.NearestMode nearestMode = Layers.NearestMode.RoundPreferFloor, Layers.CoordTransformMode coordTransformMode = Layers.CoordTransformMode.HalfPixel)
    {
        TensorShape Oshape = ShapeInference.Resize(X.shape, scale);
        if (X.shape.rank == 4)
            return Upsample2D(X, Oshape, scale, nearestMode, interpolationMode, coordTransformMode);
        else if (X.shape.rank == 5)
            return Upsample3D(X, Oshape, scale, nearestMode, interpolationMode, coordTransformMode);
        else
            return base.Resize(X, scale, interpolationMode, nearestMode, coordTransformMode);
    }

    TensorFloat Upsample2D(TensorFloat X, TensorShape Oshape, ReadOnlySpan<float> scale, Layers.NearestMode nearestMode, Layers.InterpolationMode interpolationMode, Layers.CoordTransformMode coordTransformMode)
    {
        var O = NewOutputTensorFloat(Oshape);
        if (O.shape.HasZeroDims())
            return O;

        Vector4 scaleXY = Vector4.zero;
        Vector4 biasXY = Vector4.zero;
        for (int i = 0; i < 2; i++)
        {
            OpsUtils.GetScaleAndBias(X.shape[2 + i], O.shape[2 + i], scale[2 + i], coordTransformMode, interpolationMode, nearestMode, out float outputScale, out float outputBias);
            scaleXY[i] = outputScale;
            biasXY[i] = outputBias;
        }

        ComputeFunc fn;
        if (interpolationMode == Layers.InterpolationMode.Nearest)
        {
            string kernelName;
            switch (nearestMode)
            {
                case Layers.NearestMode.RoundPreferFloor:
                case Layers.NearestMode.Ceil:
                    kernelName = "Upsample2D_Nearest_Ceil";
                    break;
                case Layers.NearestMode.RoundPreferCeil:
                case Layers.NearestMode.Floor:
                    kernelName = "Upsample2D_Nearest_Floor";
                    break;
                default:
                    throw new NotImplementedException();
            }
            fn = new ComputeFunc(kernelName);
        }
        else //if (interpolationMode == Layers.InterpolationMode.Linear)
        {
            fn = new ComputeFunc("Upsample2D_Linear_None");
        }

        cb.SetVector(fn, k_ID_scale, scaleXY);
        cb.SetVector(fn, k_ID_bias, biasXY);
        cb.SetInt(fn, k_ID_inHeight, X.shape[2]);
        cb.SetInt(fn, k_ID_inWidth, X.shape[3]);
        cb.SetInt(fn, k_ID_outHeight, O.shape[2]);
        cb.SetInt(fn, k_ID_outWidth,  O.shape[3]);

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        cb.Dispatch(fn, O.shape[0]*O.shape[1], O.shape[2], O.shape[3]);

        return O;
    }

    TensorFloat Upsample3D(TensorFloat X, TensorShape Oshape, ReadOnlySpan<float> scale, Layers.NearestMode nearestMode, Layers.InterpolationMode interpolationMode, Layers.CoordTransformMode coordTransformMode)
    {
        var O = NewOutputTensorFloat(Oshape);
        if (O.shape.HasZeroDims())
            return O;

        Vector4 scaleXYD = Vector4.zero;
        Vector4 biasXYD = Vector4.zero;
        for (int i = 0; i < 3; i++)
        {
            OpsUtils.GetScaleAndBias(X.shape[2 + i], O.shape[2 + i], scale[2 + i], coordTransformMode, interpolationMode, nearestMode, out float outputScale, out float outputBias);
            scaleXYD[i] = outputScale;
            biasXYD[i] = outputBias;
        }

        ComputeFunc fn;
        if (interpolationMode == Layers.InterpolationMode.Nearest)
        {
            string kernelName;
            switch (nearestMode)
            {
                case Layers.NearestMode.RoundPreferFloor:
                case Layers.NearestMode.Ceil:
                    kernelName = "Upsample3D_Nearest_Ceil";
                    break;
                case Layers.NearestMode.RoundPreferCeil:
                case Layers.NearestMode.Floor:
                    kernelName = "Upsample3D_Nearest_Floor";
                    break;
                default:
                    throw new NotImplementedException();
            }
            fn = new ComputeFunc(kernelName);
        }
        else //if (interpolationMode == Layers.InterpolationMode.Linear)
        {
            fn = new ComputeFunc("Upsample3D_Linear_None");
        }

        cb.SetVector(fn, k_ID_scale, scaleXYD);
        cb.SetVector(fn, k_ID_bias, biasXYD);
        cb.SetInt(fn, k_ID_inDepth, X.shape[2]);
        cb.SetInt(fn, k_ID_inHeight, X.shape[3]);
        cb.SetInt(fn, k_ID_inWidth, X.shape[4]);
        cb.SetInt(fn, k_ID_outBatch, O.shape[0]);
        cb.SetInt(fn, k_ID_outChannels, O.shape[1]);
        cb.SetInt(fn, k_ID_outDepth, O.shape[2]);
        cb.SetInt(fn, k_ID_outHeight, O.shape[3]);
        cb.SetInt(fn, k_ID_outWidth,  O.shape[4]);

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        cb.Dispatch(fn, O.shape[2], O.shape[3], O.shape[4]);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat DepthToSpace(TensorFloat X, int blocksize, Layers.DepthToSpaceMode mode)
    {
        var O = NewOutputTensorFloat(ShapeInference.DepthToSpace(X.shape, blocksize));
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc(mode == Layers.DepthToSpaceMode.DepthColumnRow ? "DepthToSpaceDepthColumnRow" : "DepthToSpaceColumnRowDepth");
        cb.SetInt(fn, k_ID_blocksize, blocksize);
        cb.SetInt(fn, k_ID_inputChannels, X.shape[1]);
        cb.SetInt(fn, k_ID_inputHeight, X.shape[2]);
        cb.SetInt(fn, k_ID_inputWidth, X.shape[3]);
        cb.SetInt(fn, k_ID_inputSpatialSize, X.shape[2] * X.shape[3]);
        cb.SetInt(fn, k_ID_inputBatchOffset, X.shape[1] * X.shape[2] * X.shape[3]);
        cb.SetInt(fn, k_ID_outputChannels, O.shape[1]);
        cb.SetInt(fn, k_ID_outputHeight, O.shape[2]);
        cb.SetInt(fn, k_ID_outputWidth, O.shape[3]);
        cb.SetInt(fn, k_ID_outputSpatialSize, O.shape[2] * O.shape[3]);
        cb.SetInt(fn, k_ID_outputBatch, O.shape[0]);
        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        cb.Dispatch(fn, O.shape[0] * O.shape[1], O.shape[2] * O.shape[3], 1);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat SpaceToDepth(TensorFloat X, int blocksize)
    {
        var O = NewOutputTensorFloat(ShapeInference.SpaceToDepth(X.shape, blocksize));
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("SpaceToDepth");
        cb.SetInt(fn, k_ID_blocksize, blocksize);
        cb.SetInt(fn, k_ID_inputChannels, X.shape[1]);
        cb.SetInt(fn, k_ID_inputHeight, X.shape[2]);
        cb.SetInt(fn, k_ID_inputWidth, X.shape[3]);
        cb.SetInt(fn, k_ID_inputSpatialSize, X.shape[2] * X.shape[3]);
        cb.SetInt(fn, k_ID_inputBatchOffset, X.shape[1] * X.shape[2] * X.shape[3]);
        cb.SetInt(fn, k_ID_outputChannels, O.shape[1]);
        cb.SetInt(fn, k_ID_outputHeight, O.shape[2]);
        cb.SetInt(fn, k_ID_outputWidth, O.shape[3]);
        cb.SetInt(fn, k_ID_outputSpatialSize, O.shape[2] * O.shape[3]);
        cb.SetInt(fn, k_ID_outputBatch, O.shape[0]);
        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        cb.Dispatch(fn, O.shape[0] * O.shape[1], O.shape[2] * O.shape[3], 1);

        return O;
    }

    TensorFloat LocalPool1D(TensorFloat X, int[] pool, int[] stride, int[] pad, string kernelName)
    {
        var O = NewOutputTensorFloat(ShapeInference.ApplyPool(X.shape, pool, stride, pad));
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc(kernelName);
        cb.SetInt(fn, k_ID_stride, stride[0]);
        cb.SetInt(fn, k_ID_pad, pad[0]);

        cb.SetInt(fn, k_ID_inHeight, X.shape[2]);

        cb.SetInt(fn, k_ID_pool, pool[0]);

        cb.SetInt(fn, k_ID_outHeight, O.shape[2]);

        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    TensorFloat LocalPool2D(TensorFloat X, int[] pool, int[] stride, int[] pad, string kernelName)
    {
        var O = NewOutputTensorFloat(ShapeInference.ApplyPool(X.shape, pool, stride, pad));
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc(kernelName);
        cb.SetInt(fn, k_ID_strideX, stride[1]);
        cb.SetInt(fn, k_ID_strideY, stride[0]);
        cb.SetInt(fn, k_ID_padX, pad[1]);
        cb.SetInt(fn, k_ID_padY, pad[0]);

        cb.SetInt(fn, k_ID_inHeight, X.shape[2]);
        cb.SetInt(fn, k_ID_inWidth, X.shape[3]);

        cb.SetInt(fn, k_ID_poolX, pool[1]);
        cb.SetInt(fn, k_ID_poolY, pool[0]);

        cb.SetInt(fn, k_ID_outHeight, O.shape[2]);
        cb.SetInt(fn, k_ID_outWidth, O.shape[3]);

        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat MaxPool(TensorFloat X, int[] pool, int[] stride, int[] pad)
    {
        switch (X.shape.rank)
        {
            case 3:
                return LocalPool1D(X, pool, stride, pad, "MaxPool1D");
            case 4:
                return LocalPool2D(X, pool, stride, pad, "MaxPool2D");
            default:
                return base.MaxPool(X, pool, stride, pad);
        }
    }

    /// <inheritdoc/>
    public override TensorFloat AveragePool(TensorFloat X, int[] pool, int[] stride, int[] pad)
    {
        switch (X.shape.rank)
        {
            case 3:
                return LocalPool1D(X, pool, stride, pad, "AveragePool1D");
            case 4:
                return LocalPool2D(X, pool, stride, pad, "AveragePool2D");
            default:
                return base.AveragePool(X, pool, stride, pad);
        }
    }

    void Reduce(Tensor X, Tensor O, int outerLength, int reduceLength, int innerLength, string localKernel, string globalKernel, string fallbackKernel)
    {
        Reduce(X, null, O, outerLength, reduceLength, innerLength, localKernel, globalKernel, fallbackKernel);
    }

    void Reduce(Tensor X, Tensor Xmax, Tensor O, int outerLength, int reduceLength, int innerLength, string localKernel, string globalKernel, string fallbackKernel)
    {
        if (innerLength > (int)ComputeFunc.SafeDispatchLimit || outerLength > (int)ComputeFunc.SafeDispatchLimit)
        {
            var fnUnrolled = new ComputeFunc(fallbackKernel);
            cb.SetInt(fnUnrolled, k_ID_ReducedDim, reduceLength);
            cb.SetInt(fnUnrolled, k_ID_InnerDim, innerLength);
            cb.SetFloat(fnUnrolled, k_ID_Normalization, 1.0f / reduceLength);

            if (Xmax != null)
                cb.ScheduleXBO(fnUnrolled, Pin(X), Pin(Xmax), Pin(O, clearOnInit: false), outerLength * innerLength);
            else
                cb.ScheduleXO(fnUnrolled, Pin(X), Pin(O, clearOnInit: false), outerLength * innerLength);
            return;
        }

        int localReduceLength = reduceLength;
        bool isFirstDispatch = true;

        const int kernelReductionThreadCount = 64 * 4;

        // downsample with pyramid approach
        while (localReduceLength > kernelReductionThreadCount)
        {
            int spatialLengthO = ComputeHelper.IDivC(localReduceLength, kernelReductionThreadCount);
            var Otemp = NewTempTensorFloat(new TensorShape(outerLength * spatialLengthO * innerLength));

            var fnPool = new ComputeFunc(localKernel);
            cb.SetTensorAsBuffer(fnPool, k_ID_Xptr, Pin(X));
            if (Xmax != null)
                cb.SetTensorAsBuffer(fnPool, k_ID_Bptr, Pin(Xmax));
            cb.SetTensorAsBuffer(fnPool, k_ID_Optr, Pin(Otemp, clearOnInit: false));
            cb.SetInt(fnPool, k_ID_ReducedDim, localReduceLength);
            cb.SetInt(fnPool, k_ID_InnerDim, innerLength);
            cb.SetInt(fnPool, k_ID_SpatialDimsO, spatialLengthO);
            cb.SetInt(fnPool, k_ID_IsFirstDispatch, isFirstDispatch ? 1 : 0);

            cb.Dispatch(fnPool, outerLength, ComputeHelper.IDivC(localReduceLength, 4), innerLength);

            X = Otemp;
            localReduceLength = spatialLengthO;
            isFirstDispatch = false;
        }

        var fn = new ComputeFunc(globalKernel);
        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        if (Xmax != null)
            cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(Xmax));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_ReducedDim, localReduceLength);
        cb.SetInt(fn, k_ID_InnerDim, innerLength);
        cb.SetInt(fn, k_ID_IsFirstDispatch, isFirstDispatch ? 1 : 0);
        cb.SetFloat(fn, k_ID_Normalization, 1.0f / reduceLength);

        cb.Dispatch(fn, outerLength, 1, innerLength);
    }

    internal TensorFloat GlobalPool(TensorFloat X, string localKernel, string globalKernel)
    {
        var O = NewOutputTensorFloat(ShapeInference.GlobalPool(X.shape));
        if (O.shape.HasZeroDims())
            return O;

        int globalSpatialDims = X.shape.Length(2);
        int globalNonSpatialLength = X.shape[0] * X.shape[1];

        int localSpatialLength = globalSpatialDims;

        var Oshape = new TensorShape(X.shape[0], X.shape[1], localSpatialLength);

        // downsample with pyramid approach
        while (localSpatialLength > 64*4)
        {
            int spatialLengthO = ComputeHelper.IDivC(localSpatialLength, 64 * 4);
            Oshape[2] = spatialLengthO;
            var Otemp = NewTempTensorFloat(Oshape);

            var fnPool = new ComputeFunc(localKernel);
            cb.SetTensorAsBuffer(fnPool, k_ID_Xptr,  Pin(X));
            cb.SetTensorAsBuffer(fnPool, k_ID_Optr,  Pin(Otemp, clearOnInit: false));
            cb.SetInt(fnPool, k_ID_SpatialDims, localSpatialLength);
            cb.SetInt(fnPool, k_ID_SpatialDimsO, spatialLengthO);

            cb.Dispatch(fnPool, globalNonSpatialLength, ComputeHelper.IDivC(localSpatialLength, 4), 1);

            X = Otemp;
            localSpatialLength = spatialLengthO;
        }

        var fn  = new ComputeFunc(globalKernel);
        cb.SetTensorAsBuffer(fn, k_ID_Xptr,  Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Optr,  Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_SpatialDims, localSpatialLength);
        cb.SetInt(fn, k_ID_GlobalSpatialDims, globalSpatialDims);

        cb.Dispatch(fn, globalNonSpatialLength, 1, 1);
        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat GlobalMaxPool(TensorFloat X)
    {
        return GlobalPool(X, "MaxPoolReduce", "GlobalMaxPool");
    }

    /// <inheritdoc/>
    public override TensorFloat GlobalAveragePool(TensorFloat X)
    {
        return GlobalPool(X, "AveragePoolReduce", "GlobalAveragePool");
    }

    /// <inheritdoc/>
    public override void GlobalAverageVariancePool(TensorFloat O, TensorFloat X, int axis)
    {
        int globalNonSpatialLength = X.shape.Length(0, axis);
        int globalSpatialDims = X.shape.length / globalNonSpatialLength;

        int localSpatialLength = globalSpatialDims;

        var Oshape = new TensorShape(globalNonSpatialLength, localSpatialLength);

        TensorFloat X2 = X; // save a X^2 and do it in the first dispatch
        bool isFirstDispatch = true;

        // downsample with pyramid approach
        while (localSpatialLength > 64 * 4)
        {
            int spatialLengthO = ComputeHelper.IDivC(localSpatialLength, 64 * 4);
            Oshape[-1] = spatialLengthO;
            var Otemp = NewTempTensorFloat(Oshape);
            var O2temp = NewTempTensorFloat(Oshape);

            var fnPool = new ComputeFunc("AverageVariancePoolReduce");
            cb.SetTensorAsBuffer(fnPool, k_ID_Xptr,  Pin(X));
            cb.SetTensorAsBuffer(fnPool, k_ID_X2ptr, Pin(X2));
            cb.SetTensorAsBuffer(fnPool, k_ID_Optr,  Pin(Otemp, clearOnInit: false));
            cb.SetTensorAsBuffer(fnPool, k_ID_O2ptr, Pin(O2temp, clearOnInit: false));
            cb.SetInt(fnPool, k_ID_SpatialDims, localSpatialLength);
            cb.SetInt(fnPool, k_ID_SpatialDimsO, spatialLengthO);
            cb.SetInt(fnPool, k_ID_IsFirstDispatch, isFirstDispatch ? 1 : 0);

            cb.Dispatch(fnPool, globalNonSpatialLength, ComputeHelper.IDivC(localSpatialLength, 4), 1);

            X = Otemp;
            X2 = O2temp;
            localSpatialLength = spatialLengthO;
            isFirstDispatch = false;
        }

        var fn = new ComputeFunc("GlobalAverageVariancePool");
        cb.SetTensorAsBuffer(fn, k_ID_Xptr,  Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_X2ptr, Pin(X2));
        cb.SetTensorAsBuffer(fn, k_ID_Optr,  Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_SpatialDims, localSpatialLength);
        cb.SetInt(fn, k_ID_GlobalSpatialDims, globalSpatialDims);
        cb.SetInt(fn, k_ID_IsFirstDispatch, isFirstDispatch ? 1 : 0);

        cb.Dispatch(fn, globalNonSpatialLength, 1, 1);
    }

    TensorFloat GroupedConv(TensorFloat X, TensorFloat K, TensorFloat B, int groups, Span<int> strides, Span<int> pads, Span<int> dilations, Layers.FusableActivation fusedActivation)
    {
        var Oshape = ShapeInference.Conv(X.shape, K.shape, groups, strides, pads, dilations);
        if (Oshape.HasZeroDims())
            return NewOutputTensorFloat(Oshape);

        var O = (fusedActivation != Layers.FusableActivation.None) ? NewTempTensorFloat(Oshape) : NewOutputTensorFloat(Oshape);

        int outputGroupedChannels = O.shape[1] / groups;

        ComputeFunc fn;

        if (X.shape.rank == 5)
        {
            fn = new ComputeFunc(outputGroupedChannels < 64 ? "GroupedConv3D" : "GroupedConv3D_GroupLower64");
            cb.SetInt(fn, k_ID_O_depth, O.shape[2]); cb.SetInt(fn, k_ID_O_height, O.shape[3]); cb.SetInt(fn, k_ID_O_width, O.shape[4]);
            cb.SetInt(fn, k_ID_X_depth, X.shape[2]); cb.SetInt(fn, k_ID_X_height, X.shape[3]); cb.SetInt(fn, k_ID_X_width, X.shape[4]);
            cb.SetInt(fn, k_ID_K_depth, K.shape[2]); cb.SetInt(fn, k_ID_K_height, K.shape[3]); cb.SetInt(fn, k_ID_K_width, K.shape[4]);
        }
        else if (X.shape.rank == 4)
        {
            fn = new ComputeFunc(outputGroupedChannels < 64 ? "GroupedConv2D" : "GroupedConv2D_GroupLower64");
            cb.SetInt(fn, k_ID_O_height, O.shape[2]); cb.SetInt(fn, k_ID_O_width, O.shape[3]);
            cb.SetInt(fn, k_ID_X_height, X.shape[2]); cb.SetInt(fn, k_ID_X_width, X.shape[3]);
            cb.SetInt(fn, k_ID_K_height, K.shape[2]); cb.SetInt(fn, k_ID_K_width, K.shape[3]);
        }
        else
        {
            fn = new ComputeFunc(outputGroupedChannels < 64 ? "GroupedConv1D" : "GroupedConv1D_GroupLower64");
            cb.SetInt(fn, k_ID_O_width, O.shape[2]);
            cb.SetInt(fn, k_ID_X_width, X.shape[2]);
            cb.SetInt(fn, k_ID_K_width, K.shape[2]);
        }

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Kptr, Pin(K));
        if (B != null)
        {
            cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(B));
            cb.EnableKeyword(fn, "USEBIAS");
        }
        else
        {
            cb.DisableKeyword(fn, "USEBIAS");
        }
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_O_channels, O.shape[1]);
        cb.SetInt(fn, k_ID_X_channels, X.shape[1]);
        cb.SetInt4(fn, k_ID__Stride, strides);
        cb.SetInt4(fn, k_ID__Pad, pads);
        cb.SetInt4(fn, k_ID__Dilation, dilations);
        cb.SetInt(fn, k_ID__Groups, groups);
        cb.SetInt(fn, k_ID_strideX, X.shape.Length(2));
        cb.SetInt(fn, k_ID_strideO, O.shape.Length(2));
        cb.SetInt(fn, k_ID_strideK, K.shape.Length(2));
        cb.SetInt(fn, k_ID_inputGroupedChannels, X.shape[1] / groups);
        cb.SetInt(fn, k_ID_outputGroupedChannels, O.shape[1] / groups);

        cb.Dispatch(fn, ComputeHelper.IDivC(O.shape[1], 4), ComputeHelper.IDivC(O.shape.Length(2), 4), O.shape[0]);

        if (fusedActivation != Layers.FusableActivation.None)
            O = ApplyFusedActivation(O, fusedActivation);

        return O;
    }

    TensorFloat DepthwiseConv2D(TensorFloat X, TensorFloat K, TensorFloat B, int group, Span<int> strides, Span<int> pads, Span<int> dilations, Layers.FusableActivation fusedActivation)
    {
        var O = NewOutputTensorFloat(ShapeInference.Conv(X.shape, K.shape, group, strides, pads, dilations));
        if (O.shape.HasZeroDims())
            return O;

        ComputeFunc fn;
        int workItemsX, workItemsY, workItemsZ;

        if (K.shape[2] == 3 && K.shape[3] == 3 && strides[0] == 1 && strides[1] == 1 && dilations[0] == 1 && dilations[1] == 1)
        {
            var KWE = NewTempTensorFloat(new TensorShape(O.shape[1], 4, 4));

            ComputeFunc fnKE = new ComputeFunc("KernelWinoExpand");
            cb.SetTensorAsBuffer(fnKE, k_ID_Kptr, Pin(K));
            cb.SetTensorAsBuffer(fnKE, k_ID_Optr, Pin(KWE, clearOnInit: false));
            cb.SetInt(fnKE, k_ID_O_channels, O.shape[1]);
            cb.Dispatch(fnKE, O.shape[1], 1, 1);

            fn = new ComputeFunc("DepthwiseConv2DWinograd");

            cb.SetTensorAsBuffer(fn, k_ID_KWEptr, Pin(KWE));

            workItemsX = ComputeHelper.IDivC(O.shape[3], 2);
            workItemsY = ComputeHelper.IDivC(O.shape[2], 2);
            workItemsZ = O.shape[0] * O.shape[1];
        }
        else
        {
            fn = new ComputeFunc("DepthwiseConv2DDirect");

            cb.SetTensorAsBuffer(fn, k_ID_Kptr, Pin(K));

            cb.SetInt(fn, k_ID_K_heightDiv4, ComputeHelper.IDivC(K.shape[2], 4));
            cb.SetInt(fn, k_ID_K_widthDiv4, ComputeHelper.IDivC(K.shape[3], 4));
            cb.SetInt(fn, k_ID_K_height, K.shape[2]);
            cb.SetInt(fn, k_ID_K_width, K.shape[3]);
            cb.SetInt(fn, k_ID_StrideK, K.shape[2] * K.shape[3]);

            workItemsX = O.shape[3];
            workItemsY = O.shape[2];
            workItemsZ = O.shape[0] * O.shape[1];
        }

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        if (B != null)
        {
            cb.EnableKeyword(fn, "USEBIAS");
            cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(B));
        }
        else
        {
            cb.DisableKeyword(fn, "USEBIAS");
        }
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_X_channels, X.shape[1]);
        cb.SetInt(fn, k_ID_X_height, X.shape[2]);
        cb.SetInt(fn, k_ID_X_width, X.shape[3]);
        cb.SetInt(fn, k_ID_O_batch, O.shape[0]);
        cb.SetInt(fn, k_ID_O_channels, O.shape[1]);
        cb.SetInt(fn, k_ID_O_height, O.shape[2]);
        cb.SetInt(fn, k_ID_O_width, O.shape[3]);
        cb.SetInt4(fn, k_ID_Stride, strides);
        cb.SetInt4(fn, k_ID_Pad, pads);
        cb.SetInt4(fn, k_ID_Dilation, dilations);
        cb.SetInt(fn, k_ID_StrideX, X.shape[2] * X.shape[3]);
        cb.SetInt(fn, k_ID_MaxLengthX, X.shape.length - 1);
        cb.SetInt(fn, k_ID_MaxLengthK, K.shape.length - 1);
        cb.SetInt(fn, k_ID_StrideO, O.shape[2] * O.shape[3]);
        cb.SetInt(fn, k_ID_StrideFeaturesO, O.shape[0] * O.shape[1]);

        cb.Dispatch(fn, workItemsX, workItemsY, workItemsZ);

        if (fusedActivation != Layers.FusableActivation.None)
            O = ApplyFusedActivation(O, fusedActivation);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat ScaleBias(TensorFloat X, TensorFloat S, TensorFloat B)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        int batch = X.shape[0];
        int channels = X.shape[1];
        int spatialDims = X.shape.Length(2);

        var fn = new ComputeFunc("ScaleBias");

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Sptr, Pin(S));
        cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(B));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_LengthO, O.shape.length);
        cb.SetInt(fn, k_ID_batch, batch);
        cb.SetInt(fn, k_ID_channels, channels);
        cb.SetInt(fn, k_ID_spatialDims, spatialDims);
        cb.Dispatch(fn, spatialDims, ComputeHelper.IDivC(channels, 4), batch);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat InstanceNormalization(TensorFloat X, TensorFloat S, TensorFloat B, float epsilon)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var reduceOpShape = ShapeInference.GlobalAverageVariancePool(X.shape);
        var meanVariance = NewTempTensorFloat(reduceOpShape);
        GlobalAverageVariancePool(meanVariance, X, 2);

        var fn = new ComputeFunc("InstanceNormalizationTail");

        cb.SetInt(fn, k_ID_channels, X.shape[1]);
        cb.SetInt(fn, k_ID_spatialDims, X.shape.length / (X.shape[0] * X.shape[1]));
        cb.SetFloat(fn, k_ID_epsilon, epsilon);

        cb.ScheduleXSBWO(fn, Pin(X), Pin(S), Pin(B), Pin(meanVariance), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat AxisNormalization(TensorFloat X, TensorFloat S, TensorFloat B, float epsilon)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        int axis = X.shape.Axis(-1);

        var reducedShape = X.shape.Reduce(axis);
        reducedShape[axis] = 2;

        int axisDim = X.shape[axis];
        int outerLength = X.shape.Length(0, -1);

        var meanVariance = NewTempTensorFloat(reducedShape);
        GlobalAverageVariancePool(meanVariance, X, -1);

        var fn = new ComputeFunc("AxisNormalizationTail");

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Wptr, Pin(meanVariance));
        cb.SetTensorAsBuffer(fn, k_ID_Sptr, Pin(S));
        cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(B));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_axisDim, axisDim);
        cb.SetInt(fn, k_ID_outerLength, outerLength);
        cb.SetFloat(fn, k_ID_epsilon, epsilon);
        cb.Dispatch(fn, axisDim, outerLength, 1);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat BatchNormalization(TensorFloat X, TensorFloat S, TensorFloat B, TensorFloat mean, TensorFloat variance, float epsilon)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        int batch = X.shape[0];
        int channels = X.shape[1];
        int spatialDims = X.shape.Length(2);

        var fn = new ComputeFunc("BatchNormalization");

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Wptr, Pin(mean));
        cb.SetTensorAsBuffer(fn, k_ID_Zptr, Pin(variance));
        cb.SetTensorAsBuffer(fn, k_ID_Sptr, Pin(S));
        cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(B));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_LengthO, O.shape.length);
        cb.SetInt(fn, k_ID_batch, batch);
        cb.SetInt(fn, k_ID_channels, channels);
        cb.SetInt(fn, k_ID_spatialDims, spatialDims);
        cb.SetFloat(fn, k_ID_epsilon, epsilon);
        cb.Dispatch(fn, spatialDims, ComputeHelper.IDivC(channels, 4), batch);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Range(float start, float limit, float delta)
    {
        var O = NewOutputTensorFloat(ShapeInference.Range(start, limit, delta));
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("RangeFloat");
        cb.SetFloat(fn, k_ID_rangeStartFloat, start);
        cb.SetFloat(fn, k_ID_rangeDeltaFloat, delta);
        cb.SetInt(fn, k_ID_O_length, O.shape.length);
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.Dispatch(fn, ComputeHelper.IDivC(O.shape.length, 4), 1, 1);

        return O;
    }

    /// <inheritdoc/>
    public override TensorInt Range(int start, int limit, int delta)
    {
        var O = NewOutputTensorInt(ShapeInference.Range(start, limit, delta));
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("RangeInt");
        cb.SetInt(fn, k_ID_rangeStartInt, start);
        cb.SetInt(fn, k_ID_rangeDeltaInt, delta);
        cb.SetInt(fn, k_ID_O_length, O.shape.length);
        cb.SetTensorAsBuffer(fn, k_ID_OIntptr, Pin(O, clearOnInit: false));
        cb.Dispatch(fn, ComputeHelper.IDivC(O.shape.length, 4), 1, 1);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Relu(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Relu");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat PRelu(TensorFloat X, TensorFloat S)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("PRelu");
        unsafe
        {
            cb.SetTensorShapeStrides(fn, k_ID_shapeX, k_ID_stridesX, X.shape);
            cb.SetTensorShapeStrides(fn, k_ID_shapeS, k_ID_stridesS, S.shape);
        }
        cb.SetInt(fn, k_ID_rank, O.shape.rank);

        cb.ScheduleXBO(fn, Pin(X), Pin(S), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Relu6(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Relu6");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat LeakyRelu(TensorFloat X, float alpha)
    {
        Logger.AssertIsTrue(alpha <= 1, "LeakyRelu.ValueError: alpha is supposed to be <= 1, got {0}", alpha);
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("LeakyRelu");
        cb.SetFloat(fn, k_ID_alpha, alpha);
        cb.SetFloat(fn, k_ID_f1, 0.5f * (1f + alpha));
        cb.SetFloat(fn, k_ID_f2, 0.5f * (1f - alpha));
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Tanh(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Tanh");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Softplus(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Softplus");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Sigmoid(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Sigmoid");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat HardSigmoid(TensorFloat X, float alpha, float beta)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("HardSigmoid");
        cb.SetFloat(fn, k_ID_alpha, alpha);
        cb.SetFloat(fn, k_ID_beta, beta);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Elu(TensorFloat X, float alpha)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Elu");
        cb.SetFloat(fn, k_ID_alpha, alpha);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Gelu(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Gelu");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Selu(TensorFloat X, float alpha, float gamma)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Selu");
        cb.SetFloat(fn, k_ID_alpha, alpha);
        cb.SetFloat(fn, k_ID_gamma, gamma);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Swish(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Swish");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Abs(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("AbsFloat");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorInt Abs(TensorInt X)
    {
        var O = NewOutputTensorInt(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("AbsInt");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Neg(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("NegFloat");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorInt Neg(TensorInt X)
    {
        var O = NewOutputTensorInt(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("NegInt");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Ceil(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Ceil");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Clip(TensorFloat X, float min, float max)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Clip");
        cb.SetFloat(fn, k_ID_minV, min);
        cb.SetFloat(fn, k_ID_maxV, max);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Floor(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Floor");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Round(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Round");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Reciprocal(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Reciprocal");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Square(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Square");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Exp(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Exp");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Log(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Log");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Sqrt(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Sqrt");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Acos(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Acos");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Acosh(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Acosh");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Asin(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Asin");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Asinh(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Asinh");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Atan(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Atan");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Atanh(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Atanh");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Cos(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Cos");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Cosh(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Cosh");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Sin(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Sin");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Sinh(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Sinh");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Tan(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Tan");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Erf(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Erf");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Celu(TensorFloat X, float alpha)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Celu");

        cb.SetFloat(fn, k_ID_alpha, alpha);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Shrink(TensorFloat X, float bias, float lambd)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Shrink");

        cb.SetFloat(fn, k_ID_bias, bias);
        cb.SetFloat(fn, k_ID_lambd, lambd);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Softsign(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Softsign");

        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat ThresholdedRelu(TensorFloat X, float alpha)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("ThresholdedRelu");

        cb.SetFloat(fn, k_ID_alpha, alpha);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Softmax(TensorFloat X, int axis)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        // Allocate temp tensors
        int innerLength = X.shape.Strides(axis);
        int outerLength = X.shape.Length(0, axis);
        int reduceLength = X.shape[axis];

        var Xmax = NewTempTensorFloat(new TensorShape(outerLength * innerLength));
        var XexpSums = NewTempTensorFloat(Xmax.shape);

        // x_max = X.max(axis=1)
        // e_x_sum = Sum[exp(x[:,c] - x_max[:]), c]
        Reduce(X, Xmax, outerLength, reduceLength, innerLength, "ReduceMaxFloat", "GlobalReduceMaxFloat", "UnrolledReduceMaxFloat");
        Reduce(X, Xmax, XexpSums, outerLength, reduceLength, innerLength, "ReduceSumExpFloat", "GlobalReduceSumExpFloat", "UnrolledReduceSumExpFloat");

        // exp(x[n,c] - x_max[n]) / e_x_sum[n]
        var fn = new ComputeFunc("SoftmaxEnd");
        cb.SetInt(fn, k_ID_innerLength, innerLength);
        cb.SetInt(fn, k_ID_reduceLength, reduceLength);
        cb.ScheduleXSBO(fn, Pin(X), Pin(XexpSums), Pin(Xmax), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat LogSoftmax(TensorFloat X, int axis)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        // Allocate temp tensors
        int innerLength = X.shape.Strides(axis);
        int outerLength = X.shape.Length(0, axis);
        int reduceLength = X.shape[axis];

        var Xmax = NewTempTensorFloat(new TensorShape(outerLength * innerLength));
        var XexpSums = NewTempTensorFloat(Xmax.shape);

        // x_max = X.max(axis=1)
        // logexp_sum = log(Sum[exp(x[:,c] - x_max[:]), c]) - x_max[:]
        Reduce(X, Xmax, outerLength, reduceLength, innerLength, "ReduceMaxFloat", "GlobalReduceMaxFloat", "UnrolledReduceMaxFloat");
        Reduce(X, Xmax, XexpSums, outerLength, reduceLength, innerLength, "ReduceLogSumExpFloat", "GlobalReduceLogSumExpFloat", "UnrolledReduceLogSumExpFloat");

        // x[n,c] - logexp_sum
        var fn = new ComputeFunc("LogSoftmaxEnd");
        cb.SetInt(fn, k_ID_innerLength, innerLength);
        cb.SetInt(fn, k_ID_reduceLength, reduceLength);
        cb.ScheduleXBO(fn, Pin(X), Pin(XexpSums), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Hardmax(TensorFloat X, int axis)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        //Allocate temp tensors
        var reduceOpShape = X.shape.Reduce(axis);
        var argMax = NewTempTensorFloat(reduceOpShape);

        int offsetReduce = X.shape.Strides(axis);

        // argmax
        {
            var fn = new ComputeFunc("ArgMaxFloatFirst");
            cb.SetInt(fn, k_ID_innerLength, offsetReduce);
            cb.SetInt(fn, k_ID_reduceLength, X.shape[axis]);
            cb.ScheduleXO(fn, Pin(X), Pin(argMax, clearOnInit: false), reduceOpShape.length);
        }
        // one hot from argmax
        {
            var fn = new ComputeFunc("HardmaxEnd");
            cb.SetInt(fn, k_ID_innerLength, offsetReduce);
            cb.SetInt(fn, k_ID_reduceLength, X.shape[axis]);
            cb.ScheduleXBO(fn, Pin(X), Pin(argMax), Pin(O, clearOnInit: false), O.shape.length);
        }

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat CumSum(TensorFloat X, int axis, bool reverse, bool exclusive)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var reduceOpShape = X.shape.Reduce(axis);
        var offsetReduce = X.shape.Strides(axis);

        var fn = new ComputeFunc(reverse ? (exclusive ? "CumSumFloatReverseExclusive" : "CumSumFloatReverseInclusive") : (exclusive ? "CumSumFloatForwardExclusive" : "CumSumFloatForwardInclusive"));
        cb.SetInt(fn, k_ID_innerLength, offsetReduce);
        cb.SetInt(fn, k_ID_reduceLength, X.shape[axis]);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), reduceOpShape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorInt CumSum(TensorInt X, int axis, bool reverse, bool exclusive)
    {
        var O = NewOutputTensorInt(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var reduceOpShape = X.shape.Reduce(axis);
        var offsetReduce = X.shape.Strides(axis);

        var fn = new ComputeFunc(reverse ? (exclusive ? "CumSumIntReverseExclusive" : "CumSumIntReverseInclusive") : (exclusive ? "CumSumIntForwardExclusive" : "CumSumIntForwardInclusive"));
        cb.SetInt(fn, k_ID_innerLength, offsetReduce);
        cb.SetInt(fn, k_ID_reduceLength, X.shape[axis]);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), reduceOpShape.length);

        return O;
    }

    static TensorIndex[] s_operandIndicesOne = new TensorIndex[1];
    static TensorIndex[] s_operandIndicesTwo = new TensorIndex[2];
    static TensorShape[] s_operandShapesOne = new TensorShape[1];
    static TensorShape[] s_operandShapesTwo = new TensorShape[2];

    /// <inheritdoc/>
    public override TensorFloat Einsum(string equation, params TensorFloat[] operands)
    {
        switch (operands.Length)
        {
            case 1:
            {
                s_operandShapesOne[0] = operands[0].shape;
                EinsumHelper.ParseEquationString(equation, s_operandShapesOne, ref s_operandIndicesOne, out var outputIndices, out var outputShape, out var sumIndices, out var sumShape, out var numIndices);

                var fn = new ComputeFunc("EinsumOne");

                unsafe
                {
                    var outStridesA = stackalloc int[TensorShape.maxRank];
                    var sumStridesA = stackalloc int[TensorShape.maxRank];
                    EinsumHelper.PinOperandStrides(operands[0].shape, s_operandIndicesOne[0], outputIndices, sumIndices, outStridesA, sumStridesA);
                    cb.SetInt8(fn, k_ID_outStridesA, outStridesA);
                    cb.SetInt8(fn, k_ID_sumStridesA, sumStridesA);

                    cb.SetTensorShapeStrides(fn, k_ID_outLengths, k_ID_outStrides, outputShape);
                    cb.SetTensorShapeStrides(fn, k_ID_sumLengths, k_ID_sumStrides, sumShape);
                }

                cb.SetInt(fn, k_ID_sumSize, sumShape.length);
                cb.SetInt(fn, k_ID_sumRank, sumShape.rank);
                cb.SetInt(fn, k_ID_outRank, outputShape.rank);

                var O = NewOutputTensorFloat(outputShape);

                cb.ScheduleXO(fn, Pin(operands[0]), Pin(O, clearOnInit: false), outputShape.length);
                return O;
            }
            case 2:
            {
                s_operandShapesTwo[0] = operands[0].shape;
                s_operandShapesTwo[1] = operands[1].shape;
                EinsumHelper.ParseEquationString(equation, s_operandShapesTwo, ref s_operandIndicesTwo, out var outputIndices, out var outputShape, out var sumIndices, out var sumShape, out var numIndices);

                var fn = new ComputeFunc("EinsumTwo");

                unsafe
                {
                    var outStridesA = stackalloc int[TensorShape.maxRank];
                    var sumStridesA = stackalloc int[TensorShape.maxRank];
                    EinsumHelper.PinOperandStrides(operands[0].shape, s_operandIndicesTwo[0], outputIndices, sumIndices, outStridesA, sumStridesA);
                    cb.SetInt8(fn, k_ID_outStridesA, outStridesA);
                    cb.SetInt8(fn, k_ID_sumStridesA, sumStridesA);

                    var outStridesB = stackalloc int[TensorShape.maxRank];
                    var sumStridesB = stackalloc int[TensorShape.maxRank];
                    EinsumHelper.PinOperandStrides(operands[1].shape, s_operandIndicesTwo[1], outputIndices, sumIndices, outStridesB, sumStridesB);
                    cb.SetInt8(fn, k_ID_outStridesB, outStridesB);
                    cb.SetInt8(fn, k_ID_sumStridesB, sumStridesB);

                    cb.SetTensorShapeStrides(fn, k_ID_outLengths, k_ID_outStrides, outputShape);
                    cb.SetTensorShapeStrides(fn, k_ID_sumLengths, k_ID_sumStrides, sumShape);
                }

                cb.SetInt(fn, k_ID_sumSize, sumShape.length);
                cb.SetInt(fn, k_ID_sumRank, sumShape.rank);
                cb.SetInt(fn, k_ID_outRank, outputShape.rank);

                var O = NewOutputTensorFloat(outputShape);

                cb.ScheduleXBO(fn, Pin(operands[0]), Pin(operands[1]), Pin(O, clearOnInit: false), outputShape.length);
                return O;
            }
            default:
                return base.Einsum(equation, operands);
        }
    }

    /// <inheritdoc/>
    public override Tensor Concat(Tensor[] tensors, int axis)
    {
        var O = NewOutputTensor(TensorShapeHelper.ConcatShape(tensors, axis), tensors[0].dataType);
        if (O.shape.HasZeroDims())
            return O;

        unsafe
        {
            // product of all tensor dimensions starting from axis
            var copyBlockLengths = stackalloc int[tensors.Length];
            var copyBlockLengthsAcum = stackalloc int[tensors.Length];
            int copyBlockLengthsSum = 0;
            for (int i = 0; i < tensors.Length; ++i)
            {
                copyBlockLengthsAcum[i] = copyBlockLengthsSum;
                copyBlockLengths[i] = tensors[i].shape.Length(axis);
                copyBlockLengthsSum += copyBlockLengths[i];
            }

            // copy tensor data interleaved into O
            int takes = O.shape.Length(0, axis);
            for (int i = 0; i < tensors.Length; ++i)
            {
                if (tensors[i].shape.HasZeroDims())
                    continue;

                MemCopyStride(tensors[i], O, copyBlockLengths[i], copyBlockLengthsSum, copyBlockLengths[i], takes, 0, copyBlockLengthsAcum[i]);
            }
        }
        return O;
    }

    /// <inheritdoc/>
    public override Tensor Slice(Tensor X, ReadOnlySpan<int> starts, ReadOnlySpan<int> ends, ReadOnlySpan<int> axes, ReadOnlySpan<int> steps)
    {
        var O = NewOutputTensor(X.shape.Slice(starts, ends, axes, steps), X.dataType);

        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Slice");
        unsafe
        {
            cb.SetTensorShapeStrides(fn, k_ID_shapeO, k_ID_stridesO, O.shape);
            cb.SetTensorShapeStrides(fn, k_ID_shapeX, k_ID_stridesX, X.shape);
            var pStarts = stackalloc int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            var pSteps = stackalloc int[8] { 1, 1, 1, 1, 1, 1, 1, 1 };

            for (int i = 0; i < starts.Length; i++)
            {
                int axis = axes != null ? X.shape.Axis(axes[i]) : i;
                int start = Math.Min(starts[i], X.shape[axis]-1);
                start = start < 0 ? X.shape[axis] + start : start;
                int step = steps != null ? steps[i] : 1;
                pStarts[(TensorShape.maxRank - X.shape.rank) + axis] = start;
                pSteps[(TensorShape.maxRank - X.shape.rank) + axis] = step;
            }
            cb.SetInt8(fn, k_ID_starts, pStarts);
            cb.SetInt8(fn, k_ID_steps, pSteps);
        }
        cb.SetInt(fn, k_ID_rank, O.shape.rank);

        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override Tensor Split(Tensor X, int axis, int start, int end)
    {
        axis = X.shape.Axis(axis);
        var O = NewOutputTensor(X.shape.Split(axis, start, end), X.dataType);

        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Split");
        cb.SetInt(fn, k_ID_start, start);
        cb.SetInt(fn, k_ID_lengthO, O.shape.length);
        cb.SetInt(fn, k_ID_strideLower, O.shape.Strides(axis));
        int strideUpperX = axis == 0 ? X.shape.length : X.shape.Strides(axis - 1);
        int strideUpperO = axis == 0 ? O.shape.length : O.shape.Strides(axis - 1);
        cb.SetInt(fn, k_ID_strideUpperX, strideUpperX);
        cb.SetInt(fn, k_ID_strideUpperO, strideUpperO);

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        cb.Dispatch(fn, O.shape.length, 1, 1);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Pad(TensorFloat X, ReadOnlySpan<int> pad, Layers.PadMode padMode, float constant)
    {
        if (padMode != Layers.PadMode.Constant)
            Assert.IsFalse(X.shape.HasZeroDims(), "ValueError: zero dimensions input for Pad operator is not supported");

        var Oshape = X.shape.Pad(pad);
        if (X.shape.HasZeroDims())
            return ConstantOfShape(Oshape, constant);
        var O = NewOutputTensorFloat(Oshape);
        if (O.shape.HasZeroDims())
            return O;

        string padKernel;
        switch (padMode)
        {
            case Layers.PadMode.Constant:
                padKernel = "PadBorderND";
                break;
            case Layers.PadMode.Reflect:
                padKernel = "PadReflectND";
                break;
            case Layers.PadMode.Edge:
                padKernel = "PadEdgeND";
                break;
            case Layers.PadMode.Symmetric:
                padKernel = "PadSymmetricND";
                break;
            default:
                throw new NotImplementedException();
        }

        var fn = new ComputeFunc(padKernel);

        unsafe
        {
            cb.SetTensorShapeStrides(fn, k_ID_shapeO, k_ID_stridesO, O.shape);
            cb.SetTensorShapeStrides(fn, k_ID_shapeX, k_ID_stridesX, X.shape);
            cb.SetInt16(fn, k_ID_pad, pad);
        }
        cb.SetInt(fn, k_ID_rank, X.shape.rank);
        if (padMode == Layers.PadMode.Constant)
            cb.SetFloat(fn, k_ID_Beta, constant);

        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override Tensor Transpose(Tensor X)
    {
        var O = NewOutputTensor(X.shape.Transpose(), X.dataType);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Transpose");
        unsafe
        {
            cb.SetTensorShapeStrides(fn, k_ID_shapeX, k_ID_stridesX, X.shape);

            int* permutations = stackalloc int[TensorShape.maxRank];
            for(int i = 0; i < X.shape.rank; i++)
                permutations[i] = (X.shape.rank-1) - i;
            cb.SetInt8(fn, k_ID_permutations, permutations);
        }
        cb.SetInt(fn, k_ID_rank, X.shape.rank);

        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), X.shape.length);

        return O;
    }

    bool IsTranspose2D(TensorShape X, int[] permutations, out int Height, out int Width)
    {
        Height = 1; Width = 1;
        if (X.length == 1)
            return true;

        var O = X.Transpose(permutations);
        var squeezedO = O.Squeeze();

        unsafe
        {
            var remapDim = stackalloc int[X.rank];
            int index = 0;
            for (int i = 0; i < X.rank; i++)
            {
                if (X[i] == 1)
                    continue;
                remapDim[i] = index;
                index++;
            }

            var squeezedPermutations = stackalloc int[squeezedO.rank];
            index = 0;
            for (int i = 0; i < permutations.Length; i++)
            {
                if (X[i] == 1)
                    continue;
                squeezedPermutations[index] = remapDim[permutations[i]];
                index++;
            }

            int widthO = 1;
            int heightO = squeezedO[0];
            int prevDim = squeezedPermutations[0];
            bool switchedDim = false;

            for (int i = 1; i < squeezedO.rank; i++)
            {
                var dim = squeezedPermutations[i];
                if (!switchedDim && ((prevDim + 1) == dim))
                {
                    prevDim = dim;
                    heightO *= squeezedO[i];
                }
                else if (switchedDim && ((prevDim + 1) != dim))
                {
                    return false;
                }
                else
                {
                    switchedDim = true;
                    prevDim = dim;
                    widthO *= squeezedO[i];
                }
            }

            Height = widthO; Width = heightO;

            return true;
        }
    }

    /// <inheritdoc/>
    public override Tensor Transpose(Tensor X, int[] permutations)
    {
        var O = NewOutputTensor(X.shape.Transpose(permutations), X.dataType);
        if (O.shape.HasZeroDims())
            return O;

        bool is2DTranspose = IsTranspose2D(X.shape, permutations, out int equivalentXH, out int equivalentXW);

        if (is2DTranspose)
        {
            var fn = new ComputeFunc("Transpose2D");
            cb.SetInt(fn, k_ID_X_width, equivalentXW);
            cb.SetInt(fn, k_ID_X_height, equivalentXH);

            cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
            cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

            cb.Dispatch(fn, equivalentXW, equivalentXH, 1);
        }
        else
        {

            var fn = new ComputeFunc("Transpose");
            unsafe
            {
                cb.SetTensorShapeStrides(fn, k_ID_shapeX, k_ID_stridesX, X.shape);
                cb.SetInt8(fn, k_ID_permutations, permutations);
            }
            cb.SetInt(fn, k_ID_rank, X.shape.rank);

            cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), X.shape.length);
        }

        return O;
    }

    void ArgMaxTail(TensorInt O, TensorFloat X, int axis)
    {
        int globalNonSpatialLength = X.shape.Length(0, axis);
        int globalSpatialDims = X.shape.length / globalNonSpatialLength;

        int localSpatialLength = globalSpatialDims;

        var Oshape = new TensorShape(globalNonSpatialLength, localSpatialLength);

        TensorInt Xindices = NewTensorInt(X.shape, AllocScope.InternalToLayer); // save max(X)
        bool isFirstDispatch = true;

        // downsample with pyramid approach
        while (localSpatialLength > 64 * 4)
        {
            int spatialLengthO = ComputeHelper.IDivC(localSpatialLength, 64 * 4);
            Oshape[-1] = spatialLengthO;
            var Otemp = NewTempTensorFloat(Oshape);
            var Oindicestemp = NewTempTensorInt(Oshape);

            var fnPool = new ComputeFunc("ArgMaxReduce");
            cb.SetTensorAsBuffer(fnPool, k_ID_Xptr,  Pin(X));
            cb.SetTensorAsBuffer(fnPool, k_ID_XIndices, Pin(Xindices));
            cb.SetTensorAsBuffer(fnPool, k_ID_Optr,  Pin(Otemp, clearOnInit: false));
            cb.SetTensorAsBuffer(fnPool, k_ID_OIndices, Pin(Oindicestemp, clearOnInit: false));
            cb.SetInt(fnPool, k_ID_SpatialDims, localSpatialLength);
            cb.SetInt(fnPool, k_ID_SpatialDimsO, spatialLengthO);
            cb.SetInt(fnPool, k_ID_IsFirstDispatch, isFirstDispatch ? 1 : 0);

            cb.Dispatch(fnPool, globalNonSpatialLength, ComputeHelper.IDivC(localSpatialLength, 4), 1);

            X = Otemp;
            Xindices = Oindicestemp;
            localSpatialLength = spatialLengthO;
            isFirstDispatch = false;
        }

        var fn = new ComputeFunc("GlobalArgMaxReduce");
        cb.SetTensorAsBuffer(fn, k_ID_Xptr,  Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_XIndices, Pin(Xindices));
        cb.SetTensorAsBuffer(fn, k_ID_OIndices,  Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_SpatialDims, localSpatialLength);
        cb.SetInt(fn, k_ID_IsFirstDispatch, isFirstDispatch ? 1 : 0);

        cb.Dispatch(fn, globalNonSpatialLength, 1, 1);
    }

    /// <inheritdoc/>
    public override TensorInt ArgMax(TensorFloat X, int axis, bool keepdim, bool selectLastIndex = false)
    {
        var Xshape = X.shape;
        var O = NewOutputTensorInt(Xshape.Reduce(axis, keepdim));
        if (O.shape.HasZeroDims())
            return O;

        int dimAxis = Xshape[axis];
        Assert.AreNotEqual(0, dimAxis, "ValueError: zero-size array to reduction operation maximum which has no identity.");

        if (!selectLastIndex && (dimAxis == X.shape.Length(axis)))
        {
            ArgMaxTail(O, X, axis);
            return O;
        }

        var fn = new ComputeFunc(selectLastIndex ? "ArgMaxFloatLast" : "ArgMaxFloatFirst");
        cb.SetInt(fn, k_ID_innerLength, X.shape.Strides(axis));
        cb.SetInt(fn, k_ID_reduceLength, dimAxis);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorInt ArgMax(TensorInt X, int axis, bool keepdim, bool selectLastIndex = false)
    {
        var O = NewOutputTensorInt(X.shape.Reduce(axis, keepdim));
        if (O.shape.HasZeroDims())
            return O;
        Assert.AreNotEqual(0, X.shape[axis], "ValueError: zero-size array to reduction operation maximum which has no identity.");

        var fn = new ComputeFunc(selectLastIndex ? "ArgMaxIntLast" : "ArgMaxIntFirst");
        cb.SetInt(fn, k_ID_innerLength, X.shape.Strides(axis));
        cb.SetInt(fn, k_ID_reduceLength, X.shape[axis]);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorInt ArgMin(TensorFloat X, int axis, bool keepdim, bool selectLastIndex = false)
    {
        var O = NewOutputTensorInt(X.shape.Reduce(axis, keepdim));
        if (O.shape.HasZeroDims())
            return O;
        Assert.AreNotEqual(0, X.shape[axis], "ValueError: zero-size array to reduction operation minimum which has no identity.");

        var fn = new ComputeFunc(selectLastIndex ? "ArgMinFloatLast" : "ArgMinFloatFirst");
        cb.SetInt(fn, k_ID_innerLength, X.shape.Strides(axis));
        cb.SetInt(fn, k_ID_reduceLength, X.shape[axis]);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorInt ArgMin(TensorInt X, int axis, bool keepdim, bool selectLastIndex = false)
    {
        var O = NewOutputTensorInt(X.shape.Reduce(axis, keepdim));
        if (O.shape.HasZeroDims())
            return O;
        Assert.AreNotEqual(0, X.shape[axis], "ValueError: zero-size array to reduction operation minimum which has no identity.");

        var fn = new ComputeFunc(selectLastIndex ? "ArgMinIntLast" : "ArgMinIntFirst");
        cb.SetInt(fn, k_ID_innerLength, X.shape.Strides(axis));
        cb.SetInt(fn, k_ID_reduceLength, X.shape[axis]);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    TensorInt Compare(Tensor A, Tensor B, string kernel)
    {
        var O = NewOutputTensorInt(TensorShapeHelper.BroadcastShape(A, B));
        if (O.shape.HasZeroDims())
            return O;
        var fn = new ComputeFunc(kernel);
        cb.SetTensorShapeStrides(fn, k_ID_shapeO, k_ID_stridesO, O.shape);
        cb.SetTensorShapeStrides(fn, k_ID_shapeA, k_ID_stridesA, A.shape);
        cb.SetTensorShapeStrides(fn, k_ID_shapeB, k_ID_stridesB, B.shape);
        cb.SetInt(fn, k_ID_rank, O.shape.rank);

        cb.ScheduleXBO(fn, Pin(A), Pin(B), Pin(O, clearOnInit: false), O.shape.length);
        return O;
    }

    /// <inheritdoc/>
    public override TensorInt Greater(TensorFloat A, TensorFloat B)
    {
        return Compare(A, B, "Greater");
    }

    /// <inheritdoc/>
    public override TensorInt Greater(TensorInt A, TensorInt B)
    {
        return Compare(A, B, "GreaterInt");
    }

    /// <inheritdoc/>
    public override TensorInt GreaterOrEqual(TensorFloat A, TensorFloat B)
    {
        return Compare(A, B, "GreaterOrEqual");
    }

    /// <inheritdoc/>
    public override TensorInt GreaterOrEqual(TensorInt A, TensorInt B)
    {
        return Compare(A, B, "GreaterOrEqualInt");
    }

    /// <inheritdoc/>
    public override TensorInt Less(TensorFloat A, TensorFloat B)
    {
        return Compare(A, B, "Less");
    }

    /// <inheritdoc/>
    public override TensorInt Less(TensorInt A, TensorInt B)
    {
        return Compare(A, B, "LessInt");
    }

    /// <inheritdoc/>
    public override TensorInt LessOrEqual(TensorFloat A, TensorFloat B)
    {
        return Compare(A, B, "LessOrEqual");
    }

    /// <inheritdoc/>
    public override TensorInt LessOrEqual(TensorInt A, TensorInt B)
    {
        return Compare(A, B, "LessOrEqualInt");
    }

    /// <inheritdoc/>
    public override TensorInt Equal(TensorFloat A, TensorFloat B)
    {
        return Compare(A, B, "Equal");
    }

    /// <inheritdoc/>
    public override TensorInt Equal(TensorInt A, TensorInt B)
    {
        return Compare(A, B, "EqualInt");
    }

    /// <inheritdoc/>
    public override TensorInt Or(TensorInt A, TensorInt B)
    {
        return Compare(A, B, "Or");
    }

    /// <inheritdoc/>
    public override TensorInt And(TensorInt A, TensorInt B)
    {
        return Compare(A, B, "And");
    }

    /// <inheritdoc/>
    public override TensorInt Xor(TensorInt A, TensorInt B)
    {
        return Compare(A, B, "Xor");
    }

    /// <inheritdoc/>
    public override TensorInt Not(TensorInt X)
    {
        var O = NewOutputTensorInt(X.shape);
        if (O.shape.HasZeroDims())
            return O;
        var fn = new ComputeFunc("Not");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);
        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat HardSwish(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;
        var fn = new ComputeFunc("HardSwish");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);
        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat Sign(TensorFloat X)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("SignFloat");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);
        return O;
    }

    /// <inheritdoc/>
    public override TensorInt Sign(TensorInt X)
    {
        var O = NewOutputTensorInt(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("SignInt");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorInt IsInf(TensorFloat X, bool detectNegative, bool detectPositive)
    {
        var O = NewOutputTensorInt(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("IsInf");
        cb.SetBool(fn, k_ID_detectNegative, detectNegative);
        cb.SetBool(fn, k_ID_detectPositive, detectPositive);
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorInt IsNaN(TensorFloat X)
    {
        var O = NewOutputTensorInt(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("IsNaN");
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override Tensor Where(TensorInt C, Tensor A, Tensor B)
    {
        var O = NewOutputTensor(A.shape.Broadcast(B.shape.Broadcast(C.shape)), A.dataType);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Where");
        unsafe
        {
            cb.SetTensorShapeStrides(fn, k_ID_shapeO, k_ID_stridesO, O.shape);
            cb.SetTensorShapeStrides(fn, k_ID_shapeC, k_ID_stridesC, C.shape);
            cb.SetTensorShapeStrides(fn, k_ID_shapeA, k_ID_stridesA, A.shape);
            cb.SetTensorShapeStrides(fn, k_ID_shapeB, k_ID_stridesB, B.shape);
        }
        cb.SetInt(fn, k_ID_rank, O.shape.rank);

        cb.ScheduleXSBO(fn, Pin(C), Pin(A), Pin(B), Pin(O, clearOnInit: false), O.shape.length);
        return O;
    }

    /// <inheritdoc/>
    public override Tensor Tile(Tensor X, ReadOnlySpan<int> repeats)
    {
        var O = NewOutputTensor(X.shape.Tile(repeats), X.dataType);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Tile");
        unsafe
        {
            cb.SetTensorShapeStrides(fn, k_ID_shapeO, k_ID_stridesO, O.shape);
            cb.SetTensorShapeStrides(fn, k_ID_shapeX, k_ID_stridesX, X.shape);
        }
        cb.SetInt(fn, k_ID_rank, O.shape.rank);

        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat ConstantOfShape(TensorShape X, float value)
    {
        var O = NewOutputTensorFloat(X);
        if (O.shape.HasZeroDims())
            return O;
        MemSet(O, math.asint(value));
        return O;
    }

    /// <inheritdoc/>
    public override TensorInt ConstantOfShape(TensorShape X, int value)
    {
        var O = NewOutputTensorInt(X);
        if (O.shape.HasZeroDims())
            return O;
        MemSet(O, value);
        return O;
    }

    /// <inheritdoc/>
    public override Tensor Expand(Tensor X, TensorShape newShape)
    {
        var O = NewOutputTensor(X.shape.Broadcast(newShape), X.dataType);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Expand");
        unsafe
        {
            cb.SetTensorShapeStrides(fn, k_ID_shapeO, k_ID_stridesO, O.shape);
            cb.SetTensorShapeStrides(fn, k_ID_shapeX, k_ID_stridesX, X.shape);
        }
        cb.SetInt(fn, k_ID_rank, O.shape.rank);

        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    protected override Tensor CompressWithIndices(Tensor X, TensorInt indices, int numIndices, int axis)
    {
        var O = NewOutputTensor(ShapeInference.Compress(X.shape, numIndices, axis), X.dataType);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Gather");
        cb.SetInt(fn, k_ID_endLength, X.shape.Strides(axis));
        cb.SetInt(fn, k_ID_indicesLength, numIndices);
        cb.SetInt(fn, k_ID_axisDim, X.shape[axis]);

        cb.ScheduleXBO(fn, Pin(X), Pin(indices), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override Tensor Gather(Tensor X, TensorInt indices, int axis)
    {
        var O = NewOutputTensor(ShapeInference.Gather(X.shape, indices.shape, axis), X.dataType);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("Gather");
        cb.SetInt(fn, k_ID_endLength, X.shape.Strides(axis));
        cb.SetInt(fn, k_ID_indicesLength, indices.shape.length);
        cb.SetInt(fn, k_ID_axisDim, X.shape[axis]);

        cb.ScheduleXBO(fn, Pin(X), Pin(indices), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override Tensor GatherElements(Tensor X, TensorInt indices, int axis)
    {
        var O = NewOutputTensor(indices.shape, X.dataType);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("GatherElements");
        cb.SetInt(fn, k_ID_endLength, X.shape.Strides(axis));
        cb.SetInt(fn, k_ID_startLength, X.shape.Length(0, axis));
        cb.SetInt(fn, k_ID_axisDim, X.shape[axis]);

        cb.ScheduleXBO(fn, Pin(X), Pin(indices), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override Tensor GatherND(Tensor X, TensorInt indices, int batchDims)
    {
        var O = NewOutputTensor(ShapeInference.GatherND(X.shape, indices.shape, batchDims), X.dataType);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("GatherND");
        cb.SetInt(fn, k_ID_rankX, X.shape.rank);
        cb.SetInt(fn, k_ID_rankO, O.shape.rank);
        cb.SetInt(fn, k_ID_rankIndices, indices.shape.rank);
        cb.SetInt(fn, k_ID_iStart, TensorShape.maxRank - O.shape.rank);
        cb.SetInt(fn, k_ID_iEndIndices, TensorShape.maxRank - O.shape.rank + indices.shape.rank - 1);
        cb.SetInt(fn, k_ID_iEndX, TensorShape.maxRank - O.shape.rank + batchDims);
        cb.SetInt(fn, k_ID_iEndMin, TensorShape.maxRank - O.shape.rank + Math.Min(batchDims, indices.shape.rank - 1));
        cb.SetInt(fn, k_ID_iStartB, TensorShape.maxRank - X.shape.rank + batchDims);
        cb.SetInt(fn, k_ID_iEndB, TensorShape.maxRank - X.shape.rank + batchDims + indices.shape[-1]);
        cb.SetTensorShapeStrides(fn, k_ID_shapeO, k_ID_stridesO, O.shape);
        cb.SetTensorShapeStrides(fn, k_ID_shapeX, k_ID_stridesX, X.shape);
        cb.SetTensorShapeStrides(fn, k_ID_shapeIndices, k_ID_stridesIndices, indices.shape);
        cb.ScheduleXBO(fn, Pin(X), Pin(indices), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override Tensor ScatterElements(Tensor X, TensorInt indices, Tensor updates, int axis, Layers.ScatterReductionMode reduction)
    {
        // TODO: The ONNX definition for ScatterElements allows duplicate indices when using the
        // reduction modes, but allowing this introduces race conditions for updating the output
        // tensor. As the current use cases for ScatterElements do not use reductions, fallback
        // to the single-threaded burst cpu implementation.
        if (reduction != Layers.ScatterReductionMode.None)
            return base.ScatterElements(X, indices, updates, axis, reduction);

        var O = NewOutputTensor(X.shape, X.dataType);
        if (O.shape.HasZeroDims())
            return O;

        MemCopy(X, O);

        var fn = new ComputeFunc("ScatterElements");
        cb.SetInt(fn, k_ID_endLength, X.shape.Strides(axis));
        cb.SetInt(fn, k_ID_axisDim, X.shape[axis]);
        cb.SetInt(fn, k_ID_axisDimIndices, indices.shape[axis]);
        cb.SetInt(fn, k_ID_reduction, (int)reduction);

        cb.ScheduleXBO(fn, Pin(updates), Pin(indices), Pin(O, clearOnInit: false), indices.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat ScatterND(TensorFloat X, TensorInt indices, TensorFloat updates, Layers.ScatterReductionMode reduction)
    {
        var O = NewOutputTensorFloat(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        MemCopy(X, O);

        int indexRemapDim = indices.shape[-1];
        int indicesLength = indices.shape.Length(0, -1);
        int updatesLength = updates.shape.length / indicesLength;

        var fn = new ComputeFunc("ScatterNDFloat");
        cb.SetInt(fn, k_ID_updatesLength, updatesLength);
        cb.SetInt(fn, k_ID_indicesLength, indicesLength);
        cb.SetInt(fn, k_ID_indexRemapDim, indexRemapDim);
        cb.SetInt(fn, k_ID_reduction, (int)reduction);
        unsafe
        {
            var trailing = stackalloc int[8];
            int trailingDim = 1;
            for (int j = (indexRemapDim-1); j >= 0; j--)
            {
                trailing[j] = trailingDim;
                trailingDim *= X.shape[j];
            }
            cb.SetInt8(fn, k_ID_trailing, trailing);
        }
        cb.SetTensorAsBuffer(fn, k_ID_Iptr, Pin(indices));
        cb.SetTensorAsBuffer(fn, k_ID_Uptr, Pin(updates));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.Dispatch(fn, updatesLength, indicesLength, 1);

        return O;
    }

    /// <inheritdoc/>
    public override TensorInt ScatterND(TensorInt X, TensorInt indices, TensorInt updates, Layers.ScatterReductionMode reduction)
    {
        var O = NewOutputTensorInt(X.shape);
        if (O.shape.HasZeroDims())
            return O;

        MemCopy(X, O);

        int indexRemapDim = indices.shape[-1];
        int indicesLength = indices.shape.Length(0, -1);
        int updatesLength = updates.shape.length / indicesLength;

        var fn = new ComputeFunc("ScatterNDInt");
        cb.SetInt(fn, k_ID_updatesLength, updatesLength);
        cb.SetInt(fn, k_ID_indicesLength, indicesLength);
        cb.SetInt(fn, k_ID_indexRemapDim, indexRemapDim);
        cb.SetInt(fn, k_ID_reduction, (int)reduction);
        unsafe
        {
            var trailing = stackalloc int[8];
            int trailingDim = 1;
            for (int j = (indexRemapDim-1); j >= 0; j--)
            {
                trailing[j] = trailingDim;
                trailingDim *= X.shape[j];
            }
            cb.SetInt8(fn, k_ID_trailing, trailing);
        }
        cb.SetTensorAsBuffer(fn, k_ID_Iptr, Pin(indices));
        cb.SetTensorAsBuffer(fn, k_ID_UIntptr, Pin(updates));
        cb.SetTensorAsBuffer(fn, k_ID_OIntptr, Pin(O, clearOnInit: false));
        cb.Dispatch(fn, updatesLength, indicesLength, 1);

        return O;
    }

    /// <inheritdoc/>
    public override TensorInt OneHot(TensorInt X, int axis, int depth, int offValue, int onValue)
    {
        var O = NewOutputTensorInt(ShapeInference.OneHot(X.shape, axis, depth));
        if (O.shape.HasZeroDims())
            return O;

        axis = O.shape.Axis(axis);

        var fn = new ComputeFunc("OneHot");
        cb.SetInt(fn, k_ID_depth, depth);
        cb.SetInt(fn, k_ID_offValue, offValue);
        cb.SetInt(fn, k_ID_onValue, onValue);
        cb.SetInt(fn, k_ID_rankO, O.shape.rank);

        cb.SetInt(fn, k_ID_stridesToAxis, O.shape.Strides(axis));
        cb.SetInt(fn, k_ID_axisDim, O.shape[axis]);

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        cb.Dispatch(fn, O.shape.length, 1, 1);

        return O;
    }

    /// <inheritdoc/>
    public override Tensor[] TopK(TensorFloat X, int k, int axis, bool largest, bool sorted)
    {
        var outputShape = new TensorShape(X.shape);
        outputShape[axis] = k;

        var values = NewOutputTensorFloat(outputShape);
        var indices = NewOutputTensorInt(outputShape);
        if (outputShape.HasZeroDims())
            return new Tensor[] { values, indices };

        int reduceLength = X.shape[axis];
        int innerLength = X.shape.Strides(axis);
        int outerLength = X.shape.length / (reduceLength * innerLength);

        var fn = new ComputeFunc(largest ? "TopKLargest" : "TopKSmallest");
        cb.SetInt(fn, k_ID_innerLength, innerLength);
        cb.SetInt(fn, k_ID_outerLength, outerLength);
        cb.SetInt(fn, k_ID_reduceLength, reduceLength);
        cb.SetInt(fn, k_ID_maxK, k);
        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Valuesptr, Pin(values, clearOnInit: false));
        cb.SetTensorAsBuffer(fn, k_ID_Indicesptr, Pin(indices, clearOnInit: false));
        cb.Dispatch(fn, innerLength, outerLength, 1);

        return new Tensor[] { values, indices };
    }

    /// <inheritdoc/>
    public override TensorFloat RoiAlign(TensorFloat X, TensorFloat Rois, TensorInt Indices, Layers.RoiPoolingMode mode, int outputHeight, int outputWidth, int samplingRatio, float spatialScale)
    {
        var O = NewOutputTensorFloat(ShapeInference.RoiAlign(X.shape, Rois.shape, Indices.shape, outputHeight, outputWidth));
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc(mode == Layers.RoiPoolingMode.Avg ? "RoiAlignAvg" : "RoiAlignMax");
        cb.SetInt(fn, k_ID_numRois, Rois.shape[0]);
        cb.SetInt(fn, k_ID_inputChannels, X.shape[1]);
        cb.SetInt(fn, k_ID_inputHeight, X.shape[2]);
        cb.SetInt(fn, k_ID_inputWidth, X.shape[3]);
        cb.SetInt(fn, k_ID_inputSpatialSize, X.shape[2] * X.shape[3]);
        cb.SetInt(fn, k_ID_inputBatchOffset, X.shape[1] * X.shape[2] * X.shape[3]);
        cb.SetInt(fn, k_ID_outputHeight, outputHeight);
        cb.SetInt(fn, k_ID_outputWidth, outputWidth);
        cb.SetInt(fn, k_ID_outputSpatialSize, outputHeight * outputWidth);
        cb.SetFloat(fn, k_ID_normalizeOHeight, 1.0f / outputHeight);
        cb.SetFloat(fn, k_ID_normalizeOWidth, 1.0f / outputWidth);
        cb.SetInt(fn, k_ID_samplingRatio, samplingRatio);
        cb.SetFloat(fn, k_ID_spatialScale, spatialScale);

        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Sptr, Pin(Rois));
        cb.SetTensorAsBuffer(fn, k_ID_Bptr, Pin(Indices));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        cb.Dispatch(fn, O.shape[0] * O.shape[1], O.shape[2] * O.shape[3], 1);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat RandomNormal(TensorShape s, float mean, float scale, float? seed)
    {
        var O = NewOutputTensorFloat(s);

        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("RandomNormal");
        cb.SetInt(fn, k_ID_lengthO, O.shape.length);
        cb.SetInt(fn, k_ID_seed, (int)Random.GetOpSeed(seed));
        cb.SetFloat(fn, k_ID_mean, mean);
        cb.SetFloat(fn, k_ID_scale, scale);

        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        cb.Dispatch(fn, O.shape.length, 1, 1);

        return O;
    }

    /// <inheritdoc/>
    public override TensorFloat RandomUniform(TensorShape s, float low, float high, float? seed)
    {
        var O = NewOutputTensorFloat(s);

        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc("RandomUniform");
        cb.SetInt(fn, k_ID_lengthO, O.shape.length);
        cb.SetInt(fn, k_ID_seed, (int)Random.GetOpSeed(seed));
        cb.SetFloat(fn, k_ID_low, low);
        cb.SetFloat(fn, k_ID_high, high);

        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        cb.Dispatch(fn, O.shape.length, 1, 1);

        return O;
    }

    /// <inheritdoc/>
    public override Tensor Bernoulli(TensorFloat X, DataType dataType, float? seed)
    {
        var O = NewOutputTensor(X.shape, dataType);
        if (O.shape.HasZeroDims())
            return O;

        var fn = new ComputeFunc(dataType == DataType.Float ? "BernoulliFloat" : "BernoulliInt");
        cb.SetInt(fn, k_ID_lengthO, O.shape.length);
        cb.SetInt(fn, k_ID_seed, (int)Random.GetOpSeed(seed));
        cb.ScheduleXO(fn, Pin(X), Pin(O, clearOnInit: false), O.shape.length);

        return O;
    }

    /// <inheritdoc/>
    public override Tensor Cast(Tensor X, DataType toType)
    {
        if (X.dataType == toType)
            return Copy(X);

        var O = NewOutputTensor(X.shape, toType);
        if (O.shape.HasZeroDims())
            return O;

        ComputeFunc fn;
        if (toType == DataType.Float)
        {
            fn = new ComputeFunc("CastToFloat");
            cb.SetTensorAsBuffer(fn, k_ID_XIntptr, Pin(X));
            cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        }
        else
        {
            fn = new ComputeFunc("CastToInt");
            cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
            cb.SetTensorAsBuffer(fn, k_ID_OIntptr, Pin(O, clearOnInit: false));
        }
        cb.SetInt(fn, k_ID_X_length, X.shape.length);

        cb.Dispatch(fn, ComputeHelper.IDivC(X.shape.length, 4), 1, 1);

        return O;
    }

    /// <inheritdoc/>
    protected override void MemCopy(Tensor X, Tensor O, int length = -1, int offsetX = 0, int offsetO = 0)
    {
        length = length < 0 ? O.shape.length - offsetO : length;
        if (length == 0)
            return;
        Logger.AssertIsTrue(length > 0, "MemCopy.InputError: copy length must be greater than 0");
        Logger.AssertIsTrue(offsetX >= 0, "MemCopy.BoundsError: copy out of bounds for tensor X");
        Logger.AssertIsTrue(offsetX + length <= X.shape.length, "MemCopy.BoundsError: copy out of bounds for tensor X");
        Logger.AssertIsTrue(offsetO >= 0, "MemCopy.BoundsError: copy out of bounds for tensor O");
        Logger.AssertIsTrue(offsetO + length <= O.shape.length, "MemCopy.BoundsError: copy out of bounds for tensor O");
        var fn = new ComputeFunc("MemCopy");
        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_offsetX, offsetX);
        cb.SetInt(fn, k_ID_offsetO, offsetO);
        cb.SetInt(fn, k_ID_count, length);
        cb.Dispatch(fn, ComputeHelper.IDivC(length, 4), 1, 1);
    }

    /// <inheritdoc/>
    protected override void MemCopyStride(Tensor X, Tensor O, int strideX, int strideO, int length, int count, int offsetX = 0, int offsetO = 0)
    {
        if (length == 0 || count == 0)
            return;
        Logger.AssertIsTrue(length > 0, "MemCopy.InputError: copy stride length must be greater than 0");
        Logger.AssertIsTrue(count > 0, "MemCopy.InputError: copy stride count must be greater than 0");
        Logger.AssertIsTrue(offsetX >= 0, "MemCopy.BoundsError: copy stride out of bounds for tensor X");
        Logger.AssertIsTrue(offsetX + (count - 1) * strideX + length <= X.shape.length, "MemCopy.BoundsError: copy stride out of bounds for tensor X");
        Logger.AssertIsTrue(offsetO >= 0, "MemCopy.BoundsError: copy stride out of bounds for tensor O");
        Logger.AssertIsTrue(offsetO + (count - 1) * strideO + length <= O.shape.length, "MemCopy.BoundsError: copy stride out of bounds for tensor O");
        var fn = new ComputeFunc("MemCopyStride");
        var copyLength = count * length;
        cb.SetTensorAsBuffer(fn, k_ID_Xptr, Pin(X));
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));
        cb.SetInt(fn, k_ID_strideX, strideX);
        cb.SetInt(fn, k_ID_strideO, strideO);
        cb.SetInt(fn, k_ID_offsetX, offsetX);
        cb.SetInt(fn, k_ID_offsetO, offsetO);
        cb.SetInt(fn, k_ID_elementSize, length);
        cb.SetInt(fn, k_ID_count, copyLength);
        cb.Dispatch(fn, ComputeHelper.IDivC(copyLength, 4), 1, 1);
    }

    /// <inheritdoc/>
    protected override void MemSet(Tensor O, int value)
    {
        var length = O.shape.length;
        var numWords = ComputeHelper.IDivC(length, 4);
        var wordsHeight = ComputeHelper.IDivC(numWords, (int)ComputeFunc.SafeDispatchLimit * 32 * 8);
        var wordsWidth = ComputeHelper.IDivC(numWords, wordsHeight);

        var fn = new ComputeFunc("MemSet");
        cb.SetFloat(fn, k_ID_memValueFloat, math.asfloat(value));
        cb.SetInt(fn, k_ID_offsetO, 0);
        cb.SetInt(fn, k_ID_count, length);
        cb.SetInt(fn, k_ID_O_width, wordsWidth * 4);
        cb.SetTensorAsBuffer(fn, k_ID_Optr, Pin(O, clearOnInit: false));

        cb.Dispatch(fn, wordsWidth, wordsHeight, 1);
    }

    void ScheduleSGEMM(
        ComputeTensorData pinX, ComputeTensorData pinK, ComputeTensorData pinB, ComputeTensorData pinO, int M, int K, int N)
    {
        int workItemsX, workItemsY;
        string kernel;
        if (M == 1)
        {
            kernel = "Dense_V_L1Cached64";
            workItemsX = ComputeHelper.IDivC(N, 4);
            workItemsY = 1;
        }
        else if (N % 64 == 0 && K % 16 == 0)
        {
            kernel = "Dense_T16x16_R4x4";
            workItemsX = ComputeHelper.IDivC(N, 4);
            workItemsY = ComputeHelper.IDivC(M, 4);
        }
        else
        {
            kernel = "Dense_T8x8_R4x4";
            workItemsX = ComputeHelper.IDivC(N, 4);
            workItemsY = ComputeHelper.IDivC(M, 4);
        }

        var fn = new ComputeFunc(kernel);

        cb.SetInt(fn, k_ID_X_width, K);
        cb.SetInt(fn, k_ID_W_width, N);
        cb.SetInt(fn, k_ID_O_height, M);
        cb.SetInt(fn, k_ID_O_width, N);
        cb.SetTensorAsBuffer(fn, k_ID_Xptr, pinX);
        cb.SetTensorAsBuffer(fn, k_ID_Optr, pinO);
        cb.SetTensorAsBuffer(fn, k_ID_Wptr, pinK);
        cb.SetInt(fn, k_ID_maxXIndex, M * K - 1);
        cb.SetInt(fn, k_ID_maxWIndex, K * N - 1);

        cb.SetTensorAsBuffer(fn, k_ID_Bptr, pinB);
        cb.SetInt(fn, k_ID_maxBIndex, N - 1);

        cb.Dispatch(fn, workItemsX, workItemsY, 1);
    }

    void ScheduleGEMM(
        ComputeTensorData pinX, ComputeTensorData pinK, ComputeTensorData pinO, int M, int K, int N, bool transposeA = false, bool transposeB = false)
    {
        if (transposeA || transposeB)
        {
            string kernel;

            if (transposeA)
                kernel = transposeB ? "GemmT_XT_WT_T8x8_R4x4" : "GemmT_XT_T8x8_R4x4";
            else
                kernel = "GemmT_WT_T8x8_R4x4";

            var fn = new ComputeFunc(kernel);

            cb.SetInt(fn, k_ID_M, M);
            cb.SetInt(fn, k_ID_N, N);
            cb.SetInt(fn, k_ID_K, K);
            cb.SetInt(fn, k_ID_maxXIndex, M * K - 1);
            cb.SetInt(fn, k_ID_maxWIndex, K * N - 1);
            cb.SetTensorAsBuffer(fn, k_ID_Xptr, pinX);
            cb.SetTensorAsBuffer(fn, k_ID_Wptr, pinK);
            cb.SetTensorAsBuffer(fn, k_ID_Optr, pinO);

            cb.Dispatch(fn, ComputeHelper.IDivC(N, 4), ComputeHelper.IDivC(M, 4), 1);
        }
        else
        {
            int workItemsX, workItemsY, workItemsZ;
            string kernel;

            if (M == 1)
            {
                kernel = "Gemm_V_L1Cached64";
                workItemsX = ComputeHelper.IDivC(N, 4);
                workItemsY = 1;
                workItemsZ = 1;
            }
            else if (N % 64 == 0 && K % 16 == 0)
            {
                kernel = "Gemm_T16x16_R4x4";
                workItemsX = ComputeHelper.IDivC(N, 4);
                workItemsY = ComputeHelper.IDivC(M, 4);
                workItemsZ = 1;
            }
            else
            {
                kernel = "Gemm_T8x8_R4x4";
                workItemsX = ComputeHelper.IDivC(N, 4);
                workItemsY = ComputeHelper.IDivC(M, 4);
                workItemsZ = 1;
            }

            var fn = new ComputeFunc(kernel);

            cb.SetInt(fn, k_ID_X_width, K);
            cb.SetInt(fn, k_ID_W_width, N);
            cb.SetInt(fn, k_ID_O_width, N);
            cb.SetInt(fn, k_ID_O_height, M);
            cb.SetInt(fn, k_ID_maxXIndex, M * K - 1);
            cb.SetInt(fn, k_ID_maxWIndex, K * N - 1);
            cb.SetTensorAsBuffer(fn, k_ID_Xptr, pinX);
            cb.SetTensorAsBuffer(fn, k_ID_Wptr, pinK);
            cb.SetTensorAsBuffer(fn, k_ID_Optr, pinO);

            cb.Dispatch(fn, workItemsX, workItemsY, workItemsZ);
        }
    }

    /// <inheritdoc/>
    protected override void SinglePassLSTM(TensorFloat X, TensorFloat W, TensorFloat R, TensorFloat B, TensorInt sequenceLens, TensorFloat P, TensorFloat Y, TensorFloat Y_h, TensorFloat Y_c, Layers.RnnActivation[] activations, float[] activationAlpha, float[] activationBeta, bool inputForget, float clip, bool isReverse, int dirIndex, Layers.RnnLayout layout)
    {
        var pinY = Pin(Y, clearOnInit: false);

        var pinX = Pin(X);
        var pinW = Pin(W);
        var pinR = Pin(R);
        var pinP = Pin(P);
        var pinB = Pin(B);
        var pinY_h = Pin(Y_h);
        var pinY_c = Pin(Y_c);
        var pinSequenceLens = Pin(sequenceLens);

        var numDirections = B.shape[0];
        var inputSize = X.shape[2];
        var hiddenSize = R.shape[2];

        var seqLength = X.shape[0];
        var batchSize = X.shape[1];

        var xStrideSeq = batchSize * 4 * hiddenSize;
        var xStrideBatch = 4 * hiddenSize;

        var yStrideDir = batchSize * hiddenSize;
        var yStrideSeq = numDirections * batchSize * hiddenSize;
        var yStrideBatch = hiddenSize;

        if (layout == Layers.RnnLayout.BatchFirst)
        {
            seqLength = X.shape[1];
            batchSize = X.shape[0];

            xStrideSeq = 4 * hiddenSize;
            xStrideBatch = seqLength * 4 * hiddenSize;

            yStrideDir = hiddenSize;
            yStrideSeq = numDirections * hiddenSize;
            yStrideBatch = seqLength * numDirections * hiddenSize;
        }

        var HtxRT = NewTempTensorFloat(new TensorShape(batchSize * 4 * hiddenSize));
        var XsixWT = NewTempTensorFloat(new TensorShape(seqLength * batchSize * 4 * hiddenSize));

        var pinHtxRT = Pin(HtxRT, clearOnInit: false);
        var pinXsixWT = Pin(XsixWT, clearOnInit: false);

        ScheduleGEMM(pinX, pinW, pinXsixWT, seqLength * batchSize, inputSize, 4 * hiddenSize, transposeB: true);

        var endFn = new ComputeFunc("LSTMEnd");
        cb.SetInt(endFn, k_ID_hiddenSize, hiddenSize);
        cb.SetInt(endFn, k_ID_batchSize, batchSize);
        cb.SetInt(endFn, k_ID_xStride, xStrideBatch);
        cb.SetInt(endFn, k_ID_yStride, yStrideBatch);
        cb.SetBool(endFn, k_ID_inputForget, inputForget);
        cb.SetFloat(endFn, k_ID_clipValue, clip);
        cb.SetInt(endFn, k_ID_fActivation, (int)activations[3 * dirIndex + 0]);
        cb.SetFloat(endFn, k_ID_fAlpha, activationAlpha[3 * dirIndex + 0]);
        cb.SetFloat(endFn, k_ID_fBeta, activationAlpha[3 * dirIndex + 0]);
        cb.SetInt(endFn, k_ID_gActivation, (int)activations[3 * dirIndex + 1]);
        cb.SetFloat(endFn, k_ID_gAlpha, activationAlpha[3 * dirIndex + 1]);
        cb.SetFloat(endFn, k_ID_gBeta, activationAlpha[3 * dirIndex + 1]);
        cb.SetInt(endFn, k_ID_hActivation, (int)activations[3 * dirIndex + 2]);
        cb.SetFloat(endFn, k_ID_hAlpha, activationAlpha[3 * dirIndex + 2]);
        cb.SetFloat(endFn, k_ID_hBeta, activationAlpha[3 * dirIndex + 2]);
        cb.SetTensorAsBuffer(endFn, k_ID_Yptr, pinY);
        cb.SetTensorAsBuffer(endFn, k_ID_YHptr, pinY_h);
        cb.SetTensorAsBuffer(endFn, k_ID_YCptr, pinY_c);
        cb.SetTensorAsBuffer(endFn, k_ID_Bptr, pinB);
        cb.SetInt(endFn, k_ID_bOffset, dirIndex * 8 * hiddenSize);
        cb.SetTensorAsBuffer(endFn, k_ID_Pptr, pinP);
        cb.SetInt(endFn, k_ID_pOffset, dirIndex * 3 * hiddenSize);
        cb.SetTensorAsBuffer(endFn, k_ID_XsixWTptr, pinXsixWT);
        cb.SetTensorAsBuffer(endFn, k_ID_HtxRTptr, pinHtxRT);
        cb.SetTensorAsBuffer(endFn, k_ID_SequenceLensptr, pinSequenceLens);

        for (var i = 0; i < seqLength; i++)
        {
            var seqIndex = isReverse ? seqLength - 1 - i : i;

            ScheduleGEMM(pinY_h, pinR, pinHtxRT, batchSize, hiddenSize, 4 * hiddenSize, transposeB: true);

            cb.SetInt(endFn, k_ID_seqIndex, seqIndex);
            cb.SetInt(endFn, k_ID_yOffset, dirIndex * yStrideDir + seqIndex * yStrideSeq);
            cb.SetInt(endFn, k_ID_xOffset, seqIndex * xStrideSeq);
            cb.Dispatch(endFn, batchSize, hiddenSize, 1);
        }
    }

    /// <inheritdoc/>
    public override Tensor PinToDevice(Tensor X, bool clearOnInit = true)
    {
        Pin(X, clearOnInit);
        return X;
    }
}
} // namespace Unity.Sentis
