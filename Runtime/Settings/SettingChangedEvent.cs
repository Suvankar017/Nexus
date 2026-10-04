namespace Nexus.Settings
{
    /// <summary>
    /// Published via <see cref="Events.IEventService"/> whenever a setting's current
    /// value actually changes (a Set/Load that leaves the value unchanged does not publish).
    /// </summary>
    public readonly struct SettingChangedEvent
    {
        public readonly string Key;

        public SettingChangedEvent(string key)
        {
            Key = key;
        }
    }
}
