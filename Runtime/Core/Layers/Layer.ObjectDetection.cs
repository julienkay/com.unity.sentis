using System;

namespace Unity.Sentis.Layers
{
    /// <summary>
    /// Options for the formatting of the box data for `NonMaxSuppression`.
    /// </summary>
    public enum CenterPointBox
    {
        /// <summary>
        /// Use TensorFlow box formatting. Box data is [y1, x1, y2, x2] where (y1, x1) and (y2, x2) are the normalized coordinates of any diagonal pair of box corners.
        /// </summary>
        Corners,
        /// <summary>
        /// Use PyTorch box formatting. Box data is [x_center, y_center, width, height].
        /// </summary>
        Center
    }

    /// <summary>
    /// Represents a `NonMaxSuppression` object detection layer. This calculates an output tensor of selected indices of boxes from input `boxes` and `scores` tensors, and bases the indices on the scores and amount of intersection with previously selected boxes.
    /// </summary>
    [Serializable]
    [Optimization.CPUFallback.CPUReadInputs(2, 3, 4)]
    public class NonMaxSuppression : Layer
    {
        /// <summary>
        /// The format in which the box data is stored in the `boxes` tensor as a `CenterPointBox`.
        /// </summary>
        public CenterPointBox centerPointBox;

        /// <summary>
        /// Initializes and returns an instance of `NonMaxSuppression` object detection layer.
        /// </summary>
        /// <param name="name">The name to use for the output tensor of the layer.</param>
        /// <param name="boxes">The name to use for the boxes tensor of the layer.</param>
        /// <param name="scores">The name to use for the scores tensor of the layer.</param>
        /// <param name="maxOutputBoxesPerClass">The name to use for an optional scalar tensor, with the maximum number of boxes to return for each class.</param>
        /// <param name="iouThreshold">The name to use for optional scalar tensor, with the threshold above which the intersect-over-union rejects a box.</param>
        /// <param name="scoreThreshold">The name to use for an optional scalar tensor, with the threshold below which the box score filters a box from the output.</param>
        /// <param name="centerPointBox">The format the `boxes` tensor uses to store the box data as a `CenterPointBox`. The default value is `CenterPointBox.Corners`.</param>
        public NonMaxSuppression(string name, string boxes, string scores, string maxOutputBoxesPerClass = null, string iouThreshold = null, string scoreThreshold = null, CenterPointBox centerPointBox = CenterPointBox.Corners)
        {
            this.name = name;
            if (scoreThreshold != null)
                this.inputs = new[] { boxes, scores, maxOutputBoxesPerClass, iouThreshold, scoreThreshold };
            else if (iouThreshold != null)
                this.inputs = new[] { boxes, scores, maxOutputBoxesPerClass, iouThreshold };
            else if (maxOutputBoxesPerClass != null)
                this.inputs = new[] { boxes, scores, maxOutputBoxesPerClass };
            else
                this.inputs = new[] { boxes, scores };
            this.centerPointBox = centerPointBox;
        }

        /// <inheritdoc/>
        internal override PartialTensor InferPartialTensor(PartialTensor[] inputTensors, PartialInferenceContext ctx)
        {
            var shape = new SymbolicTensorShape(SymbolicTensorDim.Unknown, new SymbolicTensorDim(3));
            return new PartialTensor(DataType.Int, shape);
        }

        public override Tensor Execute(Tensor[] inputTensors, ExecutionContext ctx)
        {
            var maxOutputBoxesPerClass = 0;
            if (inputTensors.Length > 2 && inputTensors[2] != null)
            {
                inputTensors[2].MakeReadable();
                maxOutputBoxesPerClass = inputTensors[2].ToReadOnlySpan<int>()[0];
            }
            var iouThreshold = 0f;
            if (inputTensors.Length > 3 && inputTensors[3] != null)
            {
                inputTensors[3].MakeReadable();
                iouThreshold = inputTensors[3].ToReadOnlySpan<float>()[0];
            }
            var scoreThreshold = 0f;
            if (inputTensors.Length > 4 && inputTensors[4] != null)
            {
                inputTensors[4].MakeReadable();
                scoreThreshold = inputTensors[4].ToReadOnlySpan<float>()[0];
            }
            return ctx.backend.NonMaxSuppression(inputTensors[0] as TensorFloat, inputTensors[1] as TensorFloat, maxOutputBoxesPerClass, iouThreshold, scoreThreshold, centerPointBox);
        }

