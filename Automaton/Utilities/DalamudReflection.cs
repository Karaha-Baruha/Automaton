using ECommons.Reflection;
using System.Reflection;

namespace Automaton.Utilities;
public class DalamudReflection
{
    public static bool HasRepo(string repository)
    {
        var conf = DalamudReflector.GetService("Dalamud.Configuration.Internal.DalamudConfiguration");
        var repolist = (IEnumerable<object>)conf.GetFoP("ThirdRepoList");
        if (repolist != null)
            foreach (var r in repolist)
                if ((string)r.GetFoP("Url") == repository)
                    return true;
        return false;
    }

    public static void AddRepo(string repository, bool enabled)
    {
        var conf = DalamudReflector.GetService("Dalamud.Configuration.Internal.DalamudConfiguration");
        var repolist = (IEnumerable<object>)conf.GetFoP("ThirdRepoList");
        if (repolist != null)
            foreach (var r in repolist)
                if ((string)r.GetFoP("Url") == repository)
                    return;
        var instance = Activator.CreateInstance(Svc.PluginInterface.GetType().Assembly.GetType("Dalamud.Configuration.ThirdPartyRepoSettings")!);
        instance.SetFoP("Url", repository);
        instance.SetFoP("IsEnabled", enabled);
        conf.GetFoP<IList<object>>("ThirdRepoList").Add(instance!);
    }

    public static void ReloadPluginMasters()
    {
        var mgr = DalamudReflector.GetService("Dalamud.Plugin.Internal.PluginManager");
        var pluginReload = mgr?.GetType().GetMethod("SetPluginReposFromConfigAsync", BindingFlags.Instance | BindingFlags.Public);
        pluginReload?.Invoke(mgr, [true]);
    }

    public static void SaveDalamudConfig()
    {
        var conf = DalamudReflector.GetService("Dalamud.Configuration.Internal.DalamudConfiguration");
        var configSave = conf?.GetType().GetMethod("QueueSave", BindingFlags.Instance | BindingFlags.Public);
        configSave?.Invoke(conf, null);
    }
}
