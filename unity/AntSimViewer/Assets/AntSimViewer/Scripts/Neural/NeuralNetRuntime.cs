using System;
using System.Collections.Generic;

namespace AntSimViewer
{
    /// <summary>
    /// Faithful port of SharpNEAT 4.1.0's cyclic network evaluation (NeuralNetCyclic), driven by
    /// the self-contained champion.json export. Semantics, per activation:
    ///   1. copy inputs into the post-activation array of the input nodes,
    ///   2. for each of CyclesPerActivation cycles: accumulate weight*post[src] into pre[tgt]
    ///      for every connection, then apply the activation function (LeakyReLU, a = 0.001)
    ///      to all non-input nodes and clear their pre-activation values,
    ///   3. outputs are read from the post-activation array of the output nodes.
    /// Node layout: inputs 0..InputCount-1, outputs InputCount..InputCount+OutputCount-1,
    /// hidden nodes after, in ascending node id order (matches SharpNEAT's digraph layout).
    /// </summary>
    public sealed class NeuralNetRuntime
    {
        static readonly double LeakyAlpha = 0.001;

        public readonly int InputCount;
        public readonly int OutputCount;
        public readonly int CyclesPerActivation;

        int[] _src;
        int[] _tgt;
        double[] _weight;

        readonly double[] _pre;
        readonly double[] _post;
        readonly int _nonInputStart;

        public NeuralNetRuntime(BrainData brain)
        {
            InputCount = Math.Max(1, brain.InputCount);
            OutputCount = Math.Max(1, brain.OutputCount);
            CyclesPerActivation = Math.Max(1, brain.CyclesPerActivation);

            int[] hidden = brain.HiddenNodeIds != null ? brain.HiddenNodeIds : new int[0];
            int total = InputCount + OutputCount + hidden.Length;
            _pre = new double[total];
            _post = new double[total];
            _nonInputStart = InputCount;

            // Map node ids to array indices; skip connections touching unknown nodes.
            var idToIndex = new Dictionary<int, int>();
            for (int i = 0; i < InputCount; i++) idToIndex[i] = i;
            for (int i = 0; i < OutputCount; i++) idToIndex[InputCount + i] = InputCount + i;
            int hiddenBase = InputCount + OutputCount;
            for (int i = 0; i < hidden.Length; i++) idToIndex[hidden[i]] = hiddenBase + i;

            var conns = brain.Connections;
            int n = conns != null ? conns.Length : 0;
            _src = new int[n];
            _tgt = new int[n];
            _weight = new double[n];
            int kept = 0;
            for (int i = 0; i < n; i++)
            {
                int s = idToIndex.TryGetValue(conns[i].SourceId, out int si) ? si : -1;
                int t = idToIndex.TryGetValue(conns[i].TargetId, out int ti) ? ti : -1;
                if (s < 0 || t < 0) continue;
                _src[kept] = s;
                _tgt[kept] = t;
                _weight[kept] = conns[i].Weight;
                kept++;
            }
            if (kept < n)
            {
                // Compact the arrays (defensive; normally all connections resolve).
                int[] s2 = new int[kept], t2 = new int[kept];
                double[] w2 = new double[kept];
                for (int i = 0; i < kept; i++) { s2[i] = _src[i]; t2[i] = _tgt[i]; w2[i] = _weight[i]; }
                _src = s2; _tgt = t2; _weight = w2;
            }
        }

        /// <summary>Clear pre/post activations of non-input nodes (mirrors IBlackBox.Reset).</summary>
        public void Reset()
        {
            for (int i = _nonInputStart; i < _post.Length; i++)
            {
                _pre[i] = 0.0;
                _post[i] = 0.0;
            }
        }

        /// <summary>
        /// Run one full activation. inputs must have length InputCount (sensors + bias already
        /// written by the caller); outputs must have length OutputCount and receives the result.
        /// The network is reset before evaluating, so repeated calls are independent.
        /// </summary>
        public void Activate(double[] inputs, double[] outputs)
        {
            Reset();
            for (int i = 0; i < InputCount; i++) _post[i] = inputs[i];

            for (int cycle = 0; cycle < CyclesPerActivation; cycle++)
            {
                for (int j = 0; j < _src.Length; j++)
                    _pre[_tgt[j]] += _post[_src[j]] * _weight[j];

                for (int i = _nonInputStart; i < _post.Length; i++)
                {
                    double x = _pre[i];
                    _post[i] = x < 0.0 ? x * LeakyAlpha : x;
                    _pre[i] = 0.0;
                }
            }

            for (int i = 0; i < OutputCount; i++)
                outputs[i] = _post[InputCount + i];
        }
    }
}