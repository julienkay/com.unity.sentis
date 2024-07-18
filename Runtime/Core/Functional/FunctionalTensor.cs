using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Sentis
{
    /// <summary>
    /// Represents a tensor that is a result of tensor operations.
    /// </summary>
    public partial class FunctionalTensor
    {
        DataType m_DataType;
        FunctionalNode m_Source;
        int m_OutputIndex;

        internal DataType DataType => m_DataType;
        internal FunctionalNode Source => m_Source;
        internal int OutputIndex => m_OutputIndex;
        internal int Index => m_Source.OutputIndices[m_OutputIndex];

        internal FunctionalTensor(DataType dataType, FunctionalNode source, int outputIndex)
        {
            m_DataType = dataType;
            m_Source = source;
            m_OutputIndex = outputIndex;
        }

        /// <summary>
        /// Creates and returns an instance of `FunctionalTensor` from an existing tensor.
        /// </summary>
        /// <param name="tensor">The tensor to use as the source.</param>
        /// <returns>The functional tensor.</returns>
        public static FunctionalTensor FromTensor(Tensor tensor)
        {
            Layers.Constant constant;
            switch (tensor.dataType)
            {
                case DataType.Float:
                {
                    constant = new Layers.Constant(-1, tensor.shape, tensor.ToReadOnlyArray<float>());
                    break;
                }
                case DataType.Int:
                {
                    constant = new Layers.Constant(-1, tensor.shape, tensor.ToReadOnlyArray<int>());
                    break;
                }
                default:
                    throw new NotImplementedException();
            }

            var constantNode = new FunctionalConstant(constant);
            return new FunctionalTensor(constant.dataType, constantNode, 0);
        }

        /// <summary>
        /// Creates and returns an instance of `FunctionalTensor` from an existing constant.
        /// </summary>
        /// <param name="constant">The constant to use as the source.</param>
        /// <returns>The functional tensor.</returns>
        public static FunctionalTensor FromConstant(Layers.Constant constant)
        {
            var constantNode = new FunctionalConstant(constant);
            return new FunctionalTensor(constant.dataType, constantNode, 0);
        }

        internal static FunctionalTensor FromInput(Model.Input input)
        {
            var inputNode = new FunctionalInput(input);
            return new FunctionalTensor(input.dataType, inputNode, 0);
        }

        internal static FunctionalTensor[] FromLayerMultiOutputs(Layers.Layer layer, DataType[] dataTypes, FunctionalTensor[] inputs)
        {
            Assert.AreEqual(layer.inputs.Length, inputs.Length);
            var layerNode = new FunctionalLayer(inputs, dataTypes, layer);
            return layerNode.CreateOutputs();
        }

        internal static FunctionalTensor FromLayer(Layers.Layer layer, DataType dataType, FunctionalTensor[] inputs)
        {
            return FromLayerMultiOutputs(layer, new[] { dataType }, inputs)[0];
        }

        internal static FunctionalTensor FromLayer(Layers.Layer layer, DataType dataType, FunctionalTensor input)
        {
            return FromLayerMultiOutputs(layer, new[] { dataType }, new[] { input })[0];
        }

        /// <summary>
        /// Creates and returns an array of `FunctionalTensor` as the outputs of an existing model.
        /// </summary>
        /// <param name="model">The model to use as the source.</param>
        /// <param name="inputs">The functional tensors to use as the inputs to the model.</param>
        /// <param name="withCopy">Whether to do a deep copy of the model. When `false` Sentis will make destructive edits of the source model.</param>
        /// <returns>The functional tensor array.</returns>
        public static FunctionalTensor[] FromModel(Model model, FunctionalTensor[] inputs, bool withCopy = false)
        {
            if (withCopy)
                model = model.DeepCopy();
            Logger.AssertIsTrue(inputs.Length == model.inputs.Count, "ModelOutputs.ValueError: inputs length does not equal model input count {0}, {1}", inputs.Length, model.inputs.Count);
            var expressions = new Dictionary<int, FunctionalTensor>();

            for (var i = 0; i < inputs.Length; i++)
                expressions[model.inputs[i].index] = inputs[i];

            foreach (var constant in model.constants)
            {
                var node = new FunctionalConstant(constant);
                expressions[constant.index] = new FunctionalTensor(constant.dataType, node, 0);
            }

            var ctx = new PartialInferenceContext();
            foreach (var kvp in expressions)
                ctx.AddPartialTensor(kvp.Key, new PartialTensor(kvp.Value.DataType));

            foreach (var layer in model.layers)
            {
                layer.inputs = (int[])layer.inputs.Clone();
                layer.outputs = (int[])layer.outputs.Clone();
                var layerInputs = new FunctionalTensor[layer.inputs.Length];
                for (var i = 0; i < layerInputs.Length; i++)
                {
                    if (layer.inputs[i] == -1)
                        continue;
                    layerInputs[i] = expressions[layer.inputs[i]];
                }

                // infer data types
                layer.InferPartial(ctx);
                var outputDataTypes = new DataType[layer.outputs.Length];
                for (var i = 0; i < outputDataTypes.Length; i++)
                {
                    if (layer.outputs[i] == -1)
                        continue;
                    outputDataTypes[i] = ctx.GetPartialTensor(layer.outputs[i]).dataType;
                }

                var node = new FunctionalLayer(layerInputs, outputDataTypes, layer);
                var layerOutputs = node.CreateOutputs();
                for (var i = 0; i < layer.outputs.Length; i++)
                {
                    if (layer.outputs[i] == -1)
                        continue;
                    expressions[layer.outputs[i]] = layerOutputs[i];
                }
            }

            var outputs = new FunctionalTensor[model.outputs.Count];
            for (var i = 0; i < model.outputs.Count; i++)
                outputs[i] = expressions[model.outputs[i].index];
            return outputs;
        }
    }
}
