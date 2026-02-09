using DWIS.API.DTO;
using DWIS.RigOS.Common.Model;
using DWIS.RigOS.Common.Worker;
using DWIS.Vocabulary.Schemas;
using OSDC.DotnetLibraries.Drilling.DrillingProperties;
using OSDC.UnitConversion.Conversion.DrillingEngineering;
using System.Reflection;

namespace DWIS.Service.WOBCorrections.Model
{
    public class AdvisorRecommendationsData :DWISData
    {
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> LocalSparQLQueries = new(BuildSparQLQueries(typeof(AdvisorRecommendationsData)));
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> LocalManifests = new(BuildManifests(typeof(AdvisorRecommendationsData), "AdvisorManifest", "DWIS", "DWISService"));
        public override Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> SparQLQueries { get => LocalSparQLQueries; }
        public override Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> Manifests { get => LocalManifests; }

        [AccessToVariable(CommonProperty.VariableAccessType.Assignable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticDiracVariable("F_bos_max")]
        [SemanticFact("F_bos_max", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("F_bos_max#01", Nouns.Enum.MaximumLimit)]
        [SemanticFact("F_bos_max#01", Nouns.Enum.WOBLimit)]
        [SemanticFact("F_bos_max#01", Nouns.Enum.ContinuousDataType)]
        [SemanticFact("F_bos_max#01", Verbs.Enum.HasDynamicValue, "F_bos_max")]
        [SemanticFact("F_bos_max#01", Verbs.Enum.IsOfMeasurableQuantity, DrillingPhysicalQuantity.QuantityEnum.ForceDrilling)]
        [SemanticFact("bos#01", Nouns.Enum.BottomOfStringReferenceLocation)]
        [SemanticFact("F_bos_max#01", Verbs.Enum.IsPhysicallyLocatedAt, "bos#01")]
        [SemanticFact("stableDrilling", Nouns.Enum.StableDrillingObjective)]
        [SemanticFact("stableFlowrate", Nouns.Enum.StableFlowrateObjective)]
        [SemanticFact("stableRotationalVelocity", Nouns.Enum.StableRotationalVelocityObjective)]
        [SemanticFact("AutoDriller", Nouns.Enum.ControllerFunction)]
        [SemanticFact("AutoDriller", Verbs.Enum.ImplementsObjective, "stableDrilling")]
        [SemanticFact("AutoDriller", Verbs.Enum.ImplementsObjective, "stableFlowrate")]
        [SemanticFact("AutoDriller", Verbs.Enum.ImplementsObjective, "stableRotationalVelocity")]
        [SemanticFact("F_bos_max#01", Verbs.Enum.IsMaximumLimitFor, "AutoDriller")]
        [SemanticFact("F_bos_max#01", Verbs.Enum.IsRelatedToDrillingObjective, "stableDrilling")]
        [SemanticFact("ADCSStandardInterface", Nouns.Enum.DWISADCSInterface)]
        [SemanticFact("F_bos_max#01", Verbs.Enum.IsProvidedBy, "ADCSStandardInterface")]
        public ScalarProperty? WOBMaxLimit { get; set; } = null;
    }
}
