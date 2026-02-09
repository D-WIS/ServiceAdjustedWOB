using DWIS.API.DTO;
using DWIS.RigOS.Common.Model;
using DWIS.RigOS.Common.Worker;
using DWIS.Vocabulary.Schemas;
using OSDC.DotnetLibraries.Drilling.DrillingProperties;
using OSDC.UnitConversion.Conversion.DrillingEngineering;
using System.Reflection;

namespace DWIS.Service.WOBCorrections.Model
{
    public class CorrectedRecommendationsData : DWISData
    {
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> LocalSparQLQueries = new(BuildSparQLQueries(typeof(CorrectedRecommendationsData)));
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> LocalManifests = new(BuildManifests(typeof(CorrectedRecommendationsData), "ServiceManifest", "DWIS", "DWISService"));
        public override Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> SparQLQueries { get => LocalSparQLQueries; }
        public override Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> Manifests { get => LocalManifests; }

        [AccessToVariable(CommonProperty.VariableAccessType.Assignable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("Corrected_F_bos_rmax")]
        [SemanticFact("Corrected_F_bos_rmax", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("Corrected_F_bos_rmax#01", Nouns.Enum.RecommendedMaximum)]
        [SemanticFact("Corrected_F_bos_rmax#01", Nouns.Enum.WOBLimit)]
        [SemanticFact("Corrected_F_bos_rmax#01", Nouns.Enum.ComputedData)]
        [SemanticFact("Corrected_F_bos_rmax#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("Corrected_F_bos_rmax#01", Verbs.Enum.HasDynamicValue, "Corrected_F_bos_rmax")]
        [SemanticFact("Corrected_F_bos_rmax#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.ForceDrilling)]
        [SemanticFact("WOBCorrectionTransformation", Nouns.Enum.SummationTransformation)]
        [SemanticFact("Corrected_F_bos_rmax#01", Verbs.Enum.IsTransformationOutput, "WOBCorrectionTransformation")]
        [SemanticFact("bos#01", Nouns.Enum.BottomOfStringReferenceLocation)]
        [SemanticFact("Corrected_F_bos_rmax#01", Verbs.Enum.IsPhysicallyLocatedAt, "bos#01")]
        [SemanticFact("stableDrilling", Nouns.Enum.StableDrillingObjective)]
        [SemanticFact("stableFlowrate", Nouns.Enum.StableFlowrateObjective)]
        [SemanticFact("stableRotationalVelocity", Nouns.Enum.StableRotationalVelocityObjective)]
        [SemanticFact("AutoDriller", Nouns.Enum.ControllerFunction)]
        [SemanticFact("AutoDriller", Verbs.Enum.ImplementsObjective, "stableDrilling")]
        [SemanticFact("AutoDriller", Verbs.Enum.ImplementsObjective, "stableFlowrate")]
        [SemanticFact("AutoDriller", Verbs.Enum.ImplementsObjective, "stableRotationalVelocity")]
        [SemanticFact("Corrected_F_bos_rmax#01", Verbs.Enum.IsMaximumLimitFor, "AutoDriller")]
        [SemanticFact("Corrected_F_bos_rmax#01", Verbs.Enum.IsRelatedToDrillingObjective, "stableDrilling")]
        [SemanticFact("ADCSStandardInterface", Nouns.Enum.DWISADCSInterface)]
        [SemanticFact("Corrected_F_bos_rmax#01", Verbs.Enum.IsProvidedTo, "ADCSStandardInterface")]
        [SemanticFact("interpreter#01", Nouns.Enum.Interpreter)]
        [SemanticFact("Corrected_F_bos_rmax#01", Verbs.Enum.IsProvidedBy, "interpreter#01")]
        public ScalarProperty? CorrectedWOBRecommendedMaximum { get; set; } = null;
    }
}
