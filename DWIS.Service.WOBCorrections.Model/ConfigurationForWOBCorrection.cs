using DWIS.RigOS.Common.Worker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.Service.WOBCorrections.Model
{
    public class ConfigurationForWOBCorrection : Configuration
    {
        public static readonly TimeSpan WindowDurationDefault = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan MaxSurfaceAgeDefault = TimeSpan.FromMinutes(10);
        public static readonly int MinSurfaceSamplesPerWindowDefault = 10;

        public static readonly double DepthMarginDefault = 0.5;
        public static readonly double MinDownholeRotationalSpeedDefault = 50.0 * 2.0 * Math.PI / 60.0;

        public static readonly double MaxRelQMadDefault = 0.15;
        public static readonly double MaxRelTMadDefault = 0.25;
        public static readonly double MaxDepthMadDefault = 0.2;

        public static readonly double MinVelocityForMotionDefault = 1e-5;

        public static readonly double FactorThresholdInSlipsDefault = 1.5;
        public static readonly double DeltaTensionInSlipsDefault = 50000.0;
        public static readonly double MinDistanceInSlipsDefault = 0.1;

        public string? BHADrillStringHostURL { get; set; }
        public TimeSpan? WindowDuration { get; set; } = WindowDurationDefault;
        public TimeSpan? MaxSurfaceAge { get; set; } = MaxSurfaceAgeDefault;
        public int? MinSurfaceSamplesPerWindow { get; set; } = MinSurfaceSamplesPerWindowDefault;
        public double? DepthMargin { get; set; } = DepthMarginDefault;
        public double? MinDownholeRotationalSpeed { get; set; } = MinDownholeRotationalSpeedDefault;
        public double? MaxRelQMad { get; set; } = MaxRelQMadDefault;
        public double? MaxRelTMad { get; set; } = MaxRelTMadDefault;
        public double? MaxDepthMad { get; set; } = MaxDepthMadDefault;
        public double? MinVelocityForMotion { get; set; } = MinVelocityForMotionDefault;

        public double? FactorThresholdInSlips { get; set; } = FactorThresholdInSlipsDefault;
        public double? DeltaTensionInSlips { get; set; } = DeltaTensionInSlipsDefault;
        public double? MinDistanceInSlips { get; set; } = MinDistanceInSlipsDefault;

    }
}
