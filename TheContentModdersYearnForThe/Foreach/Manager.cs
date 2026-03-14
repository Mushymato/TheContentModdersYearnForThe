using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;

namespace TheContentModdersYearnForThe.Foreach;

public sealed class ForeachManager(IAssetName assetName)
{
    public readonly IAssetName assetName = assetName;

    private Dictionary<string, ForeachData>? data = null;
    public Dictionary<string, ForeachData> Data => data ??= LoadAsset();

    private bool doPopulateWillInvalidateNextTick = true;
    private readonly HashSet<IAssetName> willInvalidateNextTick = [];
    private readonly HashSet<IAssetName> hasLoadedBefore = [];

    private Dictionary<string, ForeachData> LoadAsset()
    {
        Dictionary<string, ForeachData> baseAsset = ModEntry.content.Load<Dictionary<string, ForeachData>>(
            assetName.BaseName
        );
        baseAsset.RemoveWhere(AssetRemovePredicate);
        return baseAsset;
    }

    public bool AssetRemovePredicate(KeyValuePair<string, ForeachData> kv)
    {
        if (ModEntry.registry.GetFromNamespacedId(kv.Key) == null)
        {
            ModEntry.Log($"{assetName}: {kv.Key} is not a namespaced key (i.e. ModId_Name)", LogLevel.Warn);
            return true;
        }
        if (kv.Value.Target == null)
        {
            ModEntry.Log($"{assetName}: {kv.Key} must have 'Target' field", LogLevel.Warn);
            return true;
        }
        if (kv.Value.Modify == null && kv.Value.CopyFromAsset == null)
        {
            ModEntry.Log($"{assetName}: {kv.Key} must have either 'Modify' or 'CopyFrom' field", LogLevel.Warn);
            return true;
        }
        return false;
    }

    public void AssetRequested(AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(assetName))
            e.LoadFrom(() => new Dictionary<string, ForeachData>(), AssetLoadPriority.Exclusive);
        else
            AssetRequestedProcess(e);
    }

    private void AssetRequestedProcess(AssetRequestedEventArgs e)
    {
        hasLoadedBefore.Add(e.NameWithoutLocale);
        foreach (ForeachData dat in Data.Values.OrderBy(kv => kv.Precedence))
        {
            if (dat.MatchesTargetAssetName(e.NameWithoutLocale))
            {
                dat.ApplyEdit(e);
            }
        }
    }

    public void AssetsInvalidated(AssetsInvalidatedEventArgs e)
    {
        hasLoadedBefore.RemoveWhere(e.NamesWithoutLocale.Contains);
        if (data != null)
        {
            foreach (ForeachData dat in data.Values)
            {
                if (
                    dat.CopyFromAssetName != null
                    && e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(dat.CopyFromAssetName))
                )
                {
                    AddTargetToWillInvalidateNextTick(dat);
                }
            }
            willInvalidateNextTick.Remove(assetName);
        }
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(assetName)))
        {
            doPopulateWillInvalidateNextTick = true;
            data = null;
        }
    }

    private void AddTargetToWillInvalidateNextTick(ForeachData dat)
    {
        if (dat.TargetIsPrefix)
            willInvalidateNextTick.AddRange(hasLoadedBefore.Where(name => name.IsDirectlyUnderPath(dat.Target)));
        else
            willInvalidateNextTick.Add(dat.TargetAssetName);
    }

    internal void OnUpdateTicked(UpdateTickedEventArgs e)
    {
        if (doPopulateWillInvalidateNextTick)
        {
            foreach (ForeachData dat in Data.Values)
            {
                AddTargetToWillInvalidateNextTick(dat);
            }
            willInvalidateNextTick.Remove(assetName);
            doPopulateWillInvalidateNextTick = false;
        }
        if (willInvalidateNextTick.Count == 0)
            return;
        foreach (IAssetName name in willInvalidateNextTick)
        {
            ModEntry.content.InvalidateCache(name);
        }
        ModEntry.Log($"{Game1.ticks} Invalidated {string.Join(',', willInvalidateNextTick)}");
        willInvalidateNextTick.Clear();
    }
}
