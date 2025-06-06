using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// Silero VAD ONNX 模型封装类
    /// </summary>
    public class SileroVadModel : IDisposable
    {
        private readonly InferenceSession _session;
        private float[][][] _state;
        private float[][] _context;
        private int _lastSr = 0;
        private int _lastBatchSize = 0;
        private static readonly List<int> SAMPLE_RATES = new List<int> { 8000, 16000 };

        public SileroVadModel(string modelPath)
        {
            try
            {
                var sessionOptions = new SessionOptions();
                sessionOptions.InterOpNumThreads = 1;
                sessionOptions.IntraOpNumThreads = 1;
                sessionOptions.EnableCpuMemArena = true;

                _session = new InferenceSession(modelPath, sessionOptions);
                ResetStates();
                
                Log.Information("Silero VAD 模型加载成功: {ModelPath}", modelPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Silero VAD 模型加载失败: {ModelPath}", modelPath);
                throw;
            }
        }

        public void ResetStates()
        {
            _state = new float[2][][];
            _state[0] = new float[1][];
            _state[1] = new float[1][];
            _state[0][0] = new float[128];
            _state[1][0] = new float[128];
            _context = Array.Empty<float[]>();
            _lastSr = 0;
            _lastBatchSize = 0;
        }

        public void Dispose()
        {
            _session?.Dispose();
        }

        public class ValidationResult
        {
            public float[][] X { get; }
            public int Sr { get; }

            public ValidationResult(float[][] x, int sr)
            {
                X = x;
                Sr = sr;
            }
        }

        private ValidationResult ValidateInput(float[][] x, int sr)
        {
            if (x.Length == 1)
            {
                x = new float[][] { x[0] };
            }
            if (x.Length > 2)
            {
                throw new ArgumentException($"不正确的音频数据维度: {x[0].Length}");
            }

            if (sr != 16000 && (sr % 16000 == 0))
            {
                int step = sr / 16000;
                float[][] reducedX = new float[x.Length][];

                for (int i = 0; i < x.Length; i++)
                {
                    float[] current = x[i];
                    float[] newArr = new float[(current.Length + step - 1) / step];

                    for (int j = 0, index = 0; j < current.Length; j += step, index++)
                    {
                        newArr[index] = current[j];
                    }

                    reducedX[i] = newArr;
                }

                x = reducedX;
                sr = 16000;
            }

            if (!SAMPLE_RATES.Contains(sr))
            {
                throw new ArgumentException($"只支持采样率 {string.Join(", ", SAMPLE_RATES)} (或 16000 的倍数)");
            }

            if (((float)sr) / x[0].Length > 31.25)
            {
                throw new ArgumentException("输入音频太短");
            }

            return new ValidationResult(x, sr);
        }

        private static float[][] Concatenate(float[][] a, float[][] b)
        {
            if (a.Length != b.Length)
            {
                throw new ArgumentException("两个数组的行数必须相同");
            }

            int rows = a.Length;
            int colsA = a[0].Length;
            int colsB = b[0].Length;
            float[][] result = new float[rows][];

            for (int i = 0; i < rows; i++)
            {
                result[i] = new float[colsA + colsB];
                Array.Copy(a[i], 0, result[i], 0, colsA);
                Array.Copy(b[i], 0, result[i], colsA, colsB);
            }

            return result;
        }

        private static float[][] GetLastColumns(float[][] array, int contextSize)
        {
            int rows = array.Length;
            int cols = array[0].Length;

            if (contextSize > cols)
            {
                throw new ArgumentException("contextSize 不能大于数组的列数");
            }

            float[][] result = new float[rows][];

            for (int i = 0; i < rows; i++)
            {
                result[i] = new float[contextSize];
                Array.Copy(array[i], cols - contextSize, result[i], 0, contextSize);
            }

            return result;
        }

        /// <summary>
        /// 对音频片段进行 VAD 推理
        /// </summary>
        /// <param name="x">音频数据 (batch, samples)</param>
        /// <param name="sr">采样率</param>
        /// <returns>语音概率 (0-1)</returns>
        public float[] Call(float[][] x, int sr)
        {
            var result = ValidateInput(x, sr);
            x = result.X;
            sr = result.Sr;
            int numberSamples = sr == 16000 ? 512 : 256;

            if (x[0].Length != numberSamples)
            {
                throw new ArgumentException($"提供的样本数为 {x[0].Length} (支持的值: 8000 采样率用 256，16000 采样率用 512)");
            }

            int batchSize = x.Length;
            int contextSize = sr == 16000 ? 64 : 32;

            if (_lastBatchSize == 0)
            {
                ResetStates();
            }
            if (_lastSr != 0 && _lastSr != sr)
            {
                ResetStates();
            }
            if (_lastBatchSize != 0 && _lastBatchSize != batchSize)
            {
                ResetStates();
            }

            if (_context.Length == 0)
            {
                _context = new float[batchSize][];
                for (int i = 0; i < batchSize; i++)
                {
                    _context[i] = new float[contextSize];
                }
            }

            x = Concatenate(_context, x);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(x.SelectMany(a => a).ToArray(), new[] { x.Length, x[0].Length })),
                NamedOnnxValue.CreateFromTensor("sr", new DenseTensor<long>(new[] { (long)sr }, new[] { 1 })),
                NamedOnnxValue.CreateFromTensor("state", new DenseTensor<float>(_state.SelectMany(a => a.SelectMany(b => b)).ToArray(), new[] { _state.Length, _state[0].Length, _state[0][0].Length }))
            };

            using (var outputs = _session.Run(inputs))
            {
                var output = outputs.First(o => o.Name == "output").AsTensor<float>();
                var newState = outputs.First(o => o.Name == "stateN").AsTensor<float>();

                _context = GetLastColumns(x, contextSize);
                _lastSr = sr;
                _lastBatchSize = batchSize;

                _state = new float[newState.Dimensions[0]][][];
                for (int i = 0; i < newState.Dimensions[0]; i++)
                {
                    _state[i] = new float[newState.Dimensions[1]][];
                    for (int j = 0; j < newState.Dimensions[1]; j++)
                    {
                        _state[i][j] = new float[newState.Dimensions[2]];
                        for (int k = 0; k < newState.Dimensions[2]; k++)
                        {
                            _state[i][j][k] = newState[i, j, k];
                        }
                    }
                }

                return output.ToArray();
            }
        }
    }
} 