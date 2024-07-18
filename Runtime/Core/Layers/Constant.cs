using System;

namespace Unity.Sentis.Layers
{
    /// <summary>
    /// Represents a constant in a model.
    /// </summary>
    public class Constant
    {
        /// <summary>
        /// The index of the constant.
        /// </summary>
        public int index;
        /// <summary>
        /// The shape of the constant as a `TensorShape`.
        /// </summary>
        public TensorShape shape;
        /// <summary>
        /// The size of the constant in bytes.
        /// </summary>
        public int lengthBytes;
        /// <summary>
        /// The data type of the constant as a `DataType`.
        /// </summary>
        public DataType dataType;
        /// <summary>
        /// The elements of the constant as a `NativeTensorArray`.
        /// </summary>
        public NativeTensorArray weights;

        /// <summary>
        /// Initializes and returns a vector `Constant` from a given index, shape and `NativeTensorArray` array.
        /// </summary>
        /// <param name="index">The index to use for the constant.</param>
        /// <param name="shape">The shape to use for the constant.</param>
        /// <param name="dataType">The dataType to use for the constant.</param>
        /// <param name="array">The array of values.</param>
        public Constant(int index, TensorShape shape, DataType dataType, NativeTensorArray array)
        {
            this.index = index;
            this.shape = shape;
            this.lengthBytes = array.Length * sizeof(float);
            this.dataType = dataType;
            this.weights = array;
        }

        internal Constant(int index, TensorShape shape, DataType dataType, int lengthBytes)
        {
            this.index = index;
            this.shape = shape;
            this.lengthBytes = lengthBytes;
            this.dataType = dataType;
        }

        /// <summary>
        /// Initializes and returns a vector `Constant` from a given index, shape and float array.
        /// </summary>
        /// <param name="index">The index to use for the constant.</param>
        /// <param name="shape">The shape to use for the constant.</param>
        /// <param name="value">The float array of values.</param>
        public Constant(int index, TensorShape shape, float[] value)
        {
            this.index = index;
            this.shape = shape;
            this.lengthBytes = value.Length * sizeof(float);
            this.dataType = DataType.Float;
            if (value.Length == 0)
                return;
            weights = new NativeTensorArray(value.Length);
            NativeTensorArray.Copy(value, weights);
        }

        /// <summary>
        /// Initializes and returns a vector `Constant` from a given index, shape and int array.
        /// </summary>
        /// <param name="index">The index to use for the constant.</param>
        /// <param name="shape">The shape to use for the constant.</param>
        /// <param name="value">The int array of values.</param>
        internal Constant(int index, TensorShape shape, int[] value)
        {
            this.index = index;
            this.shape = shape;
            this.lengthBytes = value.Length * sizeof(int);
            this.dataType = DataType.Int;
            if (value.Length == 0)
                return;
            weights = new NativeTensorArray(value.Length);
            NativeTensorArray.Copy(value, weights);
        }

        internal static Constant AllocNoData(int index, DataType dataType, TensorShape shape)
        {
            switch (dataType)
            {
                case DataType.Float:
                    return new Constant(index, shape, DataType.Float, shape.length * sizeof(float));
                case DataType.Int:
                    return new Constant(index, shape, DataType.Int, shape.length * sizeof(int));
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns a string that represents the `Constant`.
        /// </summary>
        /// <returns>A string representation of the `Constant`.</returns>
        public override string ToString()
        {
            return $"Constant{dataType.ToString()} - index: {index}, shape: {shape}, dataType: {dataType}";
        }

        /// <summary>
        /// Creates and returns a CPU `Tensor` of the constant.
        /// </summary>
        /// <returns>The created tensor.</returns>
        /// <exception cref="NotImplementedException">Thrown when a given data type is not supported.</exception>
        internal Tensor WeightsToTensor()
        {
            switch (dataType)
            {
                case DataType.Float:
                {
                    var array = new float[shape.length];
                    NativeTensorArray.Copy(weights, 0, array, 0, shape.length);
                    return new TensorFloat(shape, array);
                }
                case DataType.Int:
                {
                    var array = new int[shape.length];
                    NativeTensorArray.Copy(weights, 0, array, 0, shape.length);
                    return new TensorInt(shape, array);
                }
                default:
                    throw new NotImplementedException();
            }
        }

        internal Tensor WeightsToTensorWithSharedTensorData()
        {
            Tensor output;
            switch (dataType)
            {
                case DataType.Float:
                {
                    output = TensorFloat.AllocNoData(shape);
                    break;
                }
                case DataType.Int:
                {
                    output = TensorInt.AllocNoData(shape);
                    break;
                }
                default:
                    throw new NotImplementedException();
            }

            output.dataOnBackend = new BurstTensorData(weights);
            return output;
        }

        /// <summary>
        /// Initializes the constant with the shape, dataType and weights from a given `Tensor`.
        /// </summary>
        /// <param name="X">The tensor to use for initialization.</param>
        /// <exception cref="NotImplementedException">Thrown when a given data type is not supported.</exception>
        internal void TensorToDataSet(Tensor X)
        {
            this.shape = X.shape;
            this.dataType = X.dataType;
            if (X.shape.HasZeroDims())
                return;
            weights = new NativeTensorArray(X.shape.length);
            switch (dataType)
            {
                case DataType.Float:
                {
                    this.lengthBytes = shape.length * sizeof(float);
                    NativeTensorArray.Copy(X.ToReadOnlyNativeArray<float>(), 0, weights, 0, shape.length);
                    break;
                }
                case DataType.Int:
                {
                    this.lengthBytes = shape.length * sizeof(float);
                    NativeTensorArray.Copy(X.ToReadOnlyNativeArray<int>(), 0, weights, 0, shape.length);
                    break;
                }
                default:
                    throw new NotImplementedException($"DataType {dataType} not supported");
            }
        }
    }
}
