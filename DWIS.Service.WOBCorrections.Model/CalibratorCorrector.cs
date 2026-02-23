using System.Reflection;
using DWIS.RigOS.Common.Worker;
using DWIS.Service.CorrectedSWOBCorrections.Model;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.General.Common;

namespace DWIS.Service.WOBCorrections.Model
{
    // Surface sample at topside rate
    public record SurfaceSample(
        DateTime Time,
        double BlockPositionZ,   // z
        double Q,                // flowrate
        double Rho,              // fluid density
        double TVDAtBitH,        // h
        double StringLengthL,    // l (bit depth)
        double Td,               // instrumented sub / direct if available
        double Tp,               // load pins
        double Tdl,              // deadline
        double BitDepth,
        double BottomHoleDepth
    );

    // Downhole sample at downhole rate (typically slower than topside)
    public record DownholeSample(
        DateTime Time,
        double StringLengthL,          // l
        double Incl,                   // theta
        double TVDAtBitH,              // h
        double Rho,                    // rho
        double Pi,                     // p_i (string pressure)
        double Pa,                     // p_a (annulus pressure)
        double Omega,                  // downhole rotational speed
        double? TBha                   // downhole tension if available
    );

    public class Window
    {
        public DateTime EndTime;
        public DateTime StartTime;
        public int SurfaceCount;

        // medians
        public double z, Q, rho, h, l;
        public double Td, Tp, Tdl;
        public double BitDepth, BottomHoleDepth;

        // MADs
        public double zMad, QMad, TdMad, TpMad, TdlMad;
        public double BitDepthMad, BottomHoleDepthMad;

        public DownholeSample Dh = default!;

        public double velocity;      // dz/dt
        public double signVelocity;  // sign(dz/dt)
    }
    public class CalibratorCorrector
    {
        public record AlphaCalibrationSituation(
            DateTime Time,
            double TBha,
            double Pi,
            double Pa,
            double Hp,
            double Rho,
            double Theta,
            double Q);

        public record AlphaCalibrationResult(
            double A0,
            double A1,
            double A2,
            double A3,
            double A4,
            int Count,
            double MeanError,
            double StdError,
            double Mae,
            double Rmse);

        public record AlphaStreamingPoint(
            DateTime Time,
            double TBha,
            double PredictedTBha,
            double Error,
            int ActiveParameterCount,
            string ActiveMask);

        public record AlphaStreamingResult(
            double A0,
            double A1,
            double A2,
            double A3,
            double A4,
            int Count,
            double MeanError,
            double StdError,
            double Mae,
            double Rmse,
            IReadOnlyList<AlphaStreamingPoint> Points);

        public record BetaCalibrationSituation(
            DateTime Time,
            double TTopSideOff,
            double H,
            double Rho,
            double Pi,
            double Pa,
            double Q,
            double L);

        public record BetaCalibrationResult(
            double B0,
            double B1,
            double B2,
            double B3,
            double B4,
            int Count,
            double MeanError,
            double StdError,
            double Mae,
            double Rmse);

        public record DCalibrationSituation(
            DateTime Time,
            double Tdl,
            double Z,
            double SignVelocity);

        public record DCalibrationResult(
            double D0,
            double D1,
            double D2,
            int Count,
            double MeanError,
            double StdError,
            double Mae,
            double Rmse);

        public record CCalibrationSituation(
            DateTime Time,
            double Tp,
            double Z,
            double Q);

        public record CCalibrationResult(
            double C0,
            double C1,
            double C2,
            double C3,
            double C4,
            double C5,
            int Count,
            double MeanError,
            double StdError,
            double Mae,
            double Rmse);

        record LinearSituation(DateTime Time, double[] X, double Y);

        record AdaptiveLinearResult(
            double[] Coef,
            int Count,
            double MeanError,
            double StdError,
            double Mae,
            double Rmse,
            int ActiveMask,
            int ActiveCount);

        static readonly object _lock = new();

        // α-model (downhole off-bottom): 5 params
        // T_BHA,off = α0 cosθ + α1 ρ cosθ + α2 ρ h_p + α3 (pi-pa) + α4 ρ Q^2

        // β-model (surface off-bottom): 5 params (no intercept per README)
        // T_top-side,off = β0 h + β1 ρ h + β2 (pi-pa) + β3 ρ Q^2 + β4 ρ l Q^2

        // Artifacts with instrumented sub:
        // f_p(z,Q) on [1, z, z^2, Q, Q^2, zQ]
        // f_dl(z,sign) on [1, z, sign(dz)]

        // Unconnected (in-slips) calibrations:
        // b_d = Td
        // d0 + d1 z + d2 sign(dz) = Tdl
        // c0 + c1 z + c2 z^2 = Tp (Q=0 in unconnected)