        internal override string profilerTag => "NonMaxSuppression";
    }

    /// <summary>
    /// Options for the pooling mode for `RoiAlign`.
    /// </summary>
    public enum RoiPoolingMode
    {
        /// <summary>
        /// Use average pooling.
        /// </summary>
        Avg = 0,
        /// <summary>
        /// Use maximum pooling.
        /// </summary>
        Max = 1
    }

    /// <summary>
    /// Represents an `RoiAlign` region of interest alignment layer. This calculates an output tensor by pooling the input tensor across each region of interest given by the `rois` tensor.
    /// </summary>
    [Serializable]
    public class RoiAlign : Layer
    {
        /// <summary>
        /// The pooling mode of the operation as an `RoiPoolingMode`.
        /// </summary>
        public RoiPoolingMode mode;
        /// <summary>
        /// The height of the output tensor.
        /// </summary>
        public int outputHeight;
        /// <summary>
        /// The width of the output tensor.
        /// </summary>
        public int outputWidth;
        /// <summary>
        /// The number of sampling points in the interpolation grid used to compute the output value of each pooled output bin.
        /// </summary>
        public int samplingRatio;
        /// <summary>
        /// The multiplicative spatial scale factor used to translate coordinates from their input spatial scale to the scale used when pooling.
        /// </summary>
        public float spatialScale;

        /// <summary>
        /// Initializes and returns an instance of `RoiAlign` region of interest alignment layer.
        /// </summary>
        /// <param name="name">The name to use for the output tensor of the layer.</param>
        /// <param name="input">The name to use for the input tensor of the layer.</param>
        /// <param name="rois">The name to use for the region of interest tensor of the layer.</param>
        /// <param name="batchIndices">The name to use for the 1D input tensor where each element denotes the index of the image in the batch for a given region of interest.</param>
        /// <param name="mode">The pooling mode of the operation as an `RoiPoolingMode`.</param>
        /// <param name="outputHeight">The height of the output tensor.</param>
        /// <param name="outputWidth">The width of the output tensor.</param>
        /// <param name="samplingRatio">The number of sampling points in the interpolation grid used to compute the output value of each pooled output bin.</param>
        /// <param name="spatialScale">The multiplicative spatial scale factor used to translate coordinates from their input spatial scale to the scale used when pooling.</param>
        public RoiAlign(string name, string input, string rois, string batchIndices, RoiPoolingMode mode, int outputHeight, int outputWidth, int samplingRatio, float spatialScale)
        {
            this.name = name;
            this.inputs = new[] { input, rois, batchIndices };
            this.mode = mode;
            this.outputHeight = outputHeight;
            this.outputWidth = outputWidth;
            this.samplingRatio = samplingRatio;
            this.spatialScale = spatialScale;
        }

        /// <inheritdoc/>
        internal override PartialTensor InferPartialTensor(PartialTensor[] inputTensors, PartialInferenceContext ctx)
        {
            var shapeX = inputTensors[0].shape;
            var shapeRois = inputTensors[1].shape;
            var shapeIndices = inputTensors[2].shape;
            var shapeOut = SymbolicTensorShape.UnknownOfRank(4);

            shapeRois.DeclareRank(2);
            Logger.AssertIsFalse(shapeRois[1] != 4, "RoiAlign.ValueError: incorrect number of num_rois, expecting 4");
            shapeOut[0] = shapeRois[0];

            shapeX.DeclareRank(4);
            shapeOut[1] = shapeX[1];

            shapeIndices.DeclareRank(1);
            shapeOut[0] = SymbolicTensorDim.MaxDefinedDim(shapeOut[0], shapeIndices[0]);

            shapeOut[2] = new SymbolicTensorDim(outputHeight);
            shapeOut[3] = new SymbolicTensorDim(outputWidth);

            return new PartialTensor(DataType.Float, shapeOut);
        }

        /// <inheritdoc/>
        public override Tensor Execute(Tensor[] inputTensors, ExecutionContext ctx)
        {
            return ctx.backend.RoiAlign(inputTensors[0] as TensorFloat, inputTensors[1] as TensorFloat, inputTensors[2] as TensorInt, mode, outputHeight, outputWidth, samplingRatio, spatialScale);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{base.ToString()}, mode: {mode}, outputHeight: {outputHeight}, outputWidth: {outputWidth}, samplingRatio: {samplingRatio}, spatialScale: {spatialScale}";
        }

        internal override string profilerTag => "RoiAlign";
    }
}
