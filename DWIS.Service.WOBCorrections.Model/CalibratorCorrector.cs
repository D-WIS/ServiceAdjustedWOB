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

    /// <summary>
    /// Robust RLS: y ~= beta^T x, with forgetting factor + Huber weights.
    /// </summary>
    public class Rls
    {
        public readonly int Dim;
        public readonly double Lambda;
        public double[] Beta;
        public double[,] P;
        public double HuberK;
        public double SigmaEwma;
        public double SigmaAlpha;

        public Rls(int dim, double lambda = 0.995, double p0 = 1e6, double huberK = 1.5, double sigmaAlpha = 0.02)
        {
            Dim = dim;
            Lambda = lambda;
            Beta = new double[dim];
            P = new double[dim, dim];
            for (int i = 0; i < dim; i++) P[i, i] = p0;
            HuberK = huberK;
            SigmaAlpha = sigmaAlpha;
            SigmaEwma = 1.0;
        }

        public double Predict(double[] x)
        {
            double s = 0;
            for (int i = 0; i < Dim; i++) s += Beta[i] * x[i];
            return s;
        }

        public (double yhat, double residual, double w) Update(double[] x, double y)
        {
            double yhat = Predict(x);
            double e = y - yhat;

            double sigmaLike = 1.2533 * Math.Max(1e-9, Math.Abs(e));
            SigmaEwma = (1.0 - SigmaAlpha) * SigmaEwma + SigmaAlpha * sigmaLike;
            double sigma = Math.Max(1e-9, SigmaEwma);

            double u = e / sigma;
            double au = Math.Abs(u);
            double w = au <= HuberK ? 1.0 : (HuberK / au);

            double sw = Math.Sqrt(w);
            var xw = new double[Dim];
            for (int i = 0; i < Dim; i++) xw[i] = sw * x[i];
            double yw = sw * y;

            double[] Px = new double[Dim];
            for (int i = 0; i < Dim; i++)
            {
                double sPx = 0;
                for (int j = 0; j < Dim; j++) sPx += P[i, j] * xw[j];
                Px[i] = sPx;
            }

            double denom = Lambda;
            for (int i = 0; i < Dim; i++) denom += xw[i] * Px[i];
            denom = Math.Max(1e-18, denom);

            double[] K = new double[Dim];
            for (int i = 0; i < Dim; i++) K[i] = Px[i] / denom;

            double yhatw = 0;
            for (int i = 0; i < Dim; i++) yhatw += Beta[i] * xw[i];
            double ew = yw - yhatw;

            for (int i = 0; i < Dim; i++) Beta[i] += K[i] * ew;

            double[,] newP = new double[Dim, Dim];
            for (int i = 0; i < Dim; i++)
            {
                for (int j = 0; j < Dim; j++)
                {
                    double kij = 0;
                    for (int k = 0; k < Dim; k++) kij += K[i] * xw[k] * P[k, j];
                    newP[i, j] = (P[i, j] - kij) / Lambda;
                }
            }
            P = newP;

            return (yhat, e, w);
        }
    }

    public class CalibratorCorrector
    {
        const double P0 = 1e7;

        // Forgetting factors (keep close to your existing)
        const double LambdaArtifacts = 0.998;
        const double LambdaOffBottom = 0.996;

        static readonly object _lock = new();

        // α-model (downhole off-bottom): 5 params
        // T_BHA,off = α0 cosθ + α1 ρ cosθ + α2 ρ h_p + α3 (pi-pa) + α4 ρ Q^2
        static readonly Rls RlsAlpha = new(5, LambdaOffBottom, P0, huberK: 2.0);

        // β-model (surface off-bottom): 5 params (no intercept per README)
        // T_top-side,off = β0 h + β1 ρ h + β2 (pi-pa) + β3 ρ Q^2 + β4 ρ l Q^2
        static readonly Rls RlsBeta = new(5, LambdaOffBottom, P0, huberK: 2.0);

        // Artifacts with instrumented sub:
        // f_p(z,Q) on [1, z, z^2, Q, Q^2, zQ]
        static readonly Rls RlsFp_WithTd = new(6, LambdaArtifacts, P0, huberK: 1.5);
        // f_dl(z,sign) on [1, z, sign(dz)]
        static readonly Rls RlsFdl_WithTd = new(3, LambdaArtifacts, P0, huberK: 1.5);

        // Artifacts without instrumented sub, using downhole tension:
        // Model y = f_p(z,Q) + γ1Δh + γ2ρΔh + γ3ρ(l-l_p)Q^2
        // x = [1,z,z^2,Q,Q^2,zQ, Δh, ρΔh, ρ(l-l_p)Q^2]
        static readonly Rls RlsFp_NoTd = new(9, LambdaArtifacts, P0, huberK: 1.5);

        // Deadline: y = f_dl(z,sign) + γ1Δh + γ2ρΔh + γ3ρ(l-l_p)Q^2
        // x = [1,z,sign, Δh, ρΔh, ρ(l-l_p)Q^2]
        static readonly Rls RlsFdl_NoTd = new(6, LambdaArtifacts, P0, huberK: 1.5);

        static readonly List<SurfaceSample> Surface = new(capacity: 50000);

        // Legacy behaviour for recommendations (kept)
        static bool _wasOnBottom = false;
        static double? _storedSurfaceWob = null;
        static int _logCounter = 0;

        public static void Process(
            ILogger<IDWISWorker<ConfigurationForWOBCorrection>>? logger,
            DateTime now,
            TopSideMeasurementsData topSide,
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
                bool offBottom = (w.BitDepth <= w.BottomHoleDepth - DepthMargin);

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

                // Determine what sensors we effectively have:
                bool hasTd = !double.IsNaN(w.Td);
                bool hasDownholeTension = w.Dh.TBha.HasValue;

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

                if (allowCalibrationUpdate && hasDownholeTension)
                {
                    RlsAlpha.Update(xAlpha, w.Dh.TBha!.Value);
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

                    if (allowCalibrationUpdate)
                    {
                        RlsFp_WithTd.Update(xFp, w.Tp - w.Td);
                        RlsFdl_WithTd.Update(xFdl, w.Tdl - w.Td);
                    }

                    fp = RlsFp_WithTd.Predict(xFp);
                    fdl = RlsFdl_WithTd.Predict(xFdl);
                }
                else if (hasDownholeTension)
                {
                    // Without instrumented sub, use README gamma formulation with T_BHA (measured)
                    double deltaH = h - hp; // (h - h_p)
                    double visc = rho * (l - lp) * Q * Q; // ρ(l-l_p)Q^2 (as in README)

                    double[] xFp = { 1.0, z, z * z, Q, Q * Q, z * Q, deltaH, rho * deltaH, visc };
                    double[] xFdl = { 1.0, z, w.signVelocity, deltaH, rho * deltaH, visc };

                    if (allowCalibrationUpdate)
                    {
                        RlsFp_NoTd.Update(xFp, w.Tp - w.Dh.TBha!.Value);
                        RlsFdl_NoTd.Update(xFdl, w.Tdl - w.Dh.TBha!.Value);
                    }

                    // For correction we need only f_p and f_dl parts (first 6 / first 3 coeffs)
                    var bFp = RlsFp_NoTd.Beta;
                    fp = bFp[0] + bFp[1] * z + bFp[2] * z * z + bFp[3] * Q + bFp[4] * Q * Q + bFp[5] * z * Q;

                    var bFdl = RlsFdl_NoTd.Beta;
                    fdl = bFdl[0] + bFdl[1] * z + bFdl[2] * w.signVelocity;
                }
                else
                {
                    // Not enough info to calibrate artifacts; set to zero
                    fp = 0.0;
                    fdl = 0.0;
                }

                // ========= 3) Choose corrected surface tension T_corr =========
                // README: T_corr = T_measured - f_sensor(z,Q,zdot)
                // For Td: no modeled artifact (bias ignored)
                // For Tp/Tdl: subtract f_p/f_dl
                double Td_corr = w.Td;
                double Tp_corr = w.Tp - fp;
                double Tdl_corr = w.Tdl - fdl;

                // Choose a "best" corrected tension for SWOB calibration/correction:
                // Prefer Td if present, else average of Tp/Tdl corrected.
                double TcorrForBeta = hasTd ? Td_corr : 0.5 * (Tp_corr + Tdl_corr);

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

                if (allowCalibrationUpdate)
                {
                    RlsBeta.Update(xBeta, TcorrForBeta);
                    logger?.LogInformation("CalibratorCorrector: calibration updated (alpha/beta/artifacts as applicable).");
                }

                // ========= 5) Compute corrected WOB outputs =========

                // Downhole corrected WOB 
                // T_DWOB = T_BHA - α0 cosθ - α1 ρ cosθ - α2 ρ h_p - α3 (pi-pa) - α4 ρ Q^2
                double correctedDownholeWob = double.NaN;
                if (hasDownholeTension)
                {
                    var a = RlsAlpha.Beta;
                    correctedDownholeWob =
                        w.Dh.TBha!.Value
                        - a[0] * cosTheta
                        - a[1] * (rho * cosTheta)
                        - a[2] * (rho * hp)
                        - a[3] * dp
                        - a[4] * (rho * Q * Q);
                }

                // Surface corrected WOB:
                // F_SWOB = T_corr - beta0 h - beta1 ρ h - beta2 (pi-pa) - beta3 ρ Q^2 - beta4 ρ l Q^2
                double betaPred = RlsBeta.Predict(xBeta);

                // Choose which surface measurement you want to output as "CorrectedSurfaceWeightOnBit":
                // If on-bottom, you may prefer Tp_corr or Td_corr depending on sensor installed; here: prefer Td if present.
                double TcorrForOutput = hasTd ? Td_corr : Tp_corr;

                double correctedSurfaceWob = TcorrForOutput - betaPred;

                if (correctedMeasurements.CorrectedSurfaceWeightOnBit is not null && !double.IsNaN(correctedSurfaceWob))
                {
                    correctedMeasurements.CorrectedSurfaceWeightOnBit.Value = correctedSurfaceWob;
                }

                if (correctedMeasurements.CorrectedDownholeWeightOnBit is not null && !double.IsNaN(correctedDownholeWob))
                {
                    correctedMeasurements.CorrectedDownholeWeightOnBit.Value = correctedDownholeWob;
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
                        $"CalibratorCorrector: onBottom={onBottom} offBottom={offBottom} omegaOk={omegaOk} " +
                        $"SWOB={correctedSurfaceWob:G6} DWOB={(double.IsNaN(correctedDownholeWob) ? double.NaN : correctedDownholeWob):G6}");
                }
            }
        }

        // ====== Build surface sample ======
        static bool TryBuildSurfaceSample(DateTime now, TopSideMeasurementsData topSide, out SurfaceSample sample)
        {
            sample = default!;

            // Required:
            if (!TryGetValue(topSide.BlockPosition, out var z)) return false;
            if (!TryGetValue(topSide.BottomOfStringDepth, out var bitDepth)) return false;
            if (!TryGetValue(topSide.BottomHoleDepth, out var bhDepth)) return false;
            if (!TryGetValue(topSide.BottomOfStringVerticalDepth, out var tvdAtBit)) return false;
            if (!TryGetValue(topSide.DrillingFluidDensityIn, out var rho)) return false;
            if (!TryGetValue(topSide.FlowrateIn, out var q)) return false;
            if (!TryGetValue(topSide.MeasuredTensionInstrumentedSub, out var td)) return false;

            // Tp/Tdl fall back to Td if missing
            double tp = TryGetValue(topSide.HookLoadAtTopDrive, out var tpVal) ? tpVal : td;
            double tdl = TryGetValue(topSide.HookLoadAtAnchor, out var tdlVal) ? tdlVal : td;

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
            TopSideMeasurementsData topSide,
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

            if (BadRel(w.TdMad, w.Td)) return false;
            if (BadRel(w.TpMad, w.Tp)) return false;
            if (BadRel(w.TdlMad, w.Tdl)) return false;

            if (w.BitDepthMad > maxDepthMad) return false;
            if (w.BottomHoleDepthMad > maxDepthMad) return false;

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

        // ====== Robust stats ======
        static double Median(IEnumerable<double> values)
        {
            var a = values.OrderBy(v => v).ToArray();
            int n = a.Length;
            if (n == 0) return double.NaN;
            return (n % 2 == 1) ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
        }

        static double Mad(IEnumerable<double> values, double median)
            => Median(values.Select(v => Math.Abs(v - median)));
    }
}
