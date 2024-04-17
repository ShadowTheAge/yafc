using System;

namespace Yafc.UI {
    public static class MathUtils {
        public static float Clamp(float value, float min, float max) {
            if (value < min) {
                return min;
            }

            if (value > max) {
                return max;
            }

            return value;
        }

        public static int Clamp(int value, int min, int max) {
            if (value < min) {
                return min;
            }

            if (value > max) {
                return max;
            }

            return value;
        }

        public static int Round(float value) {
            return (int)MathF.Round(value);
        }

        public static int Floor(float value) {
            return (int)MathF.Floor(value);
        }

        public static int Ceil(float value) {
            return (int)MathF.Ceiling(value);
        }

        public static byte FloatToByte(float f) {
            if (f <= 0) {
                return 0;
            }

            if (f >= 1) {
                return 255;
            }

            return (byte)MathF.Round(f * 255);
        }

        public static float LogarithmicToLinear(float value, float logMin, float logMax) {
            if (value < 0f) {
                value = 0f;
            }

            float cur = MathF.Log(value);
            if (cur <= logMin) {
                return 0f;
            }

            if (cur >= logMax) {
                return 1f;
            }

            return (cur - logMin) / (logMax - logMin);
        }

        public static float LinearToLogarithmic(float value, float logMin, float logMax, float min, float max) {
            if (value <= 0f) {
                return min;
            }

            if (value >= 1f) {
                return max;
            }

            float logCur = logMin + ((logMax - logMin) * value);
            return MathF.Exp(logCur);
        }
    }
}
