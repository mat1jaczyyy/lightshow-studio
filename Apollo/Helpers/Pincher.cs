using System;

namespace Apollo.Helpers {
    public static class Pincher {
        // https://www.desmos.com/calculator/t74unzeehh

        static double BasicPinch(double actual, double value) => 1 - Math.Pow(1 - Math.Pow(value, actual), 1 / actual);

        public static double ApplyPinch(double time, double total, double pinch, bool bilateral) {
            double actual = (pinch < 0)? ((1 / (1 - pinch)) - 1) * .9 + 1 : 1 + (pinch * 4 / 3);
            double value = Math.Clamp(time / total, 0, 1);

            return total * (bilateral
                ? (time / total < 0.5)
                    ? BasicPinch(actual, 2 * value) / 2
                    : 1 - BasicPinch(actual, 2 * (1 - value)) / 2
                : BasicPinch(actual, value)
            );
        }
    }
}