        static readonly List<SurfaceSample> Surface = new(capacity: 50000);
        static readonly List<AlphaCalibrationSituation> AlphaSituations = new(capacity: 50000);
        static AlphaCalibrationResult AlphaAdaptive = new(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0, double.NaN, double.NaN, double.NaN, double.NaN);
        const int MaxAlphaSituations = 20000;
        static readonly List<LinearSituation> BetaSituations = new(capacity: 50000);
        static AdaptiveLinearResult BetaAdaptive = new(new double[5], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
        static readonly List<LinearSituation> BdSituations = new(capacity: 50000);
        static AdaptiveLinearResult BdAdaptive = new(new double[1], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
        static readonly List<LinearSituation> FpUnconnectedSituations = new(capacity: 50000);
        static AdaptiveLinearResult FpUnconnectedAdaptive = new(new double[3], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
        static readonly List<LinearSituation> FdlUnconnectedSituations = new(capacity: 50000);
        static AdaptiveLinearResult FdlUnconnectedAdaptive = new(new double[3], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
        static readonly List<LinearSituation> FpWithTdSituations = new(capacity: 50000);
        static AdaptiveLinearResult FpWithTdAdaptive = new(new double[6], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
        static readonly List<LinearSituation> FdlWithTdSituations = new(capacity: 50000);
        static AdaptiveLinearResult FdlWithTdAdaptive = new(new double[3], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
        static readonly List<LinearSituation> FpNoTdSituations = new(capacity: 50000);
        static AdaptiveLinearResult FpNoTdAdaptive = new(new double[6], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
        static readonly List<LinearSituation> FdlNoTdSituations = new(capacity: 50000);
        static AdaptiveLinearResult FdlNoTdAdaptive = new(new double[3], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
        const int MaxGenericSituations = 20000;

        // Legacy behaviour for recommendations (kept)
        static bool _wasOnBottom = false;
        static double? _storedSurfaceWob = null;
        static int _logCounter = 0;
        static double? _initialBd = null;
        static double? _initialC0 = null;
        static double? _initialD0 = null;

        public static void ResetState()
        {
            lock (_lock)
            {
                Surface.Clear();
                AlphaSituations.Clear();
                BetaSituations.Clear();
                BdSituations.Clear();
                FpUnconnectedSituations.Clear();
                FdlUnconnectedSituations.Clear();
                FpWithTdSituations.Clear();
                FdlWithTdSituations.Clear();
                FpNoTdSituations.Clear();
                FdlNoTdSituations.Clear();

                AlphaAdaptive = new AlphaCalibrationResult(
                    double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
                    0, double.NaN, double.NaN, double.NaN, double.NaN);
                BetaAdaptive = new AdaptiveLinearResult(new double[5], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
                BdAdaptive = new AdaptiveLinearResult(new double[1], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
                FpUnconnectedAdaptive = new AdaptiveLinearResult(new double[3], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
                FdlUnconnectedAdaptive = new AdaptiveLinearResult(new double[3], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
                FpWithTdAdaptive = new AdaptiveLinearResult(new double[6], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
                FdlWithTdAdaptive = new AdaptiveLinearResult(new double[3], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
                FpNoTdAdaptive = new AdaptiveLinearResult(new double[6], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
                FdlNoTdAdaptive = new AdaptiveLinearResult(new double[3], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);

                _wasOnBottom = false;
                _storedSurfaceWob = null;
                _logCounter = 0;
                _initialBd = null;
                _initialC0 = null;
                _initialD0 = null;
            }
        }

        public static AlphaCalibrationResult CalibrateAlphaFromSituations(IEnumerable<AlphaCalibrationSituation> situations)
        {
            if (situations is null) throw new ArgumentNullException(nameof(situations));

            var valid = situations
                .Where(s =>
                    double.IsFinite(s.TBha) &&
                    double.IsFinite(s.Pi) &&
                    double.IsFinite(s.Pa) &&
                    double.IsFinite(s.Hp) &&
                    double.IsFinite(s.Rho) &&
                    double.IsFinite(s.Theta) &&
                    double.IsFinite(s.Q))
                .ToList();

            if (valid.Count == 0)
            {
                return new AlphaCalibrationResult(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0, double.NaN, double.NaN, double.NaN, double.NaN);
            }

            const double ridge = 1e-9;
            if (!TryFindBestSubset(valid, valid.Count, Math.Max(8, 5 + 2), ridge, out var a, out _, out _))
            {
                return new AlphaCalibrationResult(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0, double.NaN, double.NaN, double.NaN, double.NaN);
            }
            double sum = 0.0;
            double sumSq = 0.0;
            double sumAbs = 0.0;
            int n = 0;
            foreach (var s in valid)
            {
                double cosTheta = Math.Cos(s.Theta);
                double pred =
                    a[0] * cosTheta +
                    a[1] * s.Rho * cosTheta +
                    a[2] * s.Rho * s.Hp +
                    a[3] * (s.Pi - s.Pa) +
                    a[4] * s.Rho * s.Q * s.Q;
                double e = s.TBha - pred;
                sum += e;
                sumSq += e * e;
                sumAbs += Math.Abs(e);
                n++;
            }

            double mean = sum / n;
            double rmse = Math.Sqrt(sumSq / n);
            double mae = sumAbs / n;
            double var = (sumSq / n) - (mean * mean);
            double std = Math.Sqrt(Math.Max(0.0, var));

            return new AlphaCalibrationResult(a[0], a[1], a[2], a[3], a[4], n, mean, std, mae, rmse);
        }

        public static AlphaStreamingResult CalibrateAlphaStreamingAdaptive(
            IEnumerable<AlphaCalibrationSituation> situations,
            int minSamplesPerModel = 8,
            double ridge = 1e-9)
        {
            if (situations is null) throw new ArgumentNullException(nameof(situations));

            var valid = situations
                .Where(s =>
                    double.IsFinite(s.TBha) &&
                    double.IsFinite(s.Pi) &&
                    double.IsFinite(s.Pa) &&
                    double.IsFinite(s.Hp) &&
                    double.IsFinite(s.Rho) &&
                    double.IsFinite(s.Theta) &&
                    double.IsFinite(s.Q))
                .OrderBy(s => s.Time)
                .ToList();

            if (valid.Count == 0)
            {
                return new AlphaStreamingResult(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0, double.NaN, double.NaN, double.NaN, double.NaN, Array.Empty<AlphaStreamingPoint>());
            }

            var points = new List<AlphaStreamingPoint>(valid.Count);
            double[] lastBest = new double[5];
            int lastMask = 1;

            for (int i = 0; i < valid.Count; i++)
            {
                int n = i + 1;
                bool ok = TryFindBestSubset(valid, n, minSamplesPerModel, ridge, out var bestCoef, out var bestMask, out _);

                if (!ok || bestCoef is null)
                {
                    // Fallback to current single best previous model if stream is too short.
                    bestCoef = (double[])lastBest.Clone();
                    bestMask = lastMask;
                }

                lastBest = bestCoef;
                lastMask = bestMask;

                var s = valid[i];
                double pred = PredictAlpha(s, bestCoef);
                double err = s.TBha - pred;
                points.Add(new AlphaStreamingPoint(
                    Time: s.Time,
                    TBha: s.TBha,
                    PredictedTBha: pred,
                    Error: err,
                    ActiveParameterCount: CountBits(bestMask),
                    ActiveMask: MaskToString(bestMask)));
            }

            double sum = 0.0;
            double sumSq = 0.0;
            double sumAbs = 0.0;
            foreach (var p in points)
            {
                sum += p.Error;
                sumSq += p.Error * p.Error;
                sumAbs += Math.Abs(p.Error);
            }
            int count = points.Count;
            double mean = sum / count;
            double rmseAll = Math.Sqrt(sumSq / count);
            double mae = sumAbs / count;
            double var = (sumSq / count) - mean * mean;
            double std = Math.Sqrt(Math.Max(0.0, var));

            return new AlphaStreamingResult(
                A0: lastBest[0],
                A1: lastBest[1],
                A2: lastBest[2],
                A3: lastBest[3],
                A4: lastBest[4],
                Count: count,
                MeanError: mean,
                StdError: std,
                Mae: mae,
                Rmse: rmseAll,
                Points: points);
        }

        // Specific beta calibration from:
        // T_top-side,off = β0 h + β1 ρh + β2 (pi-pa) + β3 ρQ^2 + β4 ρlQ^2
        public static BetaCalibrationResult CalibrateBetaFromSituations(
            IEnumerable<BetaCalibrationSituation> situations,
            int minSamplesPerModel = 10,
            double ridge = 1e-9)
        {
            if (situations is null) throw new ArgumentNullException(nameof(situations));

            var linear = situations
                .Where(s =>
                    double.IsFinite(s.TTopSideOff) &&
                    double.IsFinite(s.H) &&
                    double.IsFinite(s.Rho) &&
                    double.IsFinite(s.Pi) &&
                    double.IsFinite(s.Pa) &&
                    double.IsFinite(s.Q) &&
                    double.IsFinite(s.L))
                .Select(s =>
                {
                    double x0 = s.H;
                    double x1 = s.Rho * s.H;
                    double x2 = s.Pi - s.Pa;
                    double x3 = s.Rho * s.Q * s.Q;
                    double x4 = s.Rho * s.L * s.Q * s.Q;
                    return new LinearSituation(s.Time, new[] { x0, x1, x2, x3, x4 }, s.TTopSideOff);
                })
                .ToList();

            if (linear.Count == 0)
            {
                return new BetaCalibrationResult(
                    double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
                    0, double.NaN, double.NaN, double.NaN, double.NaN);
            }

            var fit = CalibrateLinearAdaptive(linear, dim: 5, minSamplesPerModel, ridge);
            var b = fit.Coef ?? new double[5];

            return new BetaCalibrationResult(
                B0: b.Length > 0 ? b[0] : double.NaN,
                B1: b.Length > 1 ? b[1] : double.NaN,
                B2: b.Length > 2 ? b[2] : double.NaN,
                B3: b.Length > 3 ? b[3] : double.NaN,
                B4: b.Length > 4 ? b[4] : double.NaN,
                Count: fit.Count,
                MeanError: fit.MeanError,
                StdError: fit.StdError,
                Mae: fit.Mae,
                Rmse: fit.Rmse);
        }

        // Specific d calibration in empty-block/in-slips conditions:
        // T_dl = T_true + f_dl(z, sign(dz)), with T_true = 0 => T_dl = d0 + d1 z + d2 sign(dz)
        public static DCalibrationResult CalibrateDFromSituations(
            IEnumerable<DCalibrationSituation> situations,
            int minSamplesPerModel = 8,
            double ridge = 1e-9)
        {
            if (situations is null) throw new ArgumentNullException(nameof(situations));

            var linear = situations
                .Where(s =>
                    double.IsFinite(s.Tdl) &&
                    double.IsFinite(s.Z) &&
                    double.IsFinite(s.SignVelocity))
                .Select(s =>
                {
                    double x0 = 1.0;
                    double x1 = s.Z;
                    double x2 = s.SignVelocity;
                    return new LinearSituation(s.Time, new[] { x0, x1, x2 }, s.Tdl);
                })
                .ToList();

            if (linear.Count == 0)
            {
                return new DCalibrationResult(
                    double.NaN, double.NaN, double.NaN,
                    0, double.NaN, double.NaN, double.NaN, double.NaN);
            }

            var fit = CalibrateLinearAdaptive(linear, dim: 3, minSamplesPerModel, ridge);
            var d = fit.Coef ?? new double[3];

            return new DCalibrationResult(
                D0: d.Length > 0 ? d[0] : double.NaN,
                D1: d.Length > 1 ? d[1] : double.NaN,
                D2: d.Length > 2 ? d[2] : double.NaN,
                Count: fit.Count,
                MeanError: fit.MeanError,
                StdError: fit.StdError,
                Mae: fit.Mae,
                Rmse: fit.Rmse);
        }

        // Specific c calibration in empty-block/in-slips conditions:
        // T_p = T_true + c0 + c1 z + c2 z^2 + c3 Q + c4 Q^2 + c5 zQ, with T_true = 0 in-slips
        public static CCalibrationResult CalibrateCFromSituations(
            IEnumerable<CCalibrationSituation> situations,
            int minSamplesPerModel = 10,
            double ridge = 1e-9)
        {
            if (situations is null) throw new ArgumentNullException(nameof(situations));

            var linear = situations
                .Where(s =>
                    double.IsFinite(s.Tp) &&
                    double.IsFinite(s.Z) &&
                    double.IsFinite(s.Q))
                .Select(s =>
                {
                    double x0 = 1.0;
                    double x1 = s.Z;
                    double x2 = s.Z * s.Z;
                    double x3 = s.Q;
                    double x4 = s.Q * s.Q;
                    double x5 = s.Z * s.Q;
                    return new LinearSituation(s.Time, new[] { x0, x1, x2, x3, x4, x5 }, s.Tp);
                })
                .ToList();

            if (linear.Count == 0)
            {
                return new CCalibrationResult(
                    double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
                    0, double.NaN, double.NaN, double.NaN, double.NaN);
            }

            var fit = CalibrateLinearAdaptive(linear, dim: 6, minSamplesPerModel, ridge);
            var c = fit.Coef ?? new double[6];

            return new CCalibrationResult(
                C0: c.Length > 0 ? c[0] : double.NaN,
                C1: c.Length > 1 ? c[1] : double.NaN,
                C2: c.Length > 2 ? c[2] : double.NaN,
                C3: c.Length > 3 ? c[3] : double.NaN,
                C4: c.Length > 4 ? c[4] : double.NaN,
                C5: c.Length > 5 ? c[5] : double.NaN,
                Count: fit.Count,
                MeanError: fit.MeanError,
                StdError: fit.StdError,
                Mae: fit.Mae,
                Rmse: fit.Rmse);
        }

        static void AppendAndRefit(
            List<LinearSituation> store,
            ref AdaptiveLinearResult result,
            DateTime time,
            double[] x,
            double y,
            int dim,
            int minSamplesPerModel,
            double ridge = 1e-9)
        {
            if (!double.IsFinite(y) || x.Length != dim || x.Any(v => !double.IsFinite(v))) return;
            store.Add(new LinearSituation(time, (double[])x.Clone(), y));
            if (store.Count > MaxGenericSituations)
            {
                int drop = store.Count - MaxGenericSituations;
                store.RemoveRange(0, drop);
            }
            result = CalibrateLinearAdaptive(store, dim, minSamplesPerModel, ridge);
        }

        static AdaptiveLinearResult CalibrateLinearAdaptive(
            IReadOnlyList<LinearSituation> data,
            int dim,
            int minSamplesPerModel = 8,
            double ridge = 1e-9)
        {
            if (data.Count == 0)
            {
                return new AdaptiveLinearResult(new double[dim], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
            }

            int n = data.Count;
            int bestMask = -1;
            double[]? bestCoef = null;
            double bestScore = double.PositiveInfinity;

            int maxMask = (1 << dim) - 1;
            for (int mask = 1; mask <= maxMask; mask++)
            {
                int k = CountBits(mask);
                int minN = Math.Max(minSamplesPerModel, k + 2);
                if (n < minN) continue;
                if (!FitSubsetGeneric(data, n, dim, mask, ridge, out var coef, out var rmse)) continue;
                if (!double.IsFinite(rmse)) continue;
                double score = n * Math.Log(rmse * rmse + 1e-18) + 2.0 * k;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestMask = mask;
                    bestCoef = coef;
                }
            }

            if (bestCoef is null)
            {
                return new AdaptiveLinearResult(new double[dim], 0, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0);
            }

            double sum = 0.0, sumSq = 0.0, sumAbs = 0.0;
            for (int i = 0; i < n; i++)
            {
                double pred = PredictLinear(bestCoef, data[i].X);
                double e = data[i].Y - pred;
                sum += e;
                sumSq += e * e;
                sumAbs += Math.Abs(e);
            }
            double mean = sum / n;
            double rmseAll = Math.Sqrt(sumSq / n);
            double mae = sumAbs / n;
            double var = (sumSq / n) - (mean * mean);
            double std = Math.Sqrt(Math.Max(0.0, var));
            return new AdaptiveLinearResult(bestCoef, n, mean, std, mae, rmseAll, bestMask, CountBits(bestMask));
        }

        static bool FitSubsetGeneric(
            IReadOnlyList<LinearSituation> data,
            int n,
            int dim,
            int mask,
            double ridge,
            out double[] fullCoef,
            out double rmse)
        {
            fullCoef = new double[dim];
            rmse = double.NaN;
            var idx = new List<int>(dim);
            for (int j = 0; j < dim; j++) if (((mask >> j) & 1) != 0) idx.Add(j);
            int k = idx.Count;
            if (k == 0) return false;

            var xtx = new double[k, k];
            var xty = new double[k];
            for (int r = 0; r < n; r++)
            {
                var xFull = data[r].X;
                double y = data[r].Y;
                for (int i = 0; i < k; i++)
                {
                    double xi = xFull[idx[i]];
                    xty[i] += xi * y;
                    for (int j = 0; j < k; j++) xtx[i, j] += xi * xFull[idx[j]];
                }
            }
            for (int i = 0; i < k; i++) xtx[i, i] += ridge;
            var sub = SolveLinearSystem(xtx, xty);
            if (sub.Any(v => !double.IsFinite(v))) return false;
            for (int i = 0; i < k; i++) fullCoef[idx[i]] = sub[i];

            double sumSq = 0.0;
            for (int r = 0; r < n; r++)
            {
                double pred = PredictLinear(fullCoef, data[r].X);
                double e = data[r].Y - pred;
                sumSq += e * e;
            }
            rmse = Math.Sqrt(sumSq / n);
            return double.IsFinite(rmse);
        }

        static double PredictLinear(double[] coef, double[] x)
        {
            int n = Math.Min(coef.Length, x.Length);
            double s = 0.0;
            for (int i = 0; i < n; i++) s += coef[i] * x[i];
            return s;
        }

        static bool TryFindBestSubset(
            IReadOnlyList<AlphaCalibrationSituation> data,
            int n,
            int minSamplesPerModel,
            double ridge,
            out double[] bestCoef,
            out int bestMask,
            out double bestScore)
        {
            bestCoef = new double[5];
            bestMask = 1;
            bestScore = double.PositiveInfinity;
            bool found = false;

            for (int mask = 1; mask < (1 << 5); mask++)
            {
                int k = CountBits(mask);
                int minN = Math.Max(minSamplesPerModel, k + 2);
                if (n < minN) continue;

                if (!FitSubset(data, n, mask, ridge, out var coef, out var rmse)) continue;
                if (!double.IsFinite(rmse)) continue;

                // AIC-like model selection: prioritize fit quality but penalize complexity.
                double score = n * Math.Log(rmse * rmse + 1e-18) + 2.0 * k;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestMask = mask;
                    bestCoef = coef;
                    found = true;
                }
            }

            return found;
        }

        static bool FitSubset(
            IReadOnlyList<AlphaCalibrationSituation> data,
            int n,
            int mask,
            double ridge,
            out double[] fullCoef,
            out double rmse)
        {
            fullCoef = new double[5];
            rmse = double.NaN;

            var idx = new List<int>(5);
            for (int j = 0; j < 5; j++) if (((mask >> j) & 1) != 0) idx.Add(j);
            int k = idx.Count;
            if (k == 0) return false;

            var xtx = new double[k, k];
            var xty = new double[k];

            for (int r = 0; r < n; r++)
            {
                var xFull = AlphaFeatures(data[r]);
                double y = data[r].TBha;
                for (int i = 0; i < k; i++)
                {
                    double xi = xFull[idx[i]];
                    xty[i] += xi * y;
                    for (int j = 0; j < k; j++) xtx[i, j] += xi * xFull[idx[j]];
                }
            }

            for (int i = 0; i < k; i++) xtx[i, i] += ridge;
            var coefSub = SolveLinearSystem(xtx, xty);
            if (coefSub.Any(v => !double.IsFinite(v))) return false;

            for (int i = 0; i < k; i++) fullCoef[idx[i]] = coefSub[i];

            double sumSq = 0.0;
            for (int r = 0; r < n; r++)
            {
                double pred = PredictAlpha(data[r], fullCoef);
                double e = data[r].TBha - pred;
                sumSq += e * e;
            }
            rmse = Math.Sqrt(sumSq / n);
            return double.IsFinite(rmse);
        }

        static double[] AlphaFeatures(AlphaCalibrationSituation s)
        {
            double cosTheta = Math.Cos(s.Theta);
            return new[]
            {
                cosTheta,
                s.Rho * cosTheta,
                s.Rho * s.Hp,
                s.Pi - s.Pa,
                s.Rho * s.Q * s.Q
            };
        }

        static double PredictAlpha(AlphaCalibrationSituation s, double[] a)
        {
            var x = AlphaFeatures(s);
            return a[0] * x[0] + a[1] * x[1] + a[2] * x[2] + a[3] * x[3] + a[4] * x[4];
        }

        static int CountBits(int x)
        {
            int c = 0;
            while (x != 0) { c += x & 1; x >>= 1; }
            return c;
        }

        static string MaskToString(int mask)
        {
            // [a0,a1,a2,a3,a4] as 0/1 flags
            return $"{((mask & 1) != 0 ? 1 : 0)}{((mask & 2) != 0 ? 1 : 0)}{((mask & 4) != 0 ? 1 : 0)}{((mask & 8) != 0 ? 1 : 0)}{((mask & 16) != 0 ? 1 : 0)}";
        }

        static double[] SolveLinearSystem(double[,] a, double[] b)
        {
            int n = b.Length;
            var m = new double[n, n + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) m[i, j] = a[i, j];
                m[i, n] = b[i];
            }

            for (int k = 0; k < n; k++)
            {
                int piv = k;
                double pivAbs = Math.Abs(m[k, k]);
                for (int r = k + 1; r < n; r++)
                {
                    double v = Math.Abs(m[r, k]);
                    if (v > pivAbs) { pivAbs = v; piv = r; }
                }
                if (pivAbs < 1e-18)
                {
                    var nan = new double[n];
                    for (int i = 0; i < n; i++) nan[i] = double.NaN;
                    return nan;
                }
                if (piv != k)
                {
                    for (int c = k; c <= n; c++)
                    {
                        double tmp = m[k, c];
                        m[k, c] = m[piv, c];
                        m[piv, c] = tmp;
                    }
                }

                double d = m[k, k];
                for (int c = k; c <= n; c++) m[k, c] /= d;

                for (int r = 0; r < n; r++)
                {
                    if (r == k) continue;
                    double f = m[r, k];
                    if (Math.Abs(f) < 1e-24) continue;
                    for (int c = k; c <= n; c++) m[r, c] -= f * m[k, c];
                }
            }

            var xOut = new double[n];
            for (int i = 0; i < n; i++) xOut[i] = m[i, n];
            return xOut;
        }

        public static void Process(
            ILogger<IDWISWorker<ConfigurationForWOBCorrection>>? logger,
            DateTime now,
            TopSideMeasurementsReadable topSide,
            DownholeMeasurementsData downhole,
            ComposerRecommendationsData composerRecommendationsData,
            double SensorToBitDistance,
            ConfigurationForWOBCorrection? configuration,
            CorrectedMeasurementsData correctedMeasurements,
            CorrectedRecommendationsData correctedRecommendationsData)
        {
            TimeSpan WindowDuration = ConfigurationForWOBCorrection.WindowDurationDefault;
            TimeSpan MaxSurfaceAge = ConfigurationForWOBCorrection.MaxSurfaceAgeDefault;
            int MinSurfaceSamplesPerWindow = ConfigurationForWOBCorrection.MinSurfaceSamplesPerWindowDefault;
            double DepthMargin = ConfigurationForWOBCorrection.DepthMarginDefault;
            double MinDownholeRotationalSpeed = ConfigurationForWOBCorrection.MinDownholeRotationalSpeedDefault;
            double MaxRelQMad = ConfigurationForWOBCorrection.MaxRelQMadDefault;
            double MaxRelTMad = ConfigurationForWOBCorrection.MaxRelTMadDefault;
            double MaxDepthMad = ConfigurationForWOBCorrection.MaxDepthMadDefault;
            double MinVelocityForMotion = ConfigurationForWOBCorrection.MinVelocityForMotionDefault;
            double FactorThresholdInSlips = ConfigurationForWOBCorrection.FactorThresholdInSlipsDefault;
            double DeltaTensionInSlips = ConfigurationForWOBCorrection.DeltaTensionInSlipsDefault;
            double MinDistanceInSlips = ConfigurationForWOBCorrection.MinDistanceInSlipsDefault;

            if (configuration is not null)
            {
                if (configuration.WindowDuration is not null) WindowDuration = configuration.WindowDuration.Value;
                if (configuration.MaxSurfaceAge is not null) MaxSurfaceAge = configuration.MaxSurfaceAge.Value;
                if (configuration.MinSurfaceSamplesPerWindow is not null) MinSurfaceSamplesPerWindow = configuration.MinSurfaceSamplesPerWindow.Value;
                if (configuration.DepthMargin is not null) DepthMargin = configuration.DepthMargin.Value;
                if (configuration.MinDownholeRotationalSpeed is not null) MinDownholeRotationalSpeed = configuration.MinDownholeRotationalSpeed.Value;
                if (configuration.MaxRelQMad is not null) MaxRelQMad = configuration.MaxRelQMad.Value;
                if (configuration.MaxRelTMad is not null) MaxRelTMad = configuration.MaxRelTMad.Value;
                if (configuration.MaxDepthMad is not null) MaxDepthMad = configuration.MaxDepthMad.Value;
                if (configuration.MinVelocityForMotion is not null) MinVelocityForMotion = configuration.MinVelocityForMotion.Value;
                if (configuration.FactorThresholdInSlips is not null) FactorThresholdInSlips = configuration.FactorThresholdInSlips.Value;
                if (configuration.DeltaTensionInSlips is not null) DeltaTensionInSlips = configuration.DeltaTensionInSlips.Value;
                if (configuration.MinDistanceInSlips is not null) MinDistanceInSlips = configuration.MinDistanceInSlips.Value;
            }

            if (topSide == null) throw new ArgumentNullException(nameof(topSide));
            if (downhole == null) throw new ArgumentNullException(nameof(downhole));
            if (composerRecommendationsData == null) throw new ArgumentNullException(nameof(composerRecommendationsData));
            if (correctedMeasurements == null) throw new ArgumentNullException(nameof(correctedMeasurements));
            if (correctedRecommendationsData == null) throw new ArgumentNullException(nameof(correctedRecommendationsData));

            if (!TryBuildSurfaceSample(now, topSide, out var surfaceSample))
            {
                logger?.LogWarning("CalibratorCorrector: missing top-side inputs; skipping.");
                return;
            }

            if (!TryBuildDownholeSample(now, topSide, downhole, out var downholeSample))
            {
                logger?.LogWarning("CalibratorCorrector: missing downhole inputs; skipping.");
                return;
            }

            lock (_lock)
            {
                Surface.Add(surfaceSample);

                // prune old surface samples
                var pruneBefore = surfaceSample.Time - MaxSurfaceAge;
                int removeCount = 0;
                while (removeCount < Surface.Count && Surface[removeCount].Time < pruneBefore) removeCount++;
                if (removeCount > 0) Surface.RemoveRange(0, removeCount);

                // Build a downhole-triggered window
                var w = BuildWindow(Surface, downholeSample, WindowDuration, MinSurfaceSamplesPerWindow, MinVelocityForMotion);
                if (w == null)
                {
                    logger?.LogWarning("CalibratorCorrector: not enough surface samples for window; skipping.");
                    return;
                }

                if (!PassQualityGates(w, MaxRelQMad, MaxRelTMad, MaxDepthMad))
                {
                    logger?.LogWarning("CalibratorCorrector: quality gates failed; skipping.");
                    return;
                }

                bool onBottom = (w.BitDepth >= w.BottomHoleDepth - DepthMargin);
                // Strictly below the threshold to avoid overlap with onBottom at exact boundary.
                bool offBottom = (w.BitDepth < w.BottomHoleDepth - DepthMargin);
                bool isMoving = w.signVelocity != 0.0;

                bool omegaOk = w.Dh.Omega >= MinDownholeRotationalSpeed;
                bool allowCalibrationUpdate = offBottom && omegaOk;

                // Window scalars
                double z = w.z;
                double Q = w.Q;
                double rho = w.rho;
                double h = w.h;
                double l = w.l;

                double theta = w.Dh.Incl;
                double cosTheta = Math.Cos(theta);

                // Straight-line hp based on passed inclination
                double hp = h - SensorToBitDistance * cosTheta;
                double lp = SensorToBitDistance; // l_p is the sensor->bit distance

                // Pressures at same depth (downhole sample)
                double dp = w.Dh.Pi - w.Dh.Pa;
                double visc = rho * (l - lp) * Q * Q; // ρ(l-l_p)Q^2

                // Determine what sensors we effectively have:
                bool hasTd = !double.IsNaN(w.Td);
                bool hasTp = !double.IsNaN(w.Tp);
                bool hasTdl = !double.IsNaN(w.Tdl);
                bool hasDownholeTension = w.Dh.TBha.HasValue;
                if (_initialBd is null && hasTd) _initialBd = EstimateSlipsLevel(Surface, s => s.Td, MinDistanceInSlips, DeltaTensionInSlips);
                if (_initialC0 is null && hasTp) _initialC0 = EstimateSlipsLevel(Surface, s => s.Tp, MinDistanceInSlips, DeltaTensionInSlips);
                if (_initialD0 is null && hasTdl) _initialD0 = EstimateSlipsLevel(Surface, s => s.Tdl, MinDistanceInSlips, DeltaTensionInSlips);

                double bdConst = BdAdaptive.Count > 0 && BdAdaptive.Coef.Length > 0 ? BdAdaptive.Coef[0] : double.NaN;
                double c0Const = FpUnconnectedAdaptive.Count > 0 && FpUnconnectedAdaptive.Coef.Length > 0 ? FpUnconnectedAdaptive.Coef[0] : double.NaN;
                double d0Const = FdlUnconnectedAdaptive.Count > 0 && FdlUnconnectedAdaptive.Coef.Length > 0 ? FdlUnconnectedAdaptive.Coef[0] : double.NaN;
                double bdRef = GetReferenceLevel(hasTd ? w.Td : double.NaN, bdConst, _initialBd);
                double c0Ref = GetReferenceLevel(hasTp ? w.Tp : double.NaN, c0Const, _initialC0);
                double d0Ref = GetReferenceLevel(hasTdl ? w.Tdl : double.NaN, d0Const, _initialD0);

                int slipsVotes = 0;
                int slipsConsidered = 0;
                VoteInSlips(hasTd, w.Td, bdRef, FactorThresholdInSlips, ref slipsVotes, ref slipsConsidered);
                VoteInSlips(hasTp, w.Tp, c0Ref, FactorThresholdInSlips, ref slipsVotes, ref slipsConsidered);
                VoteInSlips(hasTdl, w.Tdl, d0Ref, FactorThresholdInSlips, ref slipsVotes, ref slipsConsidered);

                bool isUnconnected = offBottom && isMoving && slipsConsidered > 0 && (2 * slipsVotes >= slipsConsidered);
                bool isConnectedForCalibration = allowCalibrationUpdate && !isUnconnected;
                bool isUnconnectedForCalibration = offBottom && isMoving && isUnconnected;
                double bd = BdAdaptive.Count > 0 && BdAdaptive.Coef.Length > 0 ? BdAdaptive.Coef[0] : (_initialBd ?? 0.0);

                if (isUnconnectedForCalibration)
                {
                    if (hasTd) AppendAndRefit(BdSituations, ref BdAdaptive, w.EndTime, new[] { 1.0 }, w.Td, 1, 8);
                    if (hasTp) AppendAndRefit(FpUnconnectedSituations, ref FpUnconnectedAdaptive, w.EndTime, new[] { 1.0, z, z * z }, w.Tp, 3, 8);
                    if (hasTdl) AppendAndRefit(FdlUnconnectedSituations, ref FdlUnconnectedAdaptive, w.EndTime, new[] { 1.0, z, w.signVelocity }, w.Tdl, 3, 8);
                }

                // ========= 1) Calibrate α-model from downhole tension =========
                // xAlpha = [cosθ, ρcosθ, ρhp, (pi-pa), ρQ^2]
                double[] xAlpha = new[]
                {
                    cosTheta,
                    rho * cosTheta,
                    rho * hp,
                    dp,
                    rho * Q * Q
                };

                if (isConnectedForCalibration && hasDownholeTension)
                {
                    AlphaSituations.Add(new AlphaCalibrationSituation(
                        Time: w.Dh.Time,
                        TBha: w.Dh.TBha!.Value,
                        Pi: w.Dh.Pi,
                        Pa: w.Dh.Pa,
                        Hp: hp,
                        Rho: rho,
                        Theta: theta,
                        Q: Q));

                    if (AlphaSituations.Count > MaxAlphaSituations)
                    {
                        int drop = AlphaSituations.Count - MaxAlphaSituations;
                        AlphaSituations.RemoveRange(0, drop);
                    }

                    AlphaAdaptive = CalibrateAlphaFromSituations(AlphaSituations);
                }

                // ========= 2) Calibrate artifacts =========
                // f_p (pins)
                double fp;
                // f_dl (deadline)
                double fdl;

                if (hasTd)
                {
                    // With instrumented sub
                    double[] xFp = { 1.0, z, z * z, Q, Q * Q, z * Q };
                    double[] xFdl = { 1.0, z, w.signVelocity };

                    if (isConnectedForCalibration)
                    {
                        if (hasTp) AppendAndRefit(FpWithTdSituations, ref FpWithTdAdaptive, w.EndTime, xFp, w.Tp - (w.Td - bd), 6, 10);
                        if (hasTdl) AppendAndRefit(FdlWithTdSituations, ref FdlWithTdAdaptive, w.EndTime, xFdl, w.Tdl - (w.Td - bd), 3, 8);
                    }
                    fp = isUnconnected && FpUnconnectedAdaptive.Count > 0
                        ? PredictLinear(FpUnconnectedAdaptive.Coef, new[] { 1.0, z, z * z })
                        : PredictLinear(FpWithTdAdaptive.Coef, xFp);
                    fdl = isUnconnected && FdlUnconnectedAdaptive.Count > 0
                        ? PredictLinear(FdlUnconnectedAdaptive.Coef, new[] { 1.0, z, w.signVelocity })
                        : PredictLinear(FdlWithTdAdaptive.Coef, xFdl);
                }
                else if (hasDownholeTension)
                {
                    double[] xFp = { 1.0, z, z * z, Q, Q * Q, z * Q };
                    double[] xFdl = { 1.0, z, w.signVelocity };

                    double fpPred = PredictLinear(FpNoTdAdaptive.Coef, xFp);
                    double fdlPred = PredictLinear(FdlNoTdAdaptive.Coef, xFdl);

                    fp = isUnconnected && FpUnconnectedAdaptive.Count > 0
                        ? PredictLinear(FpUnconnectedAdaptive.Coef, new[] { 1.0, z, z * z })
                        : PredictLinear(FpNoTdAdaptive.Coef, xFp);
                    fdl = isUnconnected && FdlUnconnectedAdaptive.Count > 0
                        ? PredictLinear(FdlUnconnectedAdaptive.Coef, new[] { 1.0, z, w.signVelocity })
                        : PredictLinear(FdlNoTdAdaptive.Coef, xFdl);
                }
                else
                {
                    // Not enough info to calibrate artifacts; set to zero
                    fp = 0.0;
                    fdl = 0.0;
                }

                // ========= 3) Choose corrected surface tension T_corr =========
                // README: T_corr = T_measured - f_sensor(z,Q,zdot)
                // For Td: subtract b_d bias
                // For Tp/Tdl: subtract f_p/f_dl
                double Td_corr = hasTd ? w.Td - bd : double.NaN;
                double Tp_corr = hasTp ? w.Tp - fp : double.NaN;
                double Tdl_corr = hasTdl ? w.Tdl - fdl : double.NaN;

                // Choose a "best" corrected tension for SWOB calibration/correction:
                // Prefer Td if present, else average of Tp/Tdl corrected.
                double TcorrForBeta =
                    hasTd ? Td_corr :
                    (hasTp && hasTdl) ? 0.5 * (Tp_corr + Tdl_corr) :
                    hasTp ? Tp_corr :
                    hasTdl ? Tdl_corr :
                    double.NaN;

                // ========= 4) Calibrate β-model from off-bottom corrected surface tension =========
                // xBeta = [h, ρh, (pi-pa), ρQ^2, ρ l Q^2]
                double[] xBeta = new[]
                {
                    h,
                    rho * h,
                    dp,
                    rho * Q * Q,
                    rho * l * Q * Q
                };
                // Beta baseline calibration is restricted to off-bottom, rotating, off-slips windows.
                if (isConnectedForCalibration && !double.IsNaN(TcorrForBeta))
                {
                    AppendAndRefit(BetaSituations, ref BetaAdaptive, w.EndTime, xBeta, TcorrForBeta, 5, 10);
                }
                if (isConnectedForCalibration && !double.IsNaN(TcorrForBeta))
                {
                    double sigmaAlpha = AlphaAdaptive.StdError;
                    int nAlpha = AlphaAdaptive.Count;
                    double sigmaBeta = BetaAdaptive.StdError;
                    int nBeta = BetaAdaptive.Count;
                    double sigmaBd = BdAdaptive.StdError;
                    int nBd = BdAdaptive.Count;
                    logger?.LogInformation(
                        $"CalibratorCorrector: calibration updated. " +
                        $"state={(isUnconnected ? "unconnected" : "connected")} " +
                        $"sigma(alpha)={sigmaAlpha:G6} n={nAlpha} " +
                        $"sigma(beta)={sigmaBeta:G6} n={nBeta} " +
                        $"sigma(bd)={sigmaBd:G6} n={nBd} " +
                        $"sigma(fp|slips)={FpUnconnectedAdaptive.StdError:G6} n={FpUnconnectedAdaptive.Count} " +
                        $"sigma(fdl|slips)={FdlUnconnectedAdaptive.StdError:G6} n={FdlUnconnectedAdaptive.Count} " +
                        $"sigma(fp|Td)={FpWithTdAdaptive.StdError:G6} n={FpWithTdAdaptive.Count} " +
                        $"sigma(fdl|Td)={FdlWithTdAdaptive.StdError:G6} n={FdlWithTdAdaptive.Count} " +
                        $"sigma(fp|noTd)={FpNoTdAdaptive.StdError:G6} n={FpNoTdAdaptive.Count} " +
                        $"sigma(fdl|noTd)={FdlNoTdAdaptive.StdError:G6} n={FdlNoTdAdaptive.Count}");
                }

                // ========= 5) Compute corrected WOB outputs =========

                // Downhole corrected WOB 
                // T_DWOB = T_BHA - α0 cosθ - α1 ρ cosθ - α2 ρ h_p - α3 (pi-pa) - α4 ρ Q^2
                bool hasAlphaModel = TryGetAlphaModel(out var alphaModel);
                double alphaBaseline =
                    hasAlphaModel
                    ? alphaModel[0] * cosTheta
                      + alphaModel[1] * (rho * cosTheta)
                      + alphaModel[2] * (rho * hp)
                      + alphaModel[3] * dp
                      + alphaModel[4] * (rho * Q * Q)
                    : double.NaN;

                double correctedDownholeWob = double.NaN;
                if (hasDownholeTension && hasAlphaModel)
                {
                    correctedDownholeWob = w.Dh.TBha!.Value - alphaBaseline;
                }
                // convert from tension to weight (multiply by -1) and apply sign convention (positive WOB means pushing down)
                correctedDownholeWob *= -1;

                // Surface corrected WOB:
                // F_SWOB = T_corr - beta0 h - beta1 ρ h - beta2 (pi-pa) - beta3 ρ Q^2 - beta4 ρ l Q^2
                double betaPred = PredictLinear(BetaAdaptive.Coef, xBeta);

                // Choose which surface measurement you want to output as "CorrectedSurfaceWeightOnBit":
                // If on-bottom, you may prefer Tp_corr or Td_corr depending on sensor installed; here: prefer Td if present.
                double TcorrForOutput =
                    hasTd ? Td_corr :
                    hasTp ? Tp_corr :
                    hasTdl ? Tdl_corr :
                    double.NaN;

                // F_SWOB1 from beta-model
                double fSwob1 = BetaAdaptive.Count > 0 ? (TcorrForOutput - betaPred) : double.NaN;
                double sigmaSensor =
                    hasTd ? BdAdaptive.StdError :
                    hasTp ? (isUnconnected ? FpUnconnectedAdaptive.StdError : FpNoTdAdaptive.StdError) :
                    hasTdl ? (isUnconnected ? FdlUnconnectedAdaptive.StdError : FdlNoTdAdaptive.StdError) :
                    double.NaN;
                double sigma1 = CombineSigmas(sigmaSensor, BetaAdaptive.StdError);

                double correctedSurfaceWob = fSwob1;
                correctedSurfaceWob *= -1; // apply sign convention (positive WOB means pushing down)

                // WOB reporting policy:
                // - in-slips/unconnected => do not report WOB
                // - off-slips but not rotating => do not report WOB
                bool shouldReportWob = !isUnconnected && omegaOk;

                if (correctedMeasurements.CorrectedSurfaceWeightOnBit is not null)
                {
                    correctedMeasurements.CorrectedSurfaceWeightOnBit.Value =
                        (shouldReportWob && !double.IsNaN(correctedSurfaceWob))
                        ? correctedSurfaceWob
                        : null;
                }

                if (correctedMeasurements.CorrectedDownholeWeightOnBit is not null)
                {
                    correctedMeasurements.CorrectedDownholeWeightOnBit.Value =
                        (shouldReportWob && !double.IsNaN(correctedDownholeWob))
                        ? correctedDownholeWob
                        : null;
                }

                if (correctedMeasurements.CorrectedHookLoadAtTopDrive is not null && !double.IsNaN(Tp_corr))
                {
                    correctedMeasurements.CorrectedHookLoadAtTopDrive.Value = Tp_corr;
                }

                if (correctedMeasurements.CorrectedHookLoadAtDeadLine is not null && !double.IsNaN(Tdl_corr))
                {
                    correctedMeasurements.CorrectedHookLoadAtDeadLine.Value = Tdl_corr;
                }

                // ========= 6) Keep existing recommendation correction behaviour =========
                if (onBottom && !_wasOnBottom)
                {
                    if (topSide.SurfaceWeightOnBit is not null && topSide.SurfaceWeightOnBit.Value is not null)
                    {
                        _storedSurfaceWob = topSide.SurfaceWeightOnBit.Value;
                        logger?.LogInformation($"CalibratorCorrector: on-bottom detected. Stored surface WOB={_storedSurfaceWob.Value:G6}.");
                    }
                    else
                    {
                        _storedSurfaceWob = null;
                    }
                }

                if (onBottom &&
                    _storedSurfaceWob.HasValue &&
                    composerRecommendationsData.WOBRecommendedMaximum is not null &&
                    composerRecommendationsData.WOBRecommendedMaximum.Value is not null &&
                    correctedRecommendationsData.CorrectedWOBRecommendedMaximum is not null)
                {
                    correctedRecommendationsData.CorrectedWOBRecommendedMaximum.Value =
                        composerRecommendationsData.WOBRecommendedMaximum.Value + _storedSurfaceWob.Value;
                }

                _wasOnBottom = onBottom;

                _logCounter++;
                if (_logCounter % 20 == 0)
                {
                    logger?.LogInformation(
                        $"CalibratorCorrector: onBottom={onBottom} offBottom={offBottom} state={(isUnconnected ? "unconnected" : "connected")} omegaOk={omegaOk} " +
                        $"SWOB={correctedSurfaceWob:G6} sigmaSWOB={sigma1:G6} " +
                        $"DWOB={(double.IsNaN(correctedDownholeWob) ? double.NaN : correctedDownholeWob):G6}");
                }
            }
        }

        // ====== Build surface sample ======
        static bool TryBuildSurfaceSample(DateTime now, TopSideMeasurementsReadable topSide, out SurfaceSample sample)
        {
            sample = default!;

            // Required:
            if (!TryGetValue(topSide.BlockPosition, out var z)) return false;
            if (!TryGetValue(topSide.BottomOfStringDepth, out var bitDepth)) return false;
            if (!TryGetValue(topSide.BottomHoleDepth, out var bhDepth)) return false;
            if (!TryGetValue(topSide.BottomOfStringVerticalDepth, out var tvdAtBit)) return false;
            if (!TryGetValue(topSide.DrillingFluidDensityIn, out var rho)) return false;
            if (!TryGetValue(topSide.FlowrateIn, out var q)) return false;
            double td = TryGetValue(topSide.MeasuredTensionInstrumentedSub, out var tdVal) ? tdVal : double.NaN;
            double tp = TryGetValue(topSide.HookLoadAtTopDrive, out var tpVal) ? tpVal : double.NaN;
            double tdl = TryGetValue(topSide.HookLoadAtAnchor, out var tdlVal) ? tdlVal : double.NaN;

            // At least one topside tension sensor is needed for SWOB correction.
            if (double.IsNaN(td) && double.IsNaN(tp) && double.IsNaN(tdl)) return false;

            // Here l is "string length" proxy; in your existing code you use BottomOfStringDepth.
            double l = bitDepth;

            sample = new SurfaceSample(
                Time: now,
                BlockPositionZ: z,
                Q: q,
                Rho: rho,
                TVDAtBitH: tvdAtBit,
                StringLengthL: l,
                Td: td,
                Tp: tp,
                Tdl: tdl,
                BitDepth: bitDepth,
                BottomHoleDepth: bhDepth
            );

            return true;
        }

        // ====== Build downhole sample ======
        static bool TryBuildDownholeSample(
            DateTime now,
            TopSideMeasurementsReadable topSide,
            DownholeMeasurementsData downhole,
            out DownholeSample sample)
        {
            sample = default!;

            if (!TryGetValue(topSide.BottomOfStringDepth, out var l)) return false;
            if (!TryGetValue(topSide.BottomOfStringInclination, out var incl)) return false;
            if (!TryGetValue(topSide.BottomOfStringVerticalDepth, out var h)) return false;
            if (!TryGetValue(topSide.DrillingFluidDensityIn, out var rho)) return false;

            if (!TryGetValue(downhole.StringPressure, out var pi)) return false;
            if (!TryGetValue(downhole.AnnulusPressure, out var pa)) return false;
            if (!TryGetValue(downhole.AverageRotationalSpeed, out var omega)) return false;
            if (!TryGetValue(downhole.AverageRawWeight, out var tBha)) return false;

            sample = new DownholeSample(now, l, incl, h, rho, pi, pa, omega, tBha);
            return true;
        }

        // ====== Windowing ======
        static Window? BuildWindow(List<SurfaceSample> surface, DownholeSample dh, TimeSpan dur, int minCount, double minVelForMotion)
        {
            var end = dh.Time;
            var start = end - dur;

            var seg = surface.Where(s => s.Time >= start && s.Time <= end).ToList();
            if (seg.Count < minCount) return null;

            double medZ = Median(seg.Select(x => x.BlockPositionZ));
            double medQ = Median(seg.Select(x => x.Q));
            double medRho = Median(seg.Select(x => x.Rho));
            double medH = Median(seg.Select(x => x.TVDAtBitH));
            double medL = Median(seg.Select(x => x.StringLengthL));

            double medTd = Median(seg.Select(x => x.Td));
            double medTp = Median(seg.Select(x => x.Tp));
            double medTdl = Median(seg.Select(x => x.Tdl));

            double medBit = Median(seg.Select(x => x.BitDepth));
            double medBhd = Median(seg.Select(x => x.BottomHoleDepth));

            double madZ = Mad(seg.Select(x => x.BlockPositionZ), medZ);
            double madQ = Mad(seg.Select(x => x.Q), medQ);
            double madTd = Mad(seg.Select(x => x.Td), medTd);
            double madTp = Mad(seg.Select(x => x.Tp), medTp);
            double madTdl = Mad(seg.Select(x => x.Tdl), medTdl);

            double madBit = Mad(seg.Select(x => x.BitDepth), medBit);
            double madBhd = Mad(seg.Select(x => x.BottomHoleDepth), medBhd);

            // Movement direction from last two surface samples
            double dzdt = 0.0;
            double sign = 0.0;
            if (seg.Count >= 2)
            {
                var last = seg[^1];
                var prev = seg[^2];
                double dt = Math.Max(1e-6, (last.Time - prev.Time).TotalSeconds);
                dzdt = (last.BlockPositionZ - prev.BlockPositionZ) / dt;

                if (dzdt > minVelForMotion) sign = +1.0;
                else if (dzdt < -minVelForMotion) sign = -1.0;
                else sign = 0.0;
            }

            return new Window
            {
                EndTime = end,
                StartTime = start,
                SurfaceCount = seg.Count,

                z = medZ,
                Q = medQ,
                rho = medRho,
                h = medH,
                l = medL,

                Td = medTd,
                Tp = medTp,
                Tdl = medTdl,

                BitDepth = medBit,
                BottomHoleDepth = medBhd,

                zMad = madZ,
                QMad = madQ,
                TdMad = madTd,
                TpMad = madTp,
                TdlMad = madTdl,
                BitDepthMad = madBit,
                BottomHoleDepthMad = madBhd,

                Dh = dh,
                velocity = dzdt,
                signVelocity = sign
            };
        }

        static bool PassQualityGates(Window w, double maxRelQMad, double maxRelTMad, double maxDepthMad)
        {
            double qDen = Math.Max(1e-9, Math.Abs(w.Q));
            if (w.QMad / qDen > maxRelQMad) return false;

            bool BadRel(double mad, double med)
            {
                double den = Math.Max(500.0, Math.Abs(med));
                return (mad / den) > maxRelTMad;
            }

            bool hasTopsideTension = false;
            bool hasUsableTopsideTension = false;

            if (!double.IsNaN(w.Td) && !double.IsNaN(w.TdMad))
            {
                hasTopsideTension = true;
                if (!BadRel(w.TdMad, w.Td)) hasUsableTopsideTension = true;
            }
            if (!double.IsNaN(w.Tp) && !double.IsNaN(w.TpMad))
            {
                hasTopsideTension = true;
                if (!BadRel(w.TpMad, w.Tp)) hasUsableTopsideTension = true;
            }
            if (!double.IsNaN(w.Tdl) && !double.IsNaN(w.TdlMad))
            {
                hasTopsideTension = true;
                if (!BadRel(w.TdlMad, w.Tdl)) hasUsableTopsideTension = true;
            }

            if (hasTopsideTension && !hasUsableTopsideTension) return false;

            return true;
        }

        // ====== Scalar helpers ======
        static bool TryGetValue(ScalarProperty? prop, out double value)
        {
            value = default;
            if (prop?.Value is null) return false;
            value = prop.Value.Value;
            return true;
        }

        static bool TryGetAlphaModel(out double[] a)
        {
            a = Array.Empty<double>();
            if (AlphaAdaptive.Count > 0 &&
                double.IsFinite(AlphaAdaptive.A0) &&
                double.IsFinite(AlphaAdaptive.A1) &&
                double.IsFinite(AlphaAdaptive.A2) &&
                double.IsFinite(AlphaAdaptive.A3) &&
                double.IsFinite(AlphaAdaptive.A4))
            {
                a = new[] { AlphaAdaptive.A0, AlphaAdaptive.A1, AlphaAdaptive.A2, AlphaAdaptive.A3, AlphaAdaptive.A4 };
                return true;
            }
            return false;
        }

        static double GetReferenceLevel(double measured, double modelConstant, double? initialEstimate)
        {
            if (!double.IsNaN(modelConstant)) return modelConstant;
            if (initialEstimate.HasValue) return initialEstimate.Value;
            if (!double.IsNaN(measured)) return measured;
            return double.NaN;
        }

        static void VoteInSlips(bool hasSignal, double value, double referenceLevel, double factor, ref int slipsVotes, ref int considered)
        {
            if (!hasSignal || double.IsNaN(value) || double.IsNaN(referenceLevel) || referenceLevel <= 0.0) return;
            considered++;
            if (value <= factor * referenceLevel) slipsVotes++;
        }

        static double? EstimateSlipsLevel(IEnumerable<SurfaceSample> surface, Func<SurfaceSample, double> selector, double minDistance, double deltaTension)
        {
            var seq = surface.ToList();
            if (seq.Count < 2) return null;

            double? best = null;
            for (int i = 0; i < seq.Count - 1; i++)
            {
                double ti = selector(seq[i]);
                if (double.IsNaN(ti)) continue;
                for (int j = i + 1; j < seq.Count; j++)
                {
                    double tj = selector(seq[j]);
                    if (double.IsNaN(tj)) continue;
                    if (Math.Abs(seq[j].BlockPositionZ - seq[i].BlockPositionZ) < minDistance) continue;
                    if (Math.Abs(tj - ti) < deltaTension) continue;
                    double low = Math.Min(ti, tj);
                    best = best.HasValue ? Math.Min(best.Value, low) : low;
                }
            }

            if (best.HasValue) return best.Value;

            // Fallback for early startup if no jump yet observed.
            var vals = seq.Select(selector).Where(v => !double.IsNaN(v)).ToArray();
            if (vals.Length == 0) return null;
            return vals.Min();
        }

        static bool IsFinitePositive(double x) => !double.IsNaN(x) && !double.IsInfinity(x) && x > 0.0;

        static double CombineSigmas(double sigmaA, double sigmaB)
        {
            bool a = IsFinitePositive(sigmaA);
            bool b = IsFinitePositive(sigmaB);
            if (a && b) return Math.Sqrt(sigmaA * sigmaA + sigmaB * sigmaB);
            if (a) return sigmaA;
            if (b) return sigmaB;
            return double.NaN;
        }

        static double FuseGaussian(double x1, double sigma1, double x2, double sigma2, out double fusedSigma)
        {
            bool h1 = !double.IsNaN(x1);
            bool h2 = !double.IsNaN(x2);
            bool s1 = IsFinitePositive(sigma1);
            bool s2 = IsFinitePositive(sigma2);

            if (h1 && s1 && h2 && s2)
            {
                double w1 = 1.0 / (sigma1 * sigma1);
                double w2 = 1.0 / (sigma2 * sigma2);
                fusedSigma = Math.Sqrt(1.0 / (w1 + w2));
                return (w1 * x1 + w2 * x2) / (w1 + w2);
            }

            if (h1)
            {
                fusedSigma = s1 ? sigma1 : double.NaN;
                return x1;
            }

            if (h2)
            {
                fusedSigma = s2 ? sigma2 : double.NaN;
                return x2;
            }

            fusedSigma = double.NaN;
            return double.NaN;
        }

        // ====== Robust stats ======
        static double Median(IEnumerable<double> values)
        {
            var a = values.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToArray();
            int n = a.Length;
            if (n == 0) return double.NaN;
            return (n % 2 == 1) ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
        }

        static double Mad(IEnumerable<double> values, double median)
            => double.IsNaN(median)
                ? double.NaN
                : Median(values.Where(v => !double.IsNaN(v)).Select(v => Math.Abs(v - median)));
    }
}

