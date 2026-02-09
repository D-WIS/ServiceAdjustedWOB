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
        readonly double _p0;
        readonly int _adaptEvery;
        readonly int _historyLength;
        readonly int _minSamplesForAdapt;
        readonly double _minFeatureStd;
        readonly double _maxCorrCondition;
        readonly List<int[]> _candidatePartitions;
        readonly Queue<double[]> _xHistory = new();
        readonly Queue<double> _yHistory = new();
        int[] _groupOf;
        int[] _groupSizes;
        int _activeDim;
        int _updateCount;
        double[] _betaActive;
        double[,] _pActive;
        public double ResidualMean { get; private set; } = 0.0;
        public double ResidualStdDev { get; private set; } = double.NaN;
        public int ResidualCount { get; private set; } = 0;

        public Rls(int dim, double lambda = 0.995, double p0 = 1e6, double huberK = 1.5, double sigmaAlpha = 0.02)
        {
            Dim = dim;
            Lambda = lambda;
            HuberK = huberK;
            SigmaAlpha = sigmaAlpha;
            SigmaEwma = 1.0;
            _p0 = p0;

            _adaptEvery = 10;
            _historyLength = Math.Max(80, 15 * dim);
            _minSamplesForAdapt = Math.Max(2 * dim, 12);
            _minFeatureStd = 1e-9;
            _maxCorrCondition = 1e5;

            _groupOf = new int[dim];
            _groupSizes = Enumerable.Repeat(1, dim).ToArray();
            _activeDim = dim;
            _betaActive = new double[_activeDim];
            _pActive = new double[_activeDim, _activeDim];
            for (int i = 0; i < _activeDim; i++) _pActive[i, i] = p0;

            Beta = new double[dim];
            P = new double[dim, dim];
            SyncPublicState();

            _candidatePartitions = GeneratePartitions(dim)
                .OrderBy(p => PartitionMaxGroupSize(p, dim))
                .ThenBy(p => dim - PartitionGroupCount(p))
                .ThenBy(p => PartitionNonSingletonCount(p, dim))
                .ThenBy(PartitionSignature)
                .ToList();
        }

        public double Predict(double[] x)
        {
            var xa = Compress(x);
            double s = 0;
            for (int i = 0; i < _activeDim; i++) s += _betaActive[i] * xa[i];
            return s;
        }

        public (double yhat, double residual, double w) Update(double[] x, double y)
        {
            AddHistory(x, y);
            _updateCount++;
            if (_updateCount % _adaptEvery == 0)
            {
                AdaptGroupingIfNeeded();
            }

            var xa = Compress(x);
            double yhat = Predict(x);
            double e = y - yhat;

            double sigmaLike = 1.2533 * Math.Max(1e-9, Math.Abs(e));
            SigmaEwma = (1.0 - SigmaAlpha) * SigmaEwma + SigmaAlpha * sigmaLike;
            double sigma = Math.Max(1e-9, SigmaEwma);

            double u = e / sigma;
            double au = Math.Abs(u);
            double w = au <= HuberK ? 1.0 : (HuberK / au);

            double sw = Math.Sqrt(w);
            var xw = new double[_activeDim];
            for (int i = 0; i < _activeDim; i++) xw[i] = sw * xa[i];
            double yw = sw * y;

            double[] Px = new double[_activeDim];
            for (int i = 0; i < _activeDim; i++)
            {
                double sPx = 0;
                for (int j = 0; j < _activeDim; j++) sPx += _pActive[i, j] * xw[j];
                Px[i] = sPx;
            }

            double denom = Lambda;
            for (int i = 0; i < _activeDim; i++) denom += xw[i] * Px[i];
            denom = Math.Max(1e-18, denom);

            double[] K = new double[_activeDim];
            for (int i = 0; i < _activeDim; i++) K[i] = Px[i] / denom;

            double yhatw = 0;
            for (int i = 0; i < _activeDim; i++) yhatw += _betaActive[i] * xw[i];
            double ew = yw - yhatw;

            for (int i = 0; i < _activeDim; i++) _betaActive[i] += K[i] * ew;

            double[,] newP = new double[_activeDim, _activeDim];
            for (int i = 0; i < _activeDim; i++)
            {
                for (int j = 0; j < _activeDim; j++)
                {
                    double kij = 0;
                    for (int k = 0; k < _activeDim; k++) kij += K[i] * xw[k] * _pActive[k, j];
                    newP[i, j] = (_pActive[i, j] - kij) / Lambda;
                }
            }
            _pActive = newP;
            SyncPublicState();
            ComputeModelUncertainty();

            return (yhat, e, w);
        }

        void AddHistory(double[] x, double y)
        {
            _xHistory.Enqueue((double[])x.Clone());
            _yHistory.Enqueue(y);
            while (_xHistory.Count > _historyLength)
            {
                _xHistory.Dequeue();
                _yHistory.Dequeue();
            }
        }

        double[] Compress(double[] x)
        {
            var xa = new double[_activeDim];
            for (int i = 0; i < Dim; i++)
            {
                xa[_groupOf[i]] += x[i];
            }
            return xa;
        }

        void AdaptGroupingIfNeeded()
        {
            if (_xHistory.Count < _minSamplesForAdapt) return;

            int[]? best = null;
            foreach (var partition in _candidatePartitions)
            {
                if (IsSeparable(partition))
                {
                    best = partition;
                    break;
                }
            }

            if (best is null) return;
            if (SamePartition(_groupOf, best)) return;
            Reconfigure(best);
        }

        bool IsSeparable(int[] partition)
        {
            var rows = _xHistory.ToArray();
            int m = rows.Length;
            int p = PartitionGroupCount(partition);
            if (m < p + 2) return false;

            var means = new double[p];
            var variances = new double[p];

            foreach (var r in rows)
            {
                for (int j = 0; j < Dim; j++) means[partition[j]] += r[j];
            }
            for (int g = 0; g < p; g++) means[g] /= m;

            foreach (var r in rows)
            {
                var agg = new double[p];
                for (int j = 0; j < Dim; j++) agg[partition[j]] += r[j];
                for (int g = 0; g < p; g++)
                {
                    double d = agg[g] - means[g];
                    variances[g] += d * d;
                }
            }

            var std = new double[p];
            for (int g = 0; g < p; g++)
            {
                variances[g] = variances[g] / Math.Max(1, m - 1);
                std[g] = Math.Sqrt(Math.Max(0.0, variances[g]));
                if (!double.IsFinite(std[g]) || std[g] < _minFeatureStd) return false;
            }

            var corr = new double[p, p];
            foreach (var r in rows)
            {
                var agg = new double[p];
                for (int j = 0; j < Dim; j++) agg[partition[j]] += r[j];
                for (int g1 = 0; g1 < p; g1++)
                {
                    double n1 = (agg[g1] - means[g1]) / std[g1];
                    for (int g2 = g1; g2 < p; g2++)
                    {
                        double n2 = (agg[g2] - means[g2]) / std[g2];
                        corr[g1, g2] += n1 * n2;
                    }
                }
            }

            for (int g1 = 0; g1 < p; g1++)
            {
                for (int g2 = g1; g2 < p; g2++)
                {
                    corr[g1, g2] /= Math.Max(1, m - 1);
                    corr[g2, g1] = corr[g1, g2];
                }
            }

            var eig = EigenvaluesSymmetric(corr);
            if (eig.Length == 0) return false;

            double maxEig = eig.Max();
            double minEig = eig.Min();
            if (!double.IsFinite(maxEig) || !double.IsFinite(minEig)) return false;
            if (minEig <= 1e-9) return false;

            double cond = maxEig / minEig;
            return cond <= _maxCorrCondition;
        }

        void Reconfigure(int[] newGroupOf)
        {
            var fullBeta = (double[])Beta.Clone();
            _groupOf = (int[])newGroupOf.Clone();
            _activeDim = PartitionGroupCount(_groupOf);
            _groupSizes = PartitionGroupSizes(_groupOf, Dim);

            _betaActive = new double[_activeDim];
            for (int j = 0; j < Dim; j++)
            {
                _betaActive[_groupOf[j]] += fullBeta[j];
            }
            for (int g = 0; g < _activeDim; g++)
            {
                _betaActive[g] /= Math.Max(1, _groupSizes[g]);
            }

            _pActive = new double[_activeDim, _activeDim];
            for (int i = 0; i < _activeDim; i++) _pActive[i, i] = _p0;

            SyncPublicState();
        }

        void SyncPublicState()
        {
            Array.Clear(Beta, 0, Beta.Length);
            for (int j = 0; j < Dim; j++)
            {
                Beta[j] = _betaActive[_groupOf[j]];
            }

            P = new double[Dim, Dim];
            for (int i = 0; i < Dim; i++)
            {
                int gi = _groupOf[i];
                for (int j = 0; j < Dim; j++)
                {
                    int gj = _groupOf[j];
                    P[i, j] = _pActive[gi, gj];
                }
            }
        }

        static bool SamePartition(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static int PartitionGroupCount(int[] partition) => partition.Max() + 1;

        static int[] PartitionGroupSizes(int[] partition, int dim)
        {
            int groups = PartitionGroupCount(partition);
            var sizes = new int[groups];
            for (int i = 0; i < dim; i++) sizes[partition[i]]++;
            return sizes;
        }

        static int PartitionMaxGroupSize(int[] partition, int dim)
        {
            var sizes = PartitionGroupSizes(partition, dim);
            return sizes.Max();
        }

        static int PartitionNonSingletonCount(int[] partition, int dim)
        {
            var sizes = PartitionGroupSizes(partition, dim);
            int count = 0;
            for (int i = 0; i < sizes.Length; i++) if (sizes[i] > 1) count++;
            return count;
        }

        static string PartitionSignature(int[] partition) => string.Join(",", partition);

        void ComputeModelUncertainty()
        {
            var xs = _xHistory.ToArray();
            var ys = _yHistory.ToArray();
            int n = Math.Min(xs.Length, ys.Length);

            if (n <= 0)
            {
                ResidualCount = 0;
                ResidualMean = 0.0;
                ResidualStdDev = double.NaN;
                return;
            }

            double sum = 0.0;
            var residuals = new double[n];
            for (int i = 0; i < n; i++)
            {
                double r = ys[i] - Predict(xs[i]);
                residuals[i] = r;
                sum += r;
            }

            double mean = sum / n;
            double var = 0.0;
            for (int i = 0; i < n; i++)
            {
                double d = residuals[i] - mean;
                var += d * d;
            }

            var /= Math.Max(1, n - 1);
            ResidualCount = n;
            ResidualMean = mean;
            ResidualStdDev = Math.Sqrt(Math.Max(0.0, var));
        }

        static List<int[]> GeneratePartitions(int dim)
        {
            var result = new List<int[]>();
            if (dim <= 0) return result;

            var part = new int[dim];
            part[0] = 0;
            var sizes = new List<int> { 1 };

            void Recurse(int idx, int groups)
            {
                if (idx == dim)
                {
                    result.Add((int[])part.Clone());
                    return;
                }

                for (int g = 0; g < groups; g++)
                {
                    part[idx] = g;
                    sizes[g]++;
                    Recurse(idx + 1, groups);
                    sizes[g]--;
                }

                part[idx] = groups;
                sizes.Add(1);
                Recurse(idx + 1, groups + 1);
                sizes.RemoveAt(sizes.Count - 1);
            }

            Recurse(1, 1);
            return result;
        }

        static double[] EigenvaluesSymmetric(double[,] matrix)
        {
            int n = matrix.GetLength(0);
            if (n == 0) return Array.Empty<double>();

            var a = (double[,])matrix.Clone();
            int maxIter = 20 * n * n;
            for (int iter = 0; iter < maxIter; iter++)
            {
                int p = 0, q = 1;
                double maxOff = 0.0;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        double v = Math.Abs(a[i, j]);
                        if (v > maxOff)
                        {
                            maxOff = v;
                            p = i;
                            q = j;
                        }
                    }
                }

                if (maxOff < 1e-10) break;

                double app = a[p, p];
                double aqq = a[q, q];
                double apq = a[p, q];
                double phi = 0.5 * Math.Atan2(2.0 * apq, aqq - app);
                double c = Math.Cos(phi);
                double s = Math.Sin(phi);

                for (int k = 0; k < n; k++)
                {
                    if (k == p || k == q) continue;
                    double aik = a[p, k];
                    double aqk = a[q, k];
                    a[p, k] = c * aik - s * aqk;
                    a[k, p] = a[p, k];
                    a[q, k] = s * aik + c * aqk;
                    a[k, q] = a[q, k];
                }

                double appNew = c * c * app - 2.0 * s * c * apq + s * s * aqq;
                double aqqNew = s * s * app + 2.0 * s * c * apq + c * c * aqq;
                a[p, p] = appNew;
                a[q, q] = aqqNew;
                a[p, q] = 0.0;
                a[q, p] = 0.0;
            }

            var evals = new double[n];
            for (int i = 0; i < n; i++) evals[i] = a[i, i];
            return evals;
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

        // Unconnected (in-slips) calibrations:
        // b_d = Td
        static readonly Rls RlsBd = new(1, LambdaArtifacts, P0, huberK: 1.5);
        // d0 + d1 z + d2 sign(dz) = Tdl
        static readonly Rls RlsFdl_Unconnected = new(3, LambdaArtifacts, P0, huberK: 1.5);
        // c0 + c1 z + c2 z^2 = Tp (Q=0 in unconnected)
        static readonly Rls RlsFp_Unconnected = new(3, LambdaArtifacts, P0, huberK: 1.5);

        // Shared gamma model in connected conditions:
        // Td - b_d - TBHA = γ1Δh + γ2ρΔh + γ3ρ(l-l_p)Q^2
        // x = [Δh, ρΔh, ρ(l-l_p)Q^2]
        static readonly Rls RlsGamma = new(3, LambdaArtifacts, P0, huberK: 1.5);

        // Artifacts without instrumented sub, after removing shared gamma contribution:
        // Tp - TBHA - γ1Δh - γ2ρΔh - γ3ρ(l-l_p)Q^2 = f_p(z,Q)
        // x = [1,z,z^2,Q,Q^2,zQ]
        static readonly Rls RlsFp_NoTd = new(6, LambdaArtifacts, P0, huberK: 1.5);

        // Tdl - TBHA - γ1Δh - γ2ρΔh - γ3ρ(l-l_p)Q^2 = f_dl(z,sign)
        // x = [1,z,sign]
        static readonly Rls RlsFdl_NoTd = new(3, LambdaArtifacts, P0, huberK: 1.5);

        static readonly List<SurfaceSample> Surface = new(capacity: 50000);

        // Legacy behaviour for recommendations (kept)
        static bool _wasOnBottom = false;
        static double? _storedSurfaceWob = null;
        static int _logCounter = 0;
        static double? _initialBd = null;
        static double? _initialC0 = null;
        static double? _initialD0 = null;

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
                bool offBottom = (w.BitDepth <= w.BottomHoleDepth - DepthMargin);
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
                double deltaH = h - hp; // (h - h_p)
                double visc = rho * (l - lp) * Q * Q; // ρ(l-l_p)Q^2

                // Determine what sensors we effectively have:
                bool hasTd = !double.IsNaN(w.Td);
                bool hasTp = !double.IsNaN(w.Tp);
                bool hasTdl = !double.IsNaN(w.Tdl);
                bool hasDownholeTension = w.Dh.TBha.HasValue;
                if (_initialBd is null && hasTd) _initialBd = EstimateSlipsLevel(Surface, s => s.Td, MinDistanceInSlips, DeltaTensionInSlips);
                if (_initialC0 is null && hasTp) _initialC0 = EstimateSlipsLevel(Surface, s => s.Tp, MinDistanceInSlips, DeltaTensionInSlips);
                if (_initialD0 is null && hasTdl) _initialD0 = EstimateSlipsLevel(Surface, s => s.Tdl, MinDistanceInSlips, DeltaTensionInSlips);

                double bdRef = GetReferenceLevel(hasTd ? w.Td : double.NaN, RlsBd.ResidualCount > 0 ? RlsBd.Beta[0] : double.NaN, _initialBd);
                double c0Ref = GetReferenceLevel(hasTp ? w.Tp : double.NaN, RlsFp_Unconnected.ResidualCount > 0 ? RlsFp_Unconnected.Beta[0] : double.NaN, _initialC0);
                double d0Ref = GetReferenceLevel(hasTdl ? w.Tdl : double.NaN, RlsFdl_Unconnected.ResidualCount > 0 ? RlsFdl_Unconnected.Beta[0] : double.NaN, _initialD0);

                int slipsVotes = 0;
                int slipsConsidered = 0;
                VoteInSlips(hasTd, w.Td, bdRef, FactorThresholdInSlips, ref slipsVotes, ref slipsConsidered);
                VoteInSlips(hasTp, w.Tp, c0Ref, FactorThresholdInSlips, ref slipsVotes, ref slipsConsidered);
                VoteInSlips(hasTdl, w.Tdl, d0Ref, FactorThresholdInSlips, ref slipsVotes, ref slipsConsidered);

                bool isUnconnected = offBottom && isMoving && slipsConsidered > 0 && (2 * slipsVotes >= slipsConsidered);
                bool isConnectedForCalibration = allowCalibrationUpdate && !isUnconnected;
                bool isUnconnectedForCalibration = offBottom && isMoving && isUnconnected;
                double bd = RlsBd.ResidualCount > 0 ? RlsBd.Beta[0] : (_initialBd ?? 0.0);

                if (isUnconnectedForCalibration)
                {
                    if (hasTd) RlsBd.Update(new[] { 1.0 }, w.Td);
                    if (hasTp) RlsFp_Unconnected.Update(new[] { 1.0, z, z * z }, w.Tp);
                    if (hasTdl) RlsFdl_Unconnected.Update(new[] { 1.0, z, w.signVelocity }, w.Tdl);
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
                    RlsAlpha.Update(xAlpha, w.Dh.TBha!.Value);
                }

                // ========= 1.b) Calibrate shared gamma terms =========
                // If Td and downhole tension are both present:
                // Td - b_d - TBHA = γ1Δh + γ2ρΔh + γ3ρ(l-l_p)Q^2
                double[] xGamma = new[] { deltaH, rho * deltaH, visc };
                if (isConnectedForCalibration && hasTd && hasDownholeTension)
                {
                    RlsGamma.Update(xGamma, w.Td - bd - w.Dh.TBha!.Value);
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
                        if (hasTp) RlsFp_WithTd.Update(xFp, w.Tp - (w.Td - bd));
                        if (hasTdl) RlsFdl_WithTd.Update(xFdl, w.Tdl - (w.Td - bd));
                    }
                    fp = isUnconnected && RlsFp_Unconnected.ResidualCount > 0
                        ? RlsFp_Unconnected.Predict(new[] { 1.0, z, z * z })
                        : RlsFp_WithTd.Predict(xFp);
                    fdl = isUnconnected && RlsFdl_Unconnected.ResidualCount > 0
                        ? RlsFdl_Unconnected.Predict(new[] { 1.0, z, w.signVelocity })
                        : RlsFdl_WithTd.Predict(xFdl);
                }
                else if (hasDownholeTension)
                {
                    // Without instrumented sub, remove shared gamma contribution and calibrate f_p / f_dl.
                    double[] xFp = { 1.0, z, z * z, Q, Q * Q, z * Q };
                    double[] xFdl = { 1.0, z, w.signVelocity };

                    double fpPred = RlsFp_NoTd.Predict(xFp);
                    double fdlPred = RlsFdl_NoTd.Predict(xFdl);

                    // Jointly refine gamma using both sensors when available.
                    if (isConnectedForCalibration)
                    {
                        if (hasTp) RlsGamma.Update(xGamma, w.Tp - w.Dh.TBha!.Value - fpPred);
                        if (hasTdl) RlsGamma.Update(xGamma, w.Tdl - w.Dh.TBha!.Value - fdlPred);
                    }

                    var g = RlsGamma.Beta;
                    double gammaTerm = g[0] * deltaH + g[1] * rho * deltaH + g[2] * visc;

                    if (isConnectedForCalibration)
                    {
                        if (hasTp) RlsFp_NoTd.Update(xFp, w.Tp - w.Dh.TBha!.Value - gammaTerm);
                        if (hasTdl) RlsFdl_NoTd.Update(xFdl, w.Tdl - w.Dh.TBha!.Value - gammaTerm);
                    }

                    fp = isUnconnected && RlsFp_Unconnected.ResidualCount > 0
                        ? RlsFp_Unconnected.Predict(new[] { 1.0, z, z * z })
                        : RlsFp_NoTd.Predict(xFp);
                    fdl = isUnconnected && RlsFdl_Unconnected.ResidualCount > 0
                        ? RlsFdl_Unconnected.Predict(new[] { 1.0, z, w.signVelocity })
                        : RlsFdl_NoTd.Predict(xFdl);
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

                if (isConnectedForCalibration && !double.IsNaN(TcorrForBeta))
                {
                    RlsBeta.Update(xBeta, TcorrForBeta);
                    logger?.LogInformation(
                        $"CalibratorCorrector: calibration updated. " +
                        $"state={(isUnconnected ? "unconnected" : "connected")} " +
                        $"sigma(alpha)={RlsAlpha.ResidualStdDev:G6} n={RlsAlpha.ResidualCount} " +
                        $"sigma(beta)={RlsBeta.ResidualStdDev:G6} n={RlsBeta.ResidualCount} " +
                        $"sigma(bd)={RlsBd.ResidualStdDev:G6} n={RlsBd.ResidualCount} " +
                        $"sigma(gamma)={RlsGamma.ResidualStdDev:G6} n={RlsGamma.ResidualCount} " +
                        $"sigma(fp|slips)={RlsFp_Unconnected.ResidualStdDev:G6} n={RlsFp_Unconnected.ResidualCount} " +
                        $"sigma(fdl|slips)={RlsFdl_Unconnected.ResidualStdDev:G6} n={RlsFdl_Unconnected.ResidualCount} " +
                        $"sigma(fp|Td)={RlsFp_WithTd.ResidualStdDev:G6} n={RlsFp_WithTd.ResidualCount} " +
                        $"sigma(fdl|Td)={RlsFdl_WithTd.ResidualStdDev:G6} n={RlsFdl_WithTd.ResidualCount} " +
                        $"sigma(fp|noTd)={RlsFp_NoTd.ResidualStdDev:G6} n={RlsFp_NoTd.ResidualCount} " +
                        $"sigma(fdl|noTd)={RlsFdl_NoTd.ResidualStdDev:G6} n={RlsFdl_NoTd.ResidualCount}");
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
                double TcorrForOutput =
                    hasTd ? Td_corr :
                    hasTp ? Tp_corr :
                    hasTdl ? Tdl_corr :
                    double.NaN;

                // F_SWOB1 from beta-model
                double fSwob1 = TcorrForOutput - betaPred;
                double sigmaSensor =
                    hasTd ? RlsBd.ResidualStdDev :
                    hasTp ? (isUnconnected ? RlsFp_Unconnected.ResidualStdDev : RlsFp_NoTd.ResidualStdDev) :
                    hasTdl ? (isUnconnected ? RlsFdl_Unconnected.ResidualStdDev : RlsFdl_NoTd.ResidualStdDev) :
                    double.NaN;
                double sigma1 = CombineSigmas(sigmaSensor, RlsBeta.ResidualStdDev);

                // F_SWOB2 from gamma-model + downhole tension (if available/calibrated)
                double fSwob2 = double.NaN;
                double sigma2 = double.NaN;
                if (hasDownholeTension && !double.IsNaN(TcorrForOutput))
                {
                    double g1, g2, g3, sigmaGamma;
                    bool hasGamma = TryGetGammaModel(out g1, out g2, out g3, out sigmaGamma);
                    if (hasGamma)
                    {
                        fSwob2 =
                            TcorrForOutput
                            - g1 * deltaH
                            + g2 * rho * deltaH
                            + g3 * visc
                            + w.Dh.TBha!.Value;
                        sigma2 = CombineSigmas(sigmaSensor, sigmaGamma);
                    }
                }

                // Gaussian sensor fusion of SWOB estimates using inverse-variance weights
                double correctedSurfaceWob = FuseGaussian(fSwob1, sigma1, fSwob2, sigma2, out var fusedSigma);

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
                        $"CalibratorCorrector: onBottom={onBottom} offBottom={offBottom} state={(isUnconnected ? "unconnected" : "connected")} omegaOk={omegaOk} " +
                        $"SWOB1={fSwob1:G6} sigma1={sigma1:G6} " +
                        $"SWOB2={fSwob2:G6} sigma2={sigma2:G6} " +
                        $"SWOB={correctedSurfaceWob:G6} sigmaSWOB={fusedSigma:G6} " +
                        $"DWOB={(double.IsNaN(correctedDownholeWob) ? double.NaN : correctedDownholeWob):G6}");
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

            if (!double.IsNaN(w.Td) && !double.IsNaN(w.TdMad) && BadRel(w.TdMad, w.Td)) return false;
            if (!double.IsNaN(w.Tp) && !double.IsNaN(w.TpMad) && BadRel(w.TpMad, w.Tp)) return false;
            if (!double.IsNaN(w.Tdl) && !double.IsNaN(w.TdlMad) && BadRel(w.TdlMad, w.Tdl)) return false;

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

        static bool TryGetGammaModel(out double g1, out double g2, out double g3, out double sigmaGamma)
        {
            g1 = g2 = g3 = sigmaGamma = double.NaN;
            if (RlsGamma.ResidualCount <= 0 || !IsFinitePositive(RlsGamma.ResidualStdDev)) return false;

            var b = RlsGamma.Beta;
            g1 = b[0];
            g2 = b[1];
            g3 = b[2];
            sigmaGamma = RlsGamma.ResidualStdDev;
            return true;
        }

        static double GetReferenceLevel(double measured, double modelConstant, double? initialEstimate)
        {
            if (!double.IsNaN(modelConstant) && modelConstant > 0.0) return modelConstant;
            if (initialEstimate.HasValue && initialEstimate.Value > 0.0) return initialEstimate.Value;
            if (!double.IsNaN(measured) && measured > 0.0) return measured;
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
