﻿using AdvUtils;
using Seq2SeqSharp.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TensorSharp;

namespace Seq2SeqSharp
{
    [Serializable]
    public class LSTMAttentionDecoderCell
    {
        public IWeightTensor Hidden { get; set; }
        public IWeightTensor Cell { get; set; }

        int m_hiddenDim;
        int m_inputDim;
        int m_deviceId;
        string m_name;

        IWeightTensor m_Wxhc;
        IWeightTensor m_b;

        LayerNormalization m_layerNorm1;
        LayerNormalization m_layerNorm2;

        public LSTMAttentionDecoderCell(string name, int hiddenDim, int inputDim, int contextDim, int deviceId)
        {
            m_name = name;
            m_hiddenDim = hiddenDim;
            m_inputDim = inputDim;
            m_deviceId = deviceId;

            Logger.WriteLine($"Create LSTM attention decoder cell '{name}' HiddemDim = '{hiddenDim}', InputDim = '{inputDim}', ContextDim = '{contextDim}', DeviceId = '{deviceId}'");

            m_Wxhc = new WeightTensor(new long[2] { inputDim + hiddenDim + contextDim, hiddenDim * 4 }, deviceId, normal: true, name: $"{name}.{nameof(m_Wxhc)}", isTrainable: true);
            m_b = new WeightTensor(new long[2] { 1, hiddenDim * 4 }, 0, deviceId, name: $"{name}.{nameof(m_b)}", isTrainable: true);

            m_layerNorm1 = new LayerNormalization($"{name}.{nameof(m_layerNorm1)}", hiddenDim * 4, deviceId);
            m_layerNorm2 = new LayerNormalization($"{name}.{nameof(m_layerNorm2)}", hiddenDim, deviceId);
        }

        /// <summary>
        /// Update LSTM-Attention cells according to given weights
        /// </summary>
        /// <param name="context">The context weights for attention</param>
        /// <param name="input">The input weights</param>
        /// <param name="computeGraph">The compute graph to build workflow</param>
        /// <returns>Update hidden weights</returns>
        public IWeightTensor Step(IWeightTensor context, IWeightTensor input, IComputeGraph g)
        {
            var computeGraph = g.CreateSubGraph(m_name);

            var cell_prev = Cell;
            var hidden_prev = Hidden;

            var hxhc = computeGraph.ConcatColumns(input, hidden_prev, context);
            var hhSum = computeGraph.Affine(hxhc, m_Wxhc, m_b);
            var hhSum2 = m_layerNorm1.Norm(hhSum, computeGraph);

            (var gates_raw, var cell_write_raw) = computeGraph.SplitColumns(hhSum2, m_hiddenDim * 3, m_hiddenDim);
            var gates = computeGraph.Sigmoid(gates_raw);
            var cell_write = computeGraph.Tanh(cell_write_raw);

            (var input_gate, var forget_gate, var output_gate) = computeGraph.SplitColumns(gates, m_hiddenDim, m_hiddenDim, m_hiddenDim);

            // compute new cell activation: ct = forget_gate * cell_prev + input_gate * cell_write
            Cell = computeGraph.EltMulMulAdd(forget_gate, cell_prev, input_gate, cell_write);
            var ct2 = m_layerNorm2.Norm(Cell, computeGraph);

            Hidden = computeGraph.EltMul(output_gate, computeGraph.Tanh(ct2));

            return Hidden;
        }

        public List<IWeightTensor> getParams()
        {
            List<IWeightTensor> response = new List<IWeightTensor>();
            response.Add(m_Wxhc);
            response.Add(m_b);

            response.AddRange(m_layerNorm1.getParams());
            response.AddRange(m_layerNorm2.getParams());

            return response;
        }

        public void Reset(IWeightFactory weightFactory, int batchSize)
        {
            Hidden = weightFactory.CreateWeightTensor(batchSize, m_hiddenDim, m_deviceId, true, name: $"{m_name}.{nameof(Hidden)}", isTrainable: true);
            Cell = weightFactory.CreateWeightTensor(batchSize, m_hiddenDim, m_deviceId, true, name: $"{m_name}.{nameof(Cell)}", isTrainable: true);
        }

        public void Save(Stream stream)
        {
            m_Wxhc.Save(stream);
            m_b.Save(stream);

            m_layerNorm1.Save(stream);
            m_layerNorm2.Save(stream);
        }


        public void Load(Stream stream)
        {
            m_Wxhc.Load(stream);
            m_b.Load(stream);

            m_layerNorm1.Load(stream);
            m_layerNorm2.Load(stream);
        }
    }
}


