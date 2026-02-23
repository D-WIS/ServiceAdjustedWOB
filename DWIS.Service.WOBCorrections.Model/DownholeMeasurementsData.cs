using DWIS.API.DTO;
using DWIS.RigOS.Common.Worker;
using DWIS.Vocabulary.Schemas;
using OSDC.DotnetLibraries.Drilling.DrillingProperties;
using OSDC.UnitConversion.Conversion.DrillingEngineering;
using System.Reflection;

namespace DWIS.Service.WOBCorrections.Model
{
    public class DownholeMeasurementsData : DWISData
    {
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> LocalSparQLQueries = new(BuildSparQLQueries(typeof(DownholeMeasurementsData)));
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> LocalManifests = new(BuildManifests(typeof(DownholeMeasurementsData), "DownholeManifest", "DWIS", "DWISService"));
        public override Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> SparQLQueries { get => LocalSparQLQueries; }
        public override Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> Manifests { get => LocalManifests; }

        [AccessToVariable(CommonProperty.VariableAccessType.Assignable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("BaseStarMeasuredAverageRawWeight")]
        [SemanticFact("BaseStarMeasuredAverageRawWeight", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("BaseStarMeasuredAverageRawWeight#01", Nouns.Enum.DerivedMeasurement)]
        [SemanticFact("BaseStarMeasuredAverageRawWeight#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("BaseStarMeasuredAverageRawWeight#01", Verbs.Enum.HasDynamicValue, "BaseStarMeasuredAverageRawWeight")]
        [SemanticFact("BaseStarMeasuredAverageRawWeight#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.WeightOnBitDrilling)]
        [SemanticFact("MovingAverageBaseStarMeasuredAverageRawWeight", Nouns.Enum.MovingAverage)]
        [SemanticFact("BaseStarMeasuredAverageRawWeight#01", Verbs.Enum.IsTransformationOutput, "MovingAverageBaseStarMeasuredAverageRawWeight")]
        [SemanticFact("BaseStarMeasuredRawWeight", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("BaseStarMeasuredRawWeight#01", Nouns.Enum.DerivedMeasurement)]
        [SemanticFact("BaseStarMeasuredRawWeight#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("BaseStarMeasuredRawWeight#01", Verbs.Enum.HasDynamicValue, "BaseStarMeasuredRawWeight")]
        [SemanticFact("BaseStarMeasuredRawWeight#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.WeightOnBitDrilling)]
        [SemanticFact("BaseStarMeasuredRawWeight#01", Verbs.Enum.IsTransformationInput, "MovingAverageBaseStarMeasuredAverageRawWeight")]
        [SemanticFact("GaussianUncertaintyBaseStarMeasuredAverageRawWeight#01", Nouns.Enum.GaussianUncertainty)]
        [SemanticFact("BaseStarMeasuredAverageRawWeight#01", Verbs.Enum.HasUncertainty, "GaussianUncertaintyBaseStarMeasuredAverageRawWeight#01")]
        [SemanticFact("GaussianUncertaintyBaseStarMeasuredAverageRawWeight#01", Verbs.Enum.HasUncertaintyMean, "BaseStarMeasuredAverageRawWeight#01")]
        [SemanticFact("mudPulseTelemetry", Nouns.Enum.MudPulseTelemetry)]
        [SemanticFact("BaseStarMeasuredAverageRawWeight#01", Verbs.Enum.IsTransmittedBy, "mudPulseTelemetry")]
        [SemanticFact("BHA#01", Nouns.Enum.BHAMechanicalLogicalElement)]
        [SemanticFact("BaseStarMeasuredAverageRawWeight#01", Verbs.Enum.IsMechanicallyLocatedAt, "BHA#01")]
        [SemanticFact("BaseStarMeasuredRawWeight#01", Verbs.Enum.IsMechanicallyLocatedAt, "BHA#01")]
        [SemanticFact("Halliburton#01", Nouns.Enum.InstrumentationCompany)]
        [SemanticFact("BaseStarMeasuredAverageRawWeight#01", Verbs.Enum.IsProvidedBy, "Halliburton#01")]
        [SemanticFact("BaseStarMeasuredRawWeight#01", Verbs.Enum.IsProvidedBy, "Halliburton#01")]
        public ScalarProperty? AverageRawWeight { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Assignable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("BaseStarMeasuredStringPressure")]
        [SemanticFact("BaseStarMeasuredStringPressure", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("BaseStarMeasuredStringPressure#01", Nouns.Enum.DerivedMeasurement)]
        [SemanticFact("BaseStarMeasuredStringPressure#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("BaseStarMeasuredStringPressure#01", Verbs.Enum.HasDynamicValue, "BaseStarMeasuredStringPressure")]
        [SemanticFact("BaseStarMeasuredStringPressure#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.PressureDrilling)]
        [SemanticFact("MovingAverageBaseStarMeasuredStringPressure", Nouns.Enum.MovingAverage)]
        [SemanticFact("BaseStarMeasuredStringPressure#01", Verbs.Enum.IsTransformationOutput, "MovingAverageBaseStarMeasuredStringPressure")]
        [SemanticFact("GaussianUncertaintyBaseStarMeasuredStringPressure#01", Nouns.Enum.GaussianUncertainty)]
        [SemanticFact("BaseStarMeasuredStringPressure#01", Verbs.Enum.HasUncertainty, "GaussianUncertaintyBaseStarMeasuredStringPressure#01")]
        [SemanticFact("GaussianUncertaintyBaseStarMeasuredStringPressure#01", Verbs.Enum.HasUncertaintyMean, "BaseStarMeasuredStringPressure#01")]
        [SemanticFact("mudPulseTelemetry", Nouns.Enum.MudPulseTelemetry)]
        [SemanticFact("BaseStarMeasuredStringPressure#01", Verbs.Enum.IsTransmittedBy, "mudPulseTelemetry")]
        [SemanticFact("topOfStringJunction#01", Nouns.Enum.TopOfStringJunction)]
        [SemanticFact("stringHydraulicBranch#01", Nouns.Enum.HydraulicBranch)]
        [SemanticFact("topOfStringJunction#01", Verbs.Enum.HasDownstreamBranch, "stringHydraulicBranch#01")]
        [SemanticFact("BaseStarMeasuredStringPressure#01", Verbs.Enum.IsAssociatedToHydraulicBranch, "stringHydraulicBranch#01")]
        [SemanticFact("BHA#01", Nouns.Enum.BHAMechanicalLogicalElement)]
        [SemanticFact("BaseStarMeasuredStringPressure#01", Verbs.Enum.IsMechanicallyLocatedAt, "BHA#01")]
        [SemanticFact("Halliburton#01", Nouns.Enum.InstrumentationCompany)]
        [SemanticFact("BaseStarMeasuredStringPressure#01", Verbs.Enum.IsProvidedBy, "Halliburton#01")]
        public ScalarProperty? StringPressure { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Assignable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("BaseStarMeasuredAnnulusPressure")]
        [SemanticFact("BaseStarMeasuredAnnulusPressure", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("BaseStarMeasuredAnnulusPressure#01", Nouns.Enum.DerivedMeasurement)]
        [SemanticFact("BaseStarMeasuredAnnulusPressure#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("BaseStarMeasuredAnnulusPressure#01", Verbs.Enum.HasDynamicValue, "BaseStarMeasuredAnnulusPressure")]
        [SemanticFact("BaseStarMeasuredAnnulusPressure#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.PressureDrilling)]
        [SemanticFact("MovingAverageBaseStarMeasuredAnnulusPressure", Nouns.Enum.MovingAverage)]
        [SemanticFact("BaseStarMeasuredAnnulusPressure#01", Verbs.Enum.IsTransformationOutput, "MovingAverageBaseStarMeasuredAnnulusPressure")]
        [SemanticFact("GaussianUncertaintyBaseStarMeasuredAnnulusPressure#01", Nouns.Enum.GaussianUncertainty)]
        [SemanticFact("BaseStarMeasuredAnnulusPressure#01", Verbs.Enum.HasUncertainty, "GaussianUncertaintyBaseStarMeasuredAnnulusPressure#01")]
        [SemanticFact("GaussianUncertaintyBaseStarMeasuredAnnulusPressure#01", Verbs.Enum.HasUncertaintyMean, "BaseStarMeasuredAnnulusPressure#01")]
        [SemanticFact("mudPulseTelemetry", Nouns.Enum.MudPulseTelemetry)]
        [SemanticFact("BaseStarMeasuredAnnulusPressure#01", Verbs.Enum.IsTransmittedBy, "mudPulseTelemetry")]
        [SemanticFact("annulusOutletJunction#01", Nouns.Enum.AnnulusOutletJunction)]
        [SemanticFact("outletHydraulicBranch#01", Nouns.Enum.HydraulicBranch)]
        [SemanticFact("annulusOutletJunction#01", Verbs.Enum.HasUpstreamBranch, "outletHydraulicBranch#01")]
        [SemanticFact("BaseStarMeasuredAnnulusPressure#01", Verbs.Enum.IsAssociatedToHydraulicBranch, "outletHydraulicBranch#01")]
        [SemanticFact("BHA#01", Nouns.Enum.BHAMechanicalLogicalElement)]
        [SemanticFact("BaseStarMeasuredAnnulusPressure#01", Verbs.Enum.IsMechanicallyLocatedAt, "BHA#01")]
        [SemanticFact("Halliburton#01", Nouns.Enum.InstrumentationCompany)]
        [SemanticFact("BaseStarMeasuredAnnulusPressure#01", Verbs.Enum.IsProvidedBy, "Halliburton#01")]
        public ScalarProperty? AnnulusPressure { get; set; } = null;

        [AccessToVariable(CommonProperty.VariableAccessType.Assignable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("BaseStarAverageRotationalSpeed")]
        [SemanticFact("BaseStarAverageRotationalSpeed", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("BaseStarAverageRotationalSpeed#01", Nouns.Enum.DerivedMeasurement)]
        [SemanticFact("BaseStarAverageRotationalSpeed#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("BaseStarAverageRotationalSpeed#01", Verbs.Enum.HasDynamicValue, "BaseStarAverageRotationalSpeed")]
        [SemanticFact("BaseStarAverageRotationalSpeed#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.AngularVelocityDrilling)]
        [SemanticFact("MovingAverageBaseStarAverageRotationalSpeed", Nouns.Enum.MovingAverage)]
        [SemanticFact("BaseStarAverageRotationalSpeed#01", Verbs.Enum.IsTransformationOutput, "MovingAverageBaseStarAverageRotationalSpeed")]
        [SemanticFact("BaseStarRotationalSpeed", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("BaseStarRotationalSpeed#01", Nouns.Enum.DirectMeasurement)]
        [SemanticFact("BaseStarRotationalSpeed#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("BaseStarRotationalSpeed#01", Verbs.Enum.HasDynamicValue, "BaseStarRotationalSpeed")]
        [SemanticFact("BaseStarRotationalSpeed#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.AngularVelocityDrilling)]
        [SemanticFact("BaseStarRotationalSpeed#01", Verbs.Enum.IsTransformationInput, "MovingAverageBaseStarAverageRotationalSpeed")]
        [SemanticFact("GaussianUncertaintyBaseStarAverageRotationalSpeed#01", Nouns.Enum.GaussianUncertainty)]
        [SemanticFact("BaseStarAverageRotationalSpeed#01", Verbs.Enum.HasUncertainty, "GaussianUncertaintyBaseStarAverageRotationalSpeed#01")]
        [SemanticFact("GaussianUncertaintyBaseStarAverageRotationalSpeed#01", Verbs.Enum.HasUncertaintyMean, "BaseStarAverageRotationalSpeed#01")]
        [SemanticFact("mudPulseTelemetry", Nouns.Enum.MudPulseTelemetry)]
        [SemanticFact("BaseStarAverageRotationalSpeed#01", Verbs.Enum.IsTransmittedBy, "mudPulseTelemetry")]
        [SemanticFact("BHA#01", Nouns.Enum.BHAMechanicalLogicalElement)]
        [SemanticFact("BaseStarAverageRotationalSpeed#01", Verbs.Enum.IsMechanicallyLocatedAt, "BHA#01")]
        [SemanticFact("BaseStarRotationalSpeed#01", Verbs.Enum.IsMechanicallyLocatedAt, "BHA#01")]
        [SemanticFact("Halliburton#01", Nouns.Enum.InstrumentationCompany)]
        [SemanticFact("BaseStarAverageRotationalSpeed#01", Verbs.Enum.IsProvidedBy, "Halliburton#01")]
        [SemanticFact("BaseStarRotationalSpeed#01", Verbs.Enum.IsProvidedBy, "Halliburton#01")]
        public ScalarProperty? AverageRotationalSpeed { get; set; } = null;

    }
}
