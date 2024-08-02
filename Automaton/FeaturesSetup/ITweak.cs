namespace Automaton.FeaturesSetup;

public interface ITweak : IDisposable
{
    // https://github.com/Haselnussbomber/HaselTweaks
    Type CachedType { get; }
    string InternalName { get; }
    IncompatibilityWarningAttribute[] IncompatibilityWarnings { get; }
    RequirementAttribute[] Requirements { get; }

    string Name { get; }
    string Description { get; }

    bool Outdated { get; }
    bool Ready { get; }
    bool Enabled { get; }

    void SetupAddressHooks();
    void SetupVTableHooks();

    void Enable();
    void Disable();
    void DrawConfig();
    void OnConfigChange(string fieldName);
}
