using System;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using Sandbox.Game.Entities;

namespace HostileTakeover.Common.BaseClasses
{
    public abstract class BaseXmlUserSettings
    {
        private readonly string _settingsFileName;
        private const string Extension = ".xml";

        protected BaseXmlUserSettings(string modName)
        {
            _settingsFileName = modName + "_Settings" + Extension;
        }

        protected abstract void SettingsMapper();

        protected T Get<T>()
        {
            return Utilities.FileHandlers.Load.ReadXmlFileInWorldStorage<T>(_settingsFileName);
        }

        protected void Set<T>(T settings)
        {
            if (settings == null) return;
            Utilities.FileHandlers.Save.WriteXmlFileToWorldStorage(_settingsFileName, settings);
        }
    }

    public class Settings : BaseXmlUserSettings
    {
        private XmlSettings _userSettings;

        public Settings(string modName) : base(modName)
        {
            _userSettings = Get<XmlSettings>();
            SettingsMapper();
            CleanUserSettings();
            Set(_userSettings);
        }

        private void CleanUserSettings()
        {
            _userSettings.UseWeaponGroup = _userSettings.UseWeaponGroup.ToLower();
            _userSettings.UseMedicalGroup = _userSettings.UseMedicalGroup.ToLower();
            _userSettings.UseTrapGroup = _userSettings.UseTrapGroup.ToLower();
            _userSettings.UseHighlights = _userSettings.UseHighlights.ToLower();
            _userSettings.HighlightAllBlocks = _userSettings.HighlightAllBlocks.ToLower();
            _userSettings.HighlightAllBlocksInGroup = _userSettings.HighlightAllBlocksInGroup.ToLower();
            _userSettings.HighlightSingleNearestBlockInActiveGroup = _userSettings.HighlightSingleNearestBlockInActiveGroup.ToLower();
        }

        protected sealed override void SettingsMapper()
        {
            if (_userSettings == null)
            {
                _userSettings = new XmlSettings();
                _userSettings = DefaultSettings.CopyTo(_userSettings);
                return;
            }

            _userSettings.SettingsDescription = DefaultSettings.SettingsDescription;

            if (!bool.TryParse(_userSettings.UseWeaponGroup, out DefaultSettings.UseWeaponGroup))
                _userSettings.UseWeaponGroup = DefaultSettings.UseWeaponGroup.ToString().ToLower();

            if (!bool.TryParse(_userSettings.UseMedicalGroup, out DefaultSettings.UseMedicalGroup))
                _userSettings.UseMedicalGroup = DefaultSettings.UseMedicalGroup.ToString().ToLower();

            if (!bool.TryParse(_userSettings.UseTrapGroup, out DefaultSettings.UseTrapGroup))
                _userSettings.UseTrapGroup = DefaultSettings.UseTrapGroup.ToString().ToLower();

            if (!bool.TryParse(_userSettings.UseHighlights, out DefaultSettings.UseHighlights))
                _userSettings.UseHighlights = DefaultSettings.UseHighlights.ToString().ToLower();
            
            if (!bool.TryParse(_userSettings.HighlightAllBlocks, out DefaultSettings.HighlightAllBlocks))
                _userSettings.HighlightAllBlocks = DefaultSettings.HighlightAllBlocks.ToString().ToLower();

            if (DefaultSettings.HighlightAllBlocks)
            {
                DefaultSettings.HighlightAllBlocksInGroup = false;
                _userSettings.HighlightAllBlocksInGroup = false.ToString().ToLower();
                DefaultSettings.HighlightSingleNearestBlockInActiveGroup = false;
                _userSettings.HighlightSingleNearestBlockInActiveGroup = false.ToString().ToLower();
                return;
            }

            if (!bool.TryParse(_userSettings.HighlightAllBlocksInGroup, out DefaultSettings.HighlightAllBlocksInGroup))
                _userSettings.HighlightAllBlocksInGroup = DefaultSettings.HighlightAllBlocksInGroup.ToString().ToLower();

            if (DefaultSettings.HighlightAllBlocksInGroup)
            {
                DefaultSettings.HighlightSingleNearestBlockInActiveGroup = false;
                _userSettings.HighlightSingleNearestBlockInActiveGroup = false.ToString().ToLower();
                return;
            }

            if (!bool.TryParse(_userSettings.HighlightSingleNearestBlockInActiveGroup, out DefaultSettings.HighlightSingleNearestBlockInActiveGroup))
                _userSettings.HighlightSingleNearestBlockInActiveGroup = DefaultSettings.HighlightSingleNearestBlockInActiveGroup.ToString().ToLower();

            if (!DefaultSettings.UseHighlights || 
                DefaultSettings.HighlightAllBlocks ||
                DefaultSettings.HighlightAllBlocksInGroup) return;
            
            DefaultSettings.HighlightSingleNearestBlockInActiveGroup = true;
            _userSettings.HighlightSingleNearestBlockInActiveGroup = true.ToString().ToLower();
        }

        public static class DefaultSettings
        {
            public const string SettingsDescription =
                "\n\t\t1) Use XX Group settings all default to TRUE.  Setting any to FALSE will stop that block group from being required when disowning a ship." +
                "\n\t\t2) UseHighlights defaults to TRUE.  Setting it to FALSE will disable all highlighting and bypass any other highlighting rule." +
                "\n\t\t3) HighlightAllBlocks defaults to FALSE.  Setting it to TRUE will highlight all blocks on a grid that need to be disabled for disownership.  This will disable further highlight options." +
                "\n\t\t4) HighlightAllBlocksInGroup defaults to FALSE.  Setting it to TRUE will highlight all blocks in a group that need to be disabled for disownership and disable single block highlighting." +
                "\n\t\t5) HighlightSingleNearestBlockInActiveGroup defaults to TRUE.  Setting it to FALSE will essentially disable all highlighting.  This is the final highlight option processed." +
                "\n\t\tBonus) To mirror Easy NPC Takeovers, set all group settings and UseHighlights to FALSE.  Nothing else needs to change." +
                "\n\t";

            public static bool UseWeaponGroup = true;
            public static bool UseMedicalGroup = true;
            public static bool UseTrapGroup = true;
            public static bool UseHighlights = true;
            public static bool HighlightAllBlocks = false;
            public static bool HighlightAllBlocksInGroup = false;
            public static bool HighlightSingleNearestBlockInActiveGroup = true;

            public static XmlSettings CopyTo(XmlSettings userSettings)
            {
                userSettings.SettingsDescription = SettingsDescription;
                userSettings.UseWeaponGroup = UseWeaponGroup.ToString().ToLower();
                userSettings.UseMedicalGroup = UseMedicalGroup.ToString().ToLower();
                userSettings.UseTrapGroup = UseTrapGroup.ToString().ToLower();
                userSettings.UseHighlights = UseHighlights.ToString().ToLower();
                userSettings.HighlightAllBlocks = HighlightAllBlocks.ToString().ToLower();
                userSettings.HighlightAllBlocksInGroup = HighlightAllBlocksInGroup.ToString().ToLower();
                userSettings.HighlightSingleNearestBlockInActiveGroup = HighlightSingleNearestBlockInActiveGroup.ToString().ToLower();
                return userSettings;
            }
        }

        [XmlRoot(nameof(XmlSettings), IsNullable = true)]
        public class XmlSettings
        {
            [XmlElement(nameof(SettingsDescription))]
            public string SettingsDescription;

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
}