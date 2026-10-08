using System;
using System.Threading;

namespace YuiPhysicalAI.Audio
{
    // Original phase-vocoder implementation. Pitch changes preserve sample count / playback speed.
    public static class YuiSpeechPitch
    {
        public static float[] Shift(float[] input, float semitones, CancellationToken token = default)
        {
            if (float.IsNaN(semitones) || float.IsInfinity(semitones) || Math.Abs(semitones) > 4)
                throw new ArgumentOutOfRangeException(nameof(semitones));
            if (Math.Abs(semitones) < 0.001) return input;
            const int n = 1024, hop = 256;
            var ratio = Math.Pow(2, semitones / 12.0);
            var output = new double[input.Length + 2 * n]; var weights = new double[output.Length];
            var re = new double[n]; var im = new double[n];
            var last = new double[n / 2 + 1]; var phase = new double[last.Length];
            var magnitudes = new double[last.Length]; var frequencies = new double[last.Length];
            for (int start = -n; start < input.Length; start += hop)
            {
                token.ThrowIfCancellationRequested();
                for (int i = 0; i < n; i++) { int at = start + i; re[i] = (at >= 0 && at < input.Length ? input[at] : 0) * Window(i, n); im[i] = 0; }
                Fft(re, im, false); Array.Clear(magnitudes, 0, magnitudes.Length); Array.Clear(frequencies, 0, frequencies.Length);
                for (int k = 0; k <= n / 2; k++)
                {
                    var angle = Math.Atan2(im[k], re[k]); var expected = 2 * Math.PI * k * hop / n;
                    var difference = angle - last[k] - expected; difference -= 2 * Math.PI * Math.Round(difference / (2 * Math.PI)); last[k] = angle;
                    var frequency = (k + difference * n / (2 * Math.PI * hop)) * ratio;
                    int target = (int)Math.Round(k * ratio);
                    if (target > n / 2) continue;
                    var magnitude = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                    magnitudes[target] += magnitude; frequencies[target] += magnitude * frequency;
                }
                Array.Clear(re, 0, n); Array.Clear(im, 0, n);
                for (int k = 0; k <= n / 2; k++)
                {
                    var frequency = magnitudes[k] > 0 ? frequencies[k] / magnitudes[k] : k;
                    phase[k] += 2 * Math.PI * frequency * hop / n;
                    re[k] = magnitudes[k] * Math.Cos(phase[k]); im[k] = magnitudes[k] * Math.Sin(phase[k]);
                    if (k > 0 && k < n / 2) { re[n - k] = re[k]; im[n - k] = -im[k]; }
                }
                Fft(re, im, true);
                for (int i = 0; i < n; i++) { int at = start + i; if (at < 0 || at >= input.Length) continue; var w = Window(i, n); output[at] += re[i] * w; weights[at] += w * w; }
            }
            var result = new float[input.Length];
            for (int i = 0; i < result.Length; i++) result[i] = (float)Math.Max(-1, Math.Min(1, output[i] / Math.Max(0.001, weights[i])));
            return result;
        }
        private static double Window(int i, int n) => 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / n);
        private static void Fft(double[] re, double[] im, bool inverse)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++) { int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit; if (i < j) { var r = re[i]; re[i] = re[j]; re[j] = r; var m = im[i]; im[i] = im[j]; im[j] = m; } }
            for (int length = 2; length <= n; length <<= 1)
            {
                double angle = (inverse ? 2 : -2) * Math.PI / length;
                for (int offset = 0; offset < n; offset += length)
                    for (int j = 0; j < length / 2; j++) { var c = Math.Cos(angle * j); var s = Math.Sin(angle * j); int a = offset + j, b = a + length / 2; var r = re[b] * c - im[b] * s; var m = re[b] * s + im[b] * c; re[b] = re[a] - r; im[b] = im[a] - m; re[a] += r; im[a] += m; }
            }
            if (inverse) for (int i = 0; i < n; i++) { re[i] /= n; im[i] /= n; }
        }
    }
}
