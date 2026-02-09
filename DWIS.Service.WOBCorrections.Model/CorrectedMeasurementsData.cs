using DWIS.API.DTO;
using DWIS.RigOS.Common.Model;
using DWIS.RigOS.Common.Worker;
using DWIS.Vocabulary.Schemas;
using OSDC.DotnetLibraries.Drilling.DrillingProperties;
using OSDC.UnitConversion.Conversion.DrillingEngineering;
using System.Reflection;

namespace DWIS.Service.CorrectedSWOBCorrections.Model
{
    public class CorrectedMeasurementsData : DWISData
    {
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> LocalSparQLQueries = new(BuildSparQLQueries(typeof(CorrectedMeasurementsData)));
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> LocalManifests = new(BuildManifests(typeof(CorrectedMeasurementsData), "ServiceManifest", "DWIS", "DWISService"));
        public override Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> SparQLQueries { get => LocalSparQLQueries; }
        public override Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> Manifests { get => LocalManifests; }

        [AccessToVariable(CommonProperty.VariableAccessType.Assignable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("CorrectedSWOB")]
        [SemanticFact("CorrectedSWOB", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("CorrectedSWOB#01", Nouns.Enum.CorrectedMeasurement)]
        [SemanticFact("CorrectedSWOB#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("CorrectedSWOB#01", Verbs.Enum.HasDynamicValue, "CorrectedSWOB")]
        [SemanticFact("CorrectedSWOB#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.WeightOnBitDrilling)]
        [SemanticFact("movingAverageCorrectedSWOB", Nouns.Enum.MovingAverage)]
        [SemanticFact("CorrectedSWOB#01", Verbs.Enum.IsTransformationOutput, "movingAverageCorrectedSWOB")]
        [SemanticFact("tos#01", Nouns.Enum.TopOfStringReferenceLocation)]
        [SemanticFact("CorrectedSWOB#01", Verbs.Enum.IsPhysicallyLocatedAt, "tos#01")]
        [SemanticFact("bos#01", Nouns.Enum.BottomOfStringReferenceLocation)]
        [SemanticFact("bh#01", Nouns.Enum.HoleBottomLocation)]
        [SemanticFact("CorrectedSWOB#01", Verbs.Enum.IsDependentOn, "bos#01")]
        [SemanticFact("CorrectedSWOB#01", Verbs.Enum.IsDependentOn, "bh#01")]
        [SemanticFact("correctionService#01", Nouns.Enum.DataAnalysisServiceCompany)]
        [SemanticFact("CorrectedSWOB#01", Verbs.Enum.IsProvidedBy, "correctionService#01")]
        [SemanticFact("CorrectedSWOB#01", Nouns.Enum.WOB)]
        public ScalarProperty? CorrectedSurfaceWeightOnBit { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Assignable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("CorrectedDownholeMeasuredAverageBitWeight")]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight#01", Nouns.Enum.CorrectedMeasurement)]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight#01", Verbs.Enum.HasDynamicValue, "CorrectedDownholeMeasuredAverageBitWeight")]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.WeightOnBitDrilling)]
        [SemanticFact("MovingAverageCorrectedDownholeMeasuredAverageBitWeight", Nouns.Enum.MovingAverage)]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight#01", Verbs.Enum.IsTransformationOutput, "MovingAverageCorrectedDownholeMeasuredAverageBitWeight")]
        [SemanticFact("taringTransformationAverageWOB", Nouns.Enum.SummationTransformation)]
        [SemanticFact("CorrectedDownholeMeasuredAverageRawWeight", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("CorrectedDownholeMeasuredAverageRawWeight#01", Nouns.Enum.DerivedMeasurement)]
        [SemanticFact("CorrectedDownholeMeasuredAverageRawWeight#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("CorrectedDownholeMeasuredAverageRawWeight#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.WeightOnBitDrilling)]
        [SemanticFact("CorrectedDownholeMeasuredAverageRawWeight#01", Verbs.Enum.IsTransformationInput, "taringTransformationAverageWOB")]
        [SemanticFact("TareBitWeightForMudPulseTelemetry", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("TareBitWeightForMudPulseTelemetry#01", Nouns.Enum.ProcessData)]
        [SemanticFact("TareBitWeightForMudPulseTelemetry#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("TareBitWeightForMudPulseTelemetry#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.WeightOnBitDrilling)]
        [SemanticFact("TareBitWeightForMudPulseTelemetry#01", Verbs.Enum.IsTransformationInput, "taringTransformationAverageWOB")]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight#01", Verbs.Enum.IsTransformationOutput, "taringTransformationAverageWOB")]
        [SemanticFact("GaussianUncertaintyCorrectedDownholeMeasuredAverageBitWeight#01", Nouns.Enum.GaussianUncertainty)]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight#01", Verbs.Enum.HasUncertainty, "GaussianUncertaintyCorrectedDownholeMeasuredAverageBitWeight#01")]
        [SemanticFact("GaussianUncertaintyCorrectedDownholeMeasuredAverageBitWeight#01", Verbs.Enum.HasUncertaintyMean, "CorrectedDownholeMeasuredAverageBitWeight#01")]
        [SemanticFact("mudPulseTelemetry", Nouns.Enum.MudPulseTelemetry)]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight#01", Verbs.Enum.IsTransmittedBy, "mudPulseTelemetry")]
        [SemanticFact("Bit#01", Nouns.Enum.EndOfStringMechanicalLogicalElement)]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight#01", Verbs.Enum.IsMechanicallyLocatedAt, "Bit#01")]
        [SemanticFact("correctionService#01", Nouns.Enum.DataAnalysisServiceCompany)]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight#01", Verbs.Enum.IsProvidedBy, "correctionService#01")]
        [SemanticFact("CorrectedDownholeMeasuredAverageBitWeight#01", Nouns.Enum.WOB)]
        public ScalarProperty? CorrectedDownholeWeightOnBit { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Assignable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("CorrectedHookLoadAtDeadLine")]
        [SemanticFact("CorrectedHookLoadAtDeadLine", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("CorrectedHookLoadAtDeadLine#01", Nouns.Enum.CorrectedMeasurement)]
        [SemanticFact("CorrectedHookLoadAtDeadLine#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("CorrectedHookLoadAtDeadLine#01", Verbs.Enum.HasDynamicValue, "CorrectedHookLoadAtDeadLine")]
        [SemanticFact("CorrectedHookLoadAtDeadLine#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.HookLoadDrilling)]
        [SemanticFact("movingAverageCorrectedHookLoadAtDeadLine", Nouns.Enum.MovingAverage)]
        [SemanticFact("CorrectedHookLoadAtDeadLine#01", Verbs.Enum.IsTransformationOutput, "movingAverageCorrectedHookLoadAtDeadLine")]
        [SemanticFact("deadLineDeadLine#01", Nouns.Enum.DeadLineAnchor)]
        [SemanticFact("CorrectedHookLoadAtDeadLineAtTopDrive#01", Verbs.Enum.IsLocatedAtEquipment, "deadLineDeadLine#01")]
        [SemanticFact("hook#01", Nouns.Enum.Hook)]
        [SemanticFact("CorrectedHookLoadAtDeadLine#01", Verbs.Enum.IsPhysicallyLocatedAt, "hook#01")]
        [SemanticFact("correctionService#01", Nouns.Enum.DataAnalysisServiceCompany)]
        [SemanticFact("CorrectedHookLoadAtDeadLine#01", Verbs.Enum.IsProvidedBy, "correctionService#01")]
        [SemanticFact("CorrectedHookLoadAtDeadLine#01", Nouns.Enum.HookLoad)]
        public ScalarProperty? CorrectedHookLoadAtDeadLine { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Assignable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("CorrectedHookLoadAtTopDrive")]
        [SemanticFact("CorrectedHookLoadAtTopDrive", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("CorrectedHookLoadAtTopDrive#01", Nouns.Enum.CorrectedMeasurement)]
        [SemanticFact("CorrectedHookLoadAtTopDrive#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("CorrectedHookLoadAtTopDrive#01", Verbs.Enum.HasDynamicValue, "CorrectedHookLoadAtTopDrive")]
        [SemanticFact("CorrectedHookLoadAtTopDrive#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.HookLoadDrilling)]
        [SemanticFact("movingAverageCorrectedHookLoadAtTopDrive", Nouns.Enum.MovingAverage)]
        [SemanticFact("CorrectedHookLoadAtTopDrive#01", Verbs.Enum.IsTransformationOutput, "movingAverageCorrectedHookLoadAtTopDrive")]
        [SemanticFact("loadNut#01", Nouns.Enum.LoadNut)]
        [SemanticFact("CorrectedHookLoadAtTopDrive#01", Verbs.Enum.IsLocatedAtEquipment, "loadNut#01")]
        [SemanticFact("hook#01", Nouns.Enum.Hook)]
        [SemanticFact("CorrectedHookLoadAtTopDrive#01", Verbs.Enum.IsPhysicallyLocatedAt, "hook#01")]
        [SemanticFact("correctionService#01", Nouns.Enum.DataAnalysisServiceCompany)]
        [SemanticFact("CorrectedHookLoadAtTopDrive#01", Verbs.Enum.IsProvidedBy, "correctionService#01")]
        [SemanticFact("CorrectedHookLoadAtTopDrive#01", Nouns.Enum.HookLoad)]
        public ScalarProperty? CorrectedHookLoadAtTopDrive { get; set; } = null;

    }
}
