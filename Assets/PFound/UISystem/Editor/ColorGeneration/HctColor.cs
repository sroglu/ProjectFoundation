using System;
using UnityEngine;

namespace PFound.UISystem.Editor.ColorGeneration
{
    /// <summary>
    /// HCT (Hue-Chroma-Tone) color space implementation.
    ///
    /// HCT combines:
    ///   H — CAM16 hue (0–360)
    ///   C — CAM16 chroma (0–~120)
    ///   T — L* tone from CIELAB (0–100, perceptual lightness)
    ///
    /// Used by Material Design 3 Dynamic Color for tonal palette generation.
    /// Port of MIT-licensed material-color-utilities (Google LLC).
    /// Reference: https://github.com/material-foundation/material-color-utilities
    /// </summary>
    public readonly struct HctColor
    {
        public readonly float Hue;
        public readonly float Chroma;
        public readonly float Tone;

        public HctColor(float hue, float chroma, float tone)
        {
            Hue    = ((hue % 360f) + 360f) % 360f;
            Chroma = Mathf.Max(0f, chroma);
            Tone   = Mathf.Clamp(tone, 0f, 100f);
        }

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Convert a Unity Color (sRGB gamma) to HCT.</summary>
        public static HctColor FromColor(Color color)
        {
            int ri = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            int gi = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            int bi = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
            uint argb = (uint)(0xFF000000 | (ri << 16) | (gi << 8) | bi);
            return FromArgb(argb);
        }

        /// <summary>Convert sRGB ARGB int to HCT.</summary>
        public static HctColor FromArgb(uint argb)
        {
            var cam = Cam16Vc.FromArgb(argb);
            double tone = LstarFromArgb(argb);
            return new HctColor((float)cam.Hue, (float)cam.Chroma, (float)tone);
        }

        /// <summary>Convert this HCT color back to a Unity Color (sRGB gamma).</summary>
        public Color ToColor()
        {
            uint argb = SolveToArgb(Hue, Chroma, Tone);
            float r = ((argb >> 16) & 0xFF) / 255f;
            float g = ((argb >> 8)  & 0xFF) / 255f;
            float b = ((argb)       & 0xFF) / 255f;
            return new Color(r, g, b, 1f);
        }

        /// <summary>Returns a new HCT with the same Hue+Chroma but a different Tone.</summary>
        public HctColor WithTone(float tone) => new HctColor(Hue, Chroma, tone);

        /// <summary>Returns a new HCT with the same Tone+Chroma but a rotated Hue.</summary>
        public HctColor WithHue(float hue) => new HctColor(hue, Chroma, Tone);

        public override string ToString() => $"HCT({Hue:F1}, {Chroma:F1}, {Tone:F1})";

        // ------------------------------------------------------------------ //
        //  CIELAB L* ↔ Y                                                      //
        // ------------------------------------------------------------------ //

        static double LstarFromArgb(uint argb)
        {
            double r = SrgbToLinear(((argb >> 16) & 0xFF) / 255.0);
            double g = SrgbToLinear(((argb >> 8)  & 0xFF) / 255.0);
            double b = SrgbToLinear(((argb)       & 0xFF) / 255.0);
            double y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            return LstarFromY(y * 100.0);
        }

        static double LstarFromY(double y)
        {
            // y on 0-100 scale
            double yn = y / 100.0;
            if (yn <= 216.0 / 24389.0)
                return 24389.0 / 27.0 * yn;
            return 116.0 * Math.Pow(yn, 1.0 / 3.0) - 16.0;
        }

        static double YFromLstar(double lstar)
        {
            if (lstar <= 8.0)
                return lstar / (24389.0 / 27.0) * 100.0;
            double cube = (lstar + 16.0) / 116.0;
            return cube * cube * cube * 100.0;
        }

        // ------------------------------------------------------------------ //
        //  sRGB gamma                                                          //
        // ------------------------------------------------------------------ //

        static double SrgbToLinear(double c) =>
            c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        static double LinearToSrgb(double c) =>
            c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;

        // ------------------------------------------------------------------ //
        //  Solver: HCT → ARGB                                                //
        // ------------------------------------------------------------------ //

        static uint SolveToArgb(double hue, double chroma, double tone)
        {
            if (tone <= 0.0001) return 0xFF000000;
            if (tone >= 99.9999) return 0xFFFFFFFF;

            // Achromatic
            if (chroma < 0.5)
                return GrayArgbFromLstar(tone);

            // Binary search for the highest in-gamut chroma ≤ requested
            double lo = 0.0, hi = chroma;
            uint bestArgb = GrayArgbFromLstar(tone);

            for (int i = 0; i < 20; i++)
            {
                double mid = (lo + hi) * 0.5;
                var candidate = Cam16Vc.Inverse(hue, mid, tone);
                if (candidate.inGamut)
                {
                    bestArgb = candidate.argb;
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }
            return bestArgb;
        }

        static uint GrayArgbFromLstar(double lstar)
        {
            double y01 = YFromLstar(lstar) / 100.0;
            int comp = Mathf.Clamp(Mathf.RoundToInt((float)LinearToSrgb(y01) * 255f), 0, 255);
            return (uint)(0xFF000000 | (comp << 16) | (comp << 8) | comp);
        }

        // ------------------------------------------------------------------ //
        //  CAM16 with standard sRGB viewing conditions                        //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// CAM16 color appearance model under sRGB standard viewing conditions.
        /// Viewing conditions: D65 white, average surround, La = 11.725 cd/m².
        /// </summary>
        static class Cam16Vc
        {
            // --- M16 matrix: XYZ → CAM16 adapted RGB ---
            // (and its inverse for the reverse direction)

            // --- Precomputed viewing condition constants ---
            // D65 white point XYZ (Y = 100 scale)
            const double Xw = 95.047, Yw = 100.0, Zw = 108.883;

            // sRGB standard viewing conditions
            const double La = 11.725;       // adapting luminance
            const double Yb = 20.0;         // background relative luminance

            // Surround = Average
            const double F_vc = 1.0, C_vc = 0.69, Nc = 1.0;

            // Derived constants (computed once)
            static readonly double N_vc;    // Yb / Yw
            static readonly double Nbb;
            static readonly double Ncb;
            static readonly double Fl;
            static readonly double FlRoot;
            static readonly double Z_vc;
            static readonly double Aw;
            static readonly double D;       // degree of adaptation
            static readonly double[] RgbD;  // chromatic adaptation factors

            static Cam16Vc()
            {
                N_vc = Yb / Yw;

                // n-dependent constants
                Nbb = 0.725 * Math.Pow(1.0 / N_vc, 0.2);
                Ncb = Nbb;
                Z_vc = 1.48 + Math.Sqrt(N_vc);

                // Luminance adaptation factor
                double k = 1.0 / (5.0 * La + 1.0);
                double k4 = k * k * k * k;
                Fl = k4 * La + 0.1 * (1.0 - k4) * (1.0 - k4) * Math.Pow(5.0 * La, 1.0 / 3.0);
                FlRoot = Math.Pow(Fl, 0.25);

                // Degree of adaptation
                D = F_vc * (1.0 - (1.0 / 3.6) * Math.Exp((-La - 42.0) / 92.0));
                D = Math.Max(0.0, Math.Min(1.0, D));

                // Chromatic adaptation for D65 white
                double rwC = M16(0, Xw, Yw, Zw);
                double gwC = M16(1, Xw, Yw, Zw);
                double bwC = M16(2, Xw, Yw, Zw);

                RgbD = new double[]
                {
                    D * (Yw / rwC) + 1.0 - D,
                    D * (Yw / gwC) + 1.0 - D,
                    D * (Yw / bwC) + 1.0 - D,
                };

                // Aw = achromatic response for white
                double rW = AdaptedResponse(RgbD[0] * rwC);
                double gW = AdaptedResponse(RgbD[1] * gwC);
                double bW = AdaptedResponse(RgbD[2] * bwC);
                Aw = (2.0 * rW + gW + 0.05 * bW - 0.305) * Nbb;
            }

            // M16 forward: XYZ → CAM16 LMS
            static double M16(int row, double x, double y, double z)
            {
                switch (row)
                {
                    case 0: return  0.401288 * x + 0.650173 * y - 0.051461 * z;
                    case 1: return -0.250268 * x + 1.204414 * y + 0.045854 * z;
                    default: return -0.002079 * x + 0.048952 * y + 0.953127 * z;
                }
            }

            // M16 inverse: CAM16 LMS → XYZ
            static void M16Inv(double r, double g, double b, out double x, out double y, out double z)
            {
                x =  1.8620678 * r - 1.0112547 * g + 0.1491868 * b;
                y =  0.3875265 * r + 0.6214474 * g - 0.0089740 * b;
                z = -0.0158415 * r - 0.0341229 * g + 1.0499644 * b;
            }

            // Adapted nonlinear response (post-adaptation compression)
            static double AdaptedResponse(double component)
            {
                double abs = Math.Abs(component);
                double adapted = 400.0 * Math.Pow(Fl * abs / 100.0, 0.42)
                                 / (Math.Pow(Fl * abs / 100.0, 0.42) + 27.13);
                return Math.Sign(component) * adapted;
            }

            // Inverse of AdaptedResponse
            static double InverseAdaptedResponse(double adapted)
            {
                double abs = Math.Abs(adapted);
                if (abs < 1e-12) return 0.0;
                double p = 27.13 * abs / (400.0 - abs);
                return Math.Sign(adapted) * (100.0 / Fl) * Math.Pow(p, 1.0 / 0.42);
            }

            // ---- Forward: ARGB → CAM16 ----
            public readonly struct Result
            {
                public readonly double Hue, Chroma;
                public Result(double h, double c) { Hue = h; Chroma = c; }
            }

            public static Result FromArgb(uint argb)
            {
                double r = SrgbToLinear(((argb >> 16) & 0xFF) / 255.0);
                double g = SrgbToLinear(((argb >> 8)  & 0xFF) / 255.0);
                double b = SrgbToLinear(((argb)       & 0xFF) / 255.0);

                // Linear RGB → XYZ (Y=100 scale)
                double x = (0.41233895 * r + 0.35762064 * g + 0.18051042 * b) * 100.0;
                double y = (0.21263901 * r + 0.71516868 * g + 0.07219232 * b) * 100.0;
                double z = (0.01933082 * r + 0.11919478 * g + 0.95053215 * b) * 100.0;

                // XYZ → adapted LMS
                double rC = M16(0, x, y, z);
                double gC = M16(1, x, y, z);
                double bC = M16(2, x, y, z);

                // Chromatic adaptation
                double rD = RgbD[0] * rC;
                double gD = RgbD[1] * gC;
                double bD = RgbD[2] * bC;

                // Post-adaptation nonlinear compression
                double rA = AdaptedResponse(rD);
                double gA = AdaptedResponse(gD);
                double bA = AdaptedResponse(bD);

                // Opponent channels
                double a = rA - 12.0 * gA / 11.0 + bA / 11.0;
                double bOpp = (rA + gA - 2.0 * bA) / 9.0;

                // Hue
                double hRad = Math.Atan2(bOpp, a);
                double hDeg = hRad * 180.0 / Math.PI;
                if (hDeg < 0) hDeg += 360.0;

                // Achromatic response
                double ac = (2.0 * rA + gA + 0.05 * bA - 0.305) * Nbb;

                // J (lightness)
                double j = 100.0 * Math.Pow(ac / Aw, C_vc * Z_vc);

                // Eccentricity factor
                double huePrime = hDeg < 20.14 ? hDeg + 360.0 : hDeg;
                double eHue = 0.25 * (Math.Cos(huePrime * Math.PI / 180.0 + 2.0) + 3.8);

                // t (colorfulness correlate precursor)
                double u = (20.0 * rA + 20.0 * gA + 21.0 * bA) / 20.0;
                double p1 = 50000.0 / 13.0 * eHue * Nc * Ncb;
                double t = p1 * Math.Sqrt(a * a + bOpp * bOpp) / (u + 0.305);

                // Chroma
                double alpha = Math.Pow(t, 0.9) * Math.Pow(1.64 - Math.Pow(0.29, N_vc), 0.73);
                double chroma = alpha * Math.Sqrt(j / 100.0);

                return new Result(hDeg, chroma);
            }

            // ---- Inverse: (hue, chroma, tone) → ARGB ----
            public struct InverseResult
            {
                public uint argb;
                public bool inGamut;
            }

            public static InverseResult Inverse(double hue, double chroma, double tone)
            {
                // tone → Y → J
                double y = YFromLstar(tone);
                // For the achromatic case, find J from Y
                // We use the relationship: for achromatic colors, the adapted response
                // for all channels is the same, proportional to Y.
                // Approximate J by computing it from the achromatic response at luminance Y.
                double j = JFromTone(tone);
                if (j <= 0.0)
                    return new InverseResult { argb = 0xFF000000, inGamut = true };

                // alpha from chroma
                double jRoot = Math.Sqrt(j / 100.0);
                double alpha = chroma < 0.001 ? 0.0 : chroma / jRoot;
                double t = Math.Pow(alpha / Math.Pow(1.64 - Math.Pow(0.29, N_vc), 0.73), 1.0 / 0.9);

                double hRad = hue * Math.PI / 180.0;

                // Eccentricity factor
                double huePrime = hue < 20.14 ? hue + 360.0 : hue;
                double eHue = 0.25 * (Math.Cos(huePrime * Math.PI / 180.0 + 2.0) + 3.8);

                // Achromatic response for this J
                double ac = Aw * Math.Pow(j / 100.0, 1.0 / (C_vc * Z_vc));
                double p2 = ac / Nbb;

                double p1 = 50000.0 / 13.0 * eHue * Nc * Ncb;

                double hSin = Math.Sin(hRad);
                double hCos = Math.Cos(hRad);

                double gamma = 23.0 * (p2 + 0.305) * t
                               / (23.0 * p1 + 11.0 * t * hCos + 108.0 * t * hSin);
                double a = gamma * hCos;
                double b = gamma * hSin;

                // Solve for adapted RGB
                double rA = (460.0 * p2 + 451.0 * a + 288.0 * b) / 1403.0;
                double gA = (460.0 * p2 - 891.0 * a - 261.0 * b) / 1403.0;
                double bA = (460.0 * p2 - 220.0 * a - 6300.0 * b) / 1403.0;

                // Undo adapted response
                double rD = InverseAdaptedResponse(rA);
                double gD = InverseAdaptedResponse(gA);
                double bD = InverseAdaptedResponse(bA);

                // Undo chromatic adaptation
                double rC = rD / RgbD[0];
                double gC = gD / RgbD[1];
                double bC = bD / RgbD[2];

                // CAM16 LMS → XYZ
                M16Inv(rC, gC, bC, out double x, out double oy, out double oz);

                // XYZ → linear RGB
                double rLin = ( 3.2404542 * x - 1.5371385 * oy - 0.4985314 * oz) / 100.0;
                double gLin = (-0.9692660 * x + 1.8760108 * oy + 0.0415560 * oz) / 100.0;
                double bLin = ( 0.0556434 * x - 0.2040259 * oy + 1.0572252 * oz) / 100.0;

                // Check gamut (before clamping)
                const double tolerance = 0.002;
                bool inGamut = rLin >= -tolerance && rLin <= 1.0 + tolerance
                            && gLin >= -tolerance && gLin <= 1.0 + tolerance
                            && bLin >= -tolerance && bLin <= 1.0 + tolerance;

                int ri = Clamp255(LinearToSrgb(Math.Max(0, Math.Min(1, rLin))));
                int gi = Clamp255(LinearToSrgb(Math.Max(0, Math.Min(1, gLin))));
                int bi = Clamp255(LinearToSrgb(Math.Max(0, Math.Min(1, bLin))));
                uint argb = (uint)(0xFF000000 | (ri << 16) | (gi << 8) | bi);

                return new InverseResult { argb = argb, inGamut = inGamut };
            }

            /// <summary>
            /// Compute CAM16 J from L* tone by simulating an achromatic color.
            /// For achromatic colors, all three adapted responses are equal.
            /// </summary>
            static double JFromTone(double tone)
            {
                // Get Y from L*
                double y01 = YFromLstar(tone) / 100.0;
                if (y01 <= 0.0) return 0.0;

                // For a gray at luminance Y:
                // linear RGB = (y01, y01, y01)
                // XYZ: x = 0.9505 * Y, y = Y, z = 1.089 * Y (for D65 illuminant)
                // But for pure gray: x = 95.047*y01, y = 100*y01, z = 108.883*y01
                double xG = Xw * y01;
                double yG = Yw * y01;
                double zG = Zw * y01;

                double rC = M16(0, xG, yG, zG);
                double gC = M16(1, xG, yG, zG);
                double bC = M16(2, xG, yG, zG);

                double rD = RgbD[0] * rC;
                double gD = RgbD[1] * gC;
                double bD = RgbD[2] * bC;

                double rA = AdaptedResponse(rD);
                double gA = AdaptedResponse(gD);
                double bA = AdaptedResponse(bD);

                double ac = (2.0 * rA + gA + 0.05 * bA - 0.305) * Nbb;
                return 100.0 * Math.Pow(ac / Aw, C_vc * Z_vc);
            }

            static int Clamp255(double v) =>
                (int)Math.Round(Math.Max(0.0, Math.Min(255.0, v * 255.0)));
        }
    }
}
