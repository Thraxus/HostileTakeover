using System.Xml.Serialization;

namespace HostileTakeover.References.Settings
{
    [XmlRoot(nameof(UserSettings), IsNullable = true)]
    public class UserSettings
    {
        [XmlElement(nameof(SettingsDescription))]
        public string SettingsDescription;

        [XmlElement(nameof(MirrorEasyNpcTakeovers))]
        public string MirrorEasyNpcTakeovers;

        [XmlElement(nameof(UseWeaponGroup))]
        public string UseWeaponGroup;

        [XmlElement(nameof(UseMedicalGroup))]
        public string UseMedicalGroup;

        [XmlElement(nameof(UseTrapGroup))]
        public string UseTrapGroup;

        [XmlElement(nameof(UseHighlights))]
        public string UseHighlights;

        [XmlElement(nameof(HighlightAllBlocks))]
        public string HighlightAllBlocks;

        [XmlElement(nameof(HighlightAllBlocksInGroup))]
        public string HighlightAllBlocksInGroup;

        [XmlElement(nameof(HighlightSingleNearestBlockInActiveGroup))]
        public string HighlightSingleNearestBlockInActiveGroup;
    }
}