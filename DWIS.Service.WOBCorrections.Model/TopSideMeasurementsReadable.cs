using DWIS.API.DTO;
using DWIS.RigOS.Common.Worker;
using DWIS.Vocabulary.Schemas;
using OSDC.DotnetLibraries.Drilling.DrillingProperties;
using OSDC.UnitConversion.Conversion.DrillingEngineering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.Service.WOBCorrections.Model
{
    public class TopSideMeasurementsReadable : DWISData
    {
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> LocalSparQLQueries = new(BuildSparQLQueries(typeof(TopSideMeasurementsReadable)));
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> LocalManifests = new(BuildManifests(typeof(TopSideMeasurementsReadable), "TopSideManifest", "DWIS", "DWISService"));
        public override Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> SparQLQueries { get => LocalSparQLQueries; }
        public override Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> Manifests { get => LocalManifests; }

        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticExclusiveOr(1, 2)]
        [SemanticDiracVariable("blockPosition")]
        [SemanticFact("blockPosition", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("blockPosition#01", Nouns.Enum.Measurement)]
        [SemanticFact("blockPosition#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("blockPosition#01", Verbs.Enum.HasDynamicValue, "blockPosition")]
        [SemanticFact("blockPosition#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.HeightDrilling)]
        [OptionalFact(1, "movingAverageBlockPosition", Nouns.Enum.MovingAverage)]
        [OptionalFact(1, "blockPosition#01", Verbs.Enum.IsTransformationOutput, "movingAverageBlockPosition")]
        [OptionalFact(1, "elevator#01", Nouns.Enum.DrillPipeElevator)]
        [OptionalFact(1, "blockPosition#01", Verbs.Enum.IsPhysicallyLocatedAt, "elevator#01")]
        [OptionalFact(2, "blockPosition#01", Nouns.Enum.HookPosition)]
        public ScalarProperty? BlockPosition { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticExclusiveOr(1, 2)]
        [SemanticDiracVariable("BOS_depth")]
        [SemanticFact("BOS_depth", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("BOS_depth#01", Nouns.Enum.Measurement)]
        [SemanticFact("BOS_depth#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("BOS_depth#01", Verbs.Enum.HasDynamicValue, "BOS_depth")]
        [SemanticFact("BOS_depth#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.DepthDrilling)]
        [OptionalFact(1, "movingAverageBOS_depth", Nouns.Enum.MovingAverage)]
        [OptionalFact(1, "BOS_depth#01", Verbs.Enum.IsTransformationOutput, "movingAverageBOS_depth")]
        [OptionalFact(1, "curvilinearAbscissaFrame#01", Nouns.Enum.OneDimensionalCurviLinearReferenceFrame)]
        [OptionalFact(1, "BOS_depth#01", Verbs.Enum.HasReferenceFrame, "curvilinearAbscissaFrame#01")]
        [OptionalFact(1, "bos#01", Nouns.Enum.BottomOfStringReferenceLocation)]
        [OptionalFact(1, "BOS_depth#01", Verbs.Enum.IsPhysicallyLocatedAt, "bos#01")]
        [OptionalFact(2, "BOS_depth#01", Nouns.Enum.BitDepth)]
        public ScalarProperty? BottomOfStringDepth { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticExclusiveOr(1, 2)]
        [SemanticDiracVariable("BH_depth")]
        [SemanticFact("BH_depth", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("BH_depth#01", Nouns.Enum.Measurement)]
        [SemanticFact("BH_depth#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("BH_depth#01", Verbs.Enum.HasDynamicValue, "BH_depth")]
        [SemanticFact("BH_depth#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.DepthDrilling)]
        [OptionalFact(1, "movingAverageBH_depth", Nouns.Enum.MovingAverage)]
        [OptionalFact(1, "BH_depth#01", Verbs.Enum.IsTransformationOutput, "movingAverageBH_depth")]
        [OptionalFact(1, "curvilinearAbscissaFrame#01", Nouns.Enum.OneDimensionalCurviLinearReferenceFrame)]
        [OptionalFact(1, "BH_depth#01", Verbs.Enum.HasReferenceFrame, "curvilinearAbscissaFrame#01")]
        [OptionalFact(1, "bh#01", Nouns.Enum.HoleBottomLocation)]
        [OptionalFact(1, "BH_depth#01", Verbs.Enum.IsPhysicallyLocatedAt, "bh#01")]
        [OptionalFact(2, "BH_depth#01", Nouns.Enum.HoleDepth)]
        public ScalarProperty? BottomHoleDepth { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("BOS_VerticalDepth")]
        [SemanticFact("BOS_VerticalDepth", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("BOS_VerticalDepth#01", Nouns.Enum.DerivedMeasurement)]
        [SemanticFact("BOS_VerticalDepth#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("BOS_VerticalDepth#01", Verbs.Enum.HasDynamicValue, "BOS_VerticalDepth")]
        [SemanticFact("BOS_VerticalDepth#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.DepthDrilling)]
        [SemanticFact("movingAverageBOS_VerticalDepth", Nouns.Enum.MovingAverage)]
        [SemanticFact("BOS_VerticalDepth#01", Verbs.Enum.IsTransformationOutput, "movingAverageBOS_VerticalDepth")]
        [SemanticFact("verticalDirection#01", Nouns.Enum.VerticalDepthFrame)]
        [SemanticFact("BOS_VerticalDepth#01", Verbs.Enum.HasReferenceFrame, "verticalDirection#01")]
        [SemanticFact("bos#01", Nouns.Enum.BottomOfStringReferenceLocation)]
        [SemanticFact("BOS_VerticalDepth#01", Verbs.Enum.IsPhysicallyLocatedAt, "bos#01")]
        public ScalarProperty? BottomOfStringVerticalDepth { get; set; } = null;

        // inclination
        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("BOS_Inclination")]
        [SemanticFact("BOS_Inclination", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("BOS_Inclination#01", Nouns.Enum.DerivedMeasurement)]
        [SemanticFact("BOS_Inclination#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("BOS_Inclination#01", Verbs.Enum.HasDynamicValue, "BOS_Inclination")]
        [SemanticFact("BOS_Inclination#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.PlaneAngleDrilling)]
        [SemanticFact("verticalDirection#01", Nouns.Enum.VerticalDepthFrame)]
        [SemanticFact("BOS_Inclination#01", Verbs.Enum.IsMeasuredFromReferenceFrame, "verticalDirection#01")]
        [SemanticFact("movingAverageBOS_Inclination", Nouns.Enum.MovingAverage)]
        [SemanticFact("BOS_Inclination#01", Verbs.Enum.IsTransformationOutput, "movingAveragBOS_Inclinatione")]
        [SemanticFact("bos#01", Nouns.Enum.BottomOfStringReferenceLocation)]
        [SemanticFact("BOS_Inclination#01", Verbs.Enum.IsPhysicallyLocatedAt, "bos#01")]
        public ScalarProperty? BottomOfStringInclination { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticExclusiveOr(1, 2)]
        [SemanticDiracVariable("Q_tos")]
        [SemanticFact("Q_tos", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("Q_tos#01", Nouns.Enum.Measurement)]
        [SemanticFact("Q_tos#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("Q_tos#01", Verbs.Enum.HasDynamicValue, "Q_tos")]
        [SemanticFact("Q_tos#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.VolumetricFlowrateDrilling)]
        [OptionalFact(1, "movingAverageQ_tos", Nouns.Enum.MovingAverage)]
        [OptionalFact(1, "Q_tos#01", Verbs.Enum.IsTransformationOutput, "movingAverageQ_tos")]
        [OptionalFact(1, "topOfStringJunction#01", Nouns.Enum.TopOfStringJunction)]
        [OptionalFact(1, "inletHydraulicBranch#01", Nouns.Enum.HydraulicBranch)]
        [OptionalFact(1, "topOfStringJunction#01", Verbs.Enum.HasUpstreamBranch, "inletHydraulicBranch#01")]
        [OptionalFact(1, "Q_tos#01", Verbs.Enum.IsAssociatedToHydraulicBranch, "inletHydraulicBranch#01")]
        [OptionalFact(2, "Q_tos#01", Nouns.Enum.FlowRateIn)]
        public ScalarProperty? FlowrateIn { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticExclusiveOr(1, 2)]
        [SemanticDiracVariable("densityIn")]
        [SemanticFact("densityIn", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("densityIn#01", Nouns.Enum.Measurement)]
        [SemanticFact("densityIn#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("densityIn#01", Verbs.Enum.HasDynamicValue, "densityIn")]
        [SemanticFact("densityIn#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.MassDensityDrilling)]
        [OptionalFact(1, "movingAverageDensityIn", Nouns.Enum.MovingAverage)]
        [OptionalFact(1, "densityIn#01", Verbs.Enum.IsTransformationOutput, "movingAverageDensityIn")]
        [OptionalFact(1, "topOfStringJunction#01", Nouns.Enum.TopOfStringJunction)]
        [OptionalFact(1, "inletHydraulicBranch#01", Nouns.Enum.HydraulicBranch)]
        [OptionalFact(1, "topOfStringJunction#01", Verbs.Enum.HasUpstreamBranch, "inletHydraulicBranch#01")]
        [OptionalFact(1, "densityIn#01", Verbs.Enum.IsAssociatedToHydraulicBranch, "inletHydraulicBranch#01")]
        [OptionalFact(2, "densityIn#01", Nouns.Enum.DensityIn)]
        public ScalarProperty? DrillingFluidDensityIn { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticExclusiveOr(1, 2)]
        [SemanticDiracVariable("hookLoad")]
        [SemanticFact("hookLoad", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("hookLoad#01", Nouns.Enum.Measurement)]
        [SemanticFact("hookLoad#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("hookLoad#01", Verbs.Enum.HasDynamicValue, "hookLoad")]
        [SemanticFact("hookLoad#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.HookLoadDrilling)]
        [OptionalFact(1, "movingAverageHookLoad", Nouns.Enum.MovingAverage)]
        [OptionalFact(1, "hookLoad#01", Verbs.Enum.IsTransformationOutput, "movingAverageHookLoad")]
        [OptionalFact(1, "hook#01", Nouns.Enum.Hook)]
        [OptionalFact(1, "hookLoad#01", Verbs.Enum.IsPhysicallyLocatedAt, "hook#01")]
        [OptionalFact(2, "hookLoad#01", Nouns.Enum.HookLoad)]
        public ScalarProperty? HookLoad { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("OmniViewMeasuredTension")]
        [SemanticFact("OmniViewMeasuredTension", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("OmniViewMeasuredTension#01", Nouns.Enum.DirectMeasurement)]
        [SemanticFact("OmniViewMeasuredTension#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("OmniViewMeasuredTension#01", Verbs.Enum.HasDynamicValue, "OmniViewMeasuredTension")]
        [SemanticFact("OmniViewMeasuredTension#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.TensionDrilling)]
        [SemanticFact("topSideTelemetry", Nouns.Enum.TopSideTelemetry)]
        [SemanticFact("OmniViewMeasuredTension#01", Verbs.Enum.IsTransmittedBy, "topSideTelemetry")]
        [SemanticFact("OmniViewMeasuredTensionGaussianUncertainty#01", Nouns.Enum.GaussianUncertainty)]
        [SemanticFact("OmniViewMeasuredTension#01", Verbs.Enum.HasUncertainty, "OmniViewMeasuredTensionGaussianUncertainty#01")]
        [SemanticFact("OmniViewMeasuredTensionGaussianUncertainty#01", Verbs.Enum.HasUncertaintyMean, "OmniViewMeasuredTension#01")]
        [SemanticFact("SwivelSub#01", Nouns.Enum.SwivelSub)]
        [SemanticFact("OmniViewMeasuredTension#01", Verbs.Enum.IsMechanicallyLocatedAt, "SwivelSub#01")]
        [SemanticFact("rotatingDriveSystem", Nouns.Enum.RotatingDriveSystemLocation)]
        [SemanticFact("OmniViewMeasuredTension#01", Verbs.Enum.IsPhysicallyLocatedAt, "rotatingDriveSystem")]
        [SemanticFact("Petromar#01", Nouns.Enum.InstrumentationCompany)]
        [SemanticFact("OmniViewMeasuredTension#01", Verbs.Enum.IsProvidedBy, "Petromar#01")]
        public ScalarProperty? MeasuredTensionInstrumentedSub { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("hookLoadAtAnchor")]
        [SemanticFact("hookLoadAtAnchor", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("hookLoadAtAnchor#01", Nouns.Enum.DerivedMeasurement)]
        [SemanticFact("hookLoadAtAnchor#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("hookLoadAtAnchor#01", Verbs.Enum.HasDynamicValue, "hookLoadAtAnchor")]
        [SemanticFact("hookLoadAtAnchor#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.HookLoadDrilling)]
        [SemanticFact("movingAverageHookLoadAtAnchor", Nouns.Enum.MovingAverage)]
        [SemanticFact("hookLoadAtAnchor#01", Verbs.Enum.IsTransformationOutput, "movingAverageHookLoadAtAnchor")]
        [SemanticFact("deadLineAnchor#01", Nouns.Enum.DeadLineAnchor)]
        [SemanticFact("hookLoadAtAnchorAtTopDrive#01", Verbs.Enum.IsLocatedAtEquipment, "deadLineAnchor#01")]
        [SemanticFact("hook#01", Nouns.Enum.Hook)]
        [SemanticFact("hookLoadAtAnchor#01", Verbs.Enum.IsPhysicallyLocatedAt, "hook#01")]
        public ScalarProperty? HookLoadAtAnchor { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("hookLoadAtTopDrive")]
        [SemanticFact("hookLoadAtTopDrive", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("hookLoadAtTopDrive#01", Nouns.Enum.DerivedMeasurement)]
        [SemanticFact("hookLoadAtTopDrive#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("hookLoadAtTopDrive#01", Verbs.Enum.HasDynamicValue, "hookLoadAtTopDrive")]
        [SemanticFact("hookLoadAtTopDrive#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.HookLoadDrilling)]
        [SemanticFact("movingAverageHookLoadAtTopDrive", Nouns.Enum.MovingAverage)]
        [SemanticFact("hookLoadAtTopDrive#01", Verbs.Enum.IsTransformationOutput, "movingAverageHookLoadAtTopDrive")]
        [SemanticFact("loadNut#01", Nouns.Enum.LoadNut)]
        [SemanticFact("hookLoadAtTopDrive#01", Verbs.Enum.IsLocatedAtEquipment, "loadNut#01")]
        [SemanticFact("hook#01", Nouns.Enum.Hook)]
        [SemanticFact("hookLoadAtTopDrive#01", Verbs.Enum.IsPhysicallyLocatedAt, "hook#01")]
        public ScalarProperty? HookLoadAtTopDrive { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Readable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticExclusiveOr(1, 2)]
        [SemanticDiracVariable("WOB")]
        [SemanticFact("WOB", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("WOB#01", Nouns.Enum.Measurement)]
        [SemanticFact("WOB#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("WOB#01", Verbs.Enum.HasDynamicValue, "WOB")]
        [SemanticFact("WOB#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.WeightOnBitDrilling)]
        [OptionalFact(1, "movingAverageWOB", Nouns.Enum.MovingAverage)]
        [OptionalFact(1, "WOB#01", Verbs.Enum.IsTransformationOutput, "movingAverageWOB")]
        [OptionalFact(1, "tos#01", Nouns.Enum.TopOfStringReferenceLocation)]
        [OptionalFact(1, "WOB#01", Verbs.Enum.IsPhysicallyLocatedAt, "tos#01")]
        [OptionalFact(1, "bos#01", Nouns.Enum.BottomOfStringReferenceLocation)]
        [OptionalFact(1, "bh#01", Nouns.Enum.HoleBottomLocation)]
        [OptionalFact(1, "WOB#01", Verbs.Enum.IsDependentOn, "bos#01")]
        [OptionalFact(1, "WOB#01", Verbs.Enum.IsDependentOn, "bh#01")]
        [OptionalFact(2, "WOB#01", Nouns.Enum.WOB)]
        public ScalarProperty? SurfaceWeightOnBit { get; set; } = null;

    }
}
