namespace Automaton.FeaturesSetup.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RequirementAttribute(string InternalName, string repo) : Attribute
{
    public string InternalName { get; } = InternalName;
    public string Repo { get; } = repo;

    public bool IsLoaded => Svc.PluginInterface.InstalledPlugins.Any(p => p.InternalName == InternalName && p.IsLoaded);
}
