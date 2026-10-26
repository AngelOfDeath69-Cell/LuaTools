using System.Reflection;
using LuaToolsGui.Services;
using Xunit;

namespace LuaToolsGui.Tests;

public class SettingsPersistenceTests
{
    [Fact]
    public void EmptySettings_DoNotNeedPersistence() =>
        Assert.False(SettingsService.HasPersistedValues(new AppSettings()));

    [Fact]
    public void DnsModeAsOnlySetting_MustBePersisted()
    {
        // Previously DnsMode was not included in Save()'s empty-state check.
        Assert.True(SettingsService.HasPersistedValues(new AppSettings { DnsMode = "Never" }));
    }

    [Fact]
    public void EverySettingsPropertyByItself_IsConsideredPersisted()
    {
        // A new AppSettings property MUST fail this test until the save predicate is updated.
        foreach (var property in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var settings = new AppSettings();
            Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object value = type == typeof(string) ? "test"
                : type == typeof(bool) ? true
                : type == typeof(int) ? 12
                : throw new InvalidOperationException($"Add test data for {property.Name}: {type}");
            property.SetValue(settings, value);
            Assert.True(SettingsService.HasPersistedValues(settings),
                $"Save() would discard {property.Name} when it is the only set property.");
        }
    }
}
