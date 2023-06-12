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
}