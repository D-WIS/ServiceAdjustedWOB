using DWIS.API.DTO;
using DWIS.RigOS.Common.Model;
using DWIS.RigOS.Common.Worker;
using DWIS.Vocabulary.Schemas;
using OSDC.DotnetLibraries.Drilling.DrillingProperties;
using System.Reflection;
using DWIS.Service.WOBCorrections.Model;

namespace DWIS.Service.WOBCorrections.Server
{
    public class BHADrillStringProperty : SemanticInfo
    {
        public ModelShared.DrillString? BHADrillString { get; set; } = null;
    }
    public class BHADrillStringData : DWISData
    {
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> LocalSparQLQueries = new(BuildSparQLQueries(typeof(BHADrillStringData)));
        private static readonly Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> LocalManifests = new(BuildManifests(typeof(BHADrillStringData), "ContextualDataBridgeManifest", "DWIS", "DWISBridge"));
        public override Lazy<IReadOnlyDictionary<PropertyInfo, Dictionary<string, QuerySpecification>>> SparQLQueries { get => LocalSparQLQueries; }
        public override Lazy<IReadOnlyDictionary<PropertyInfo, ManifestFile>> Manifests { get => LocalManifests; }

        [AccessToVariable(CommonProperty.VariableAccessType.Assignable)]
        [Mandatory(CommonProperty.MandatoryType.General)]
        [SemanticStringVariable("BHADrillStringDescription")]
        [SemanticFact("BHADrillStringDescription", Nouns.Enum.DynamicDrillingSignal)]
        [SemanticFact("BHADrillStringDescription#01", Nouns.Enum.DrillingDataPoint)]
        [SemanticFact("BHADrillStringDescription#01", Nouns.Enum.DrillStringDescription)]
        [SemanticFact("BHADrillStringDescription#01", Nouns.Enum.JSonDataType)]
        [SemanticFact("BHADrillStringDescription#01", Verbs.Enum.HasDynamicValue, "BHADrillStringDescription")]
        [SemanticFact("Current#01", Nouns.Enum.Current)]
        [SemanticFact("BHADrillStringDescription#01", Verbs.Enum.IsCharacterizedBy, "Current#01")]
        [SemanticFact("Advisor#01", Nouns.Enum.Advisor)]
        [SemanticFact("BHADrillStringDescription#01", Verbs.Enum.IsProvidedTo, "Advisor#01")]
        [SemanticFact("DataProvider#01", Nouns.Enum.DataProvider)]
        [SemanticFact("BHADrillStringDescription#01", Verbs.Enum.IsProvidedTo, "DataProvider#01")]
        [SemanticFact("contextualDataBuilder#01", Nouns.Enum.DWISContextualDataBuilder)]
        [SemanticFact("BHADrillStringDescription#01", Verbs.Enum.IsProvidedBy, "contextualDataBuilder#01")]
        public BHADrillStringProperty? BHADrillString { get; set; } = null;
    }
}
