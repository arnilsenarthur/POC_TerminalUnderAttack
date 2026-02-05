namespace TUA.Settings
{
    public readonly struct SettingChanged
    {
        public readonly string Key;
        public readonly SettingType Type;
        public readonly SettingValue OldValue;
        public readonly SettingValue NewValue;

        public SettingChanged(string key, SettingType type, SettingValue oldValue, SettingValue newValue)
        {
            Key = key;
            Type = type;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}

