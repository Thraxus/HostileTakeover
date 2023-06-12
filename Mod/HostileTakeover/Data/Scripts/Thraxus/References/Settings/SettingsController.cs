using HostileTakeover.Common.BaseClasses;

namespace HostileTakeover.References.Settings
{
    public class SettingsController : BaseXmlUserSettings
    {
        private UserSettings _userSettings;

        public SettingsController(string modName) : base(modName) { }

        public void Initialize()
        {
            _userSettings = Get<UserSettings>();
            SettingsMapper();
            CleanUserSettings();
            Set(_userSettings);
        }

        private void CleanUserSettings()
        {
            _userSettings.MirrorEasyNpcTakeovers = _userSettings.MirrorEasyNpcTakeovers.ToLower();
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
                _userSettings = new UserSettings();
                _userSettings = DefaultSettings.CopyTo(_userSettings);
                return;
            }

            _userSettings.SettingsDescription = DefaultSettings.SettingsDescription;

            if (!bool.TryParse(_userSettings.MirrorEasyNpcTakeovers, out DefaultSettings.MirrorEasyNpcTakeovers))
                _userSettings.MirrorEasyNpcTakeovers = DefaultSettings.MirrorEasyNpcTakeovers.ToString().ToLower();

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
    }
}