using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Text;
using System.Linq;

public class AssetBundleBuildUtils
{
    static string DependFileName = "AssetConfig.bytes";
    public static string BundleSavePath = string.Format("{0}/{1}", Application.streamingAssetsPath, "ABResources");
    public static string ProjectPath = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("/Assets") + 1);

    static Dictionary<string, AssetItemInfo> AllAssetInfos = new Dictionary<string, AssetItemInfo>();
    static Dictionary<string, BundleItemInfo> AllBundleInfos = new Dictionary<string, BundleItemInfo>();

    /// <summary>
    /// 最终需要重新打包的AB， BuildBundles可能包含了一些没有变动，但是因为依赖关系的原因而需要重新打包的AB
    /// </summary>
    public static HashSet<string> BuildBundles = new HashSet<string>();
    /// <summary>
    /// 本次打包有变动的AB 用于制作Patch
    /// </summary>
    public static HashSet<string> ChangedBundles = new HashSet<string>();

    public static string ToBundleName(string path)
    {
        path = path.Replace(ProjectPath, "").Replace('\\', '/').Replace(' ', '_').Replace('/', '_');
        path = path.ToLower();
        path = string.Format("{0}.ab", path);
        return path;
    }

    public static void Clear()
    {
        AllAssetInfos.Clear();
        ChangedBundles.Clear();
        BuildBundles.Clear();
        AllBundleInfos.Clear();
    }

    public static bool ContainsAsset(string filePath)
    {
        return AllAssetInfos.ContainsKey(filePath);
    }

    public static List<AssetItemInfo> GetAllAssets()
    {
        return AllAssetInfos.Values.ToList();
    }

    public static List<UnityEngine.Object> GetAllAssetObjects()
    {
        return AllAssetInfos.Select(o => o.Value.asset).ToList();
    }

    public static string[] GetRefBundles(string bundleName)
    {
        if(!AllBundleInfos.ContainsKey(bundleName))
        {
            Debug.LogError("Bundle Not Exists:" + bundleName);
            return null;
        }
        BundleItemInfo bundleItem = AllBundleInfos[bundleName];
        var refChilds = bundleItem.GetRefChilds();
        string[] refBundleNames = new string[refChilds.Length];
        for(int i = 0; i < refChilds.Length; i++)
        {
            refBundleNames[i] = refChilds[i].bundleName;
        }
        return refBundleNames;
    }

    public static AssetItemInfo LoadAssetItem(string filePath)
    {
        filePath = filePath.Replace("\\", "/");
        if(AllAssetInfos.ContainsKey(filePath))
        {
            return AllAssetInfos[filePath];
        }
        else
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(filePath);
            if(asset == null)
            {
                Debug.LogError("Asset Not Exits! with Path:" + filePath);
                BuildLogger.LogError("Asset Not Exits! with Path:{0}", filePath);
                return null;
            }
            AssetItemInfo assetItem = new AssetItemInfo(asset, filePath);
            AllAssetInfos.Add(filePath, assetItem);
            return assetItem;
        }
    }

    public static void GenerateBuildAssets()
    {
        BuildLogger.LogInfo("Begain GenerateBuildAssets");
        var assets = AssetBundleBuildUtils.GetAllAssets();
        int count = assets.Count;
        int index = 0;
        EditorUtility.DisplayProgressBar("GenerateBuildAssets", "Begain GenerateBuildAssets", (float)(index) / count);
        //HashSet<string> buildBundleSet = new HashSet<string>();
        //HashSet<string> changedBundleSet = new HashSet<string>();
        HashSet<string> deps = new HashSet<string>();
        foreach(var asset in assets)
        {
            index++;
            EditorUtility.DisplayProgressBar("GenerateBuildAssets", string.Format("【{0}】{1}", asset.assetType.ToString(), asset.filePath), (float)(index) / count);
            //即使是非独立打包资源，隐式打包的资源也可能会因为有变动而导致所合并的包重打
            if (asset.assetType == AssetType.NoPack)
                continue;
            if (!AssetBuildInfoUtils.CacheChangedAsset(asset) && !AssetBuildInfoUtils.IsBundleChanged(asset.bundleName))
                continue;
            if (ChangedBundles.Contains(asset.bundleName))
                continue;
            ChangedBundles.Add(asset.bundleName);

            if (BuildBundles.Contains(asset.bundleName))
                continue;
            BuildBundles.Add(asset.bundleName);

            GetDepsBundleRecursive(asset.bundleName, deps);
            foreach(string depBundle in deps)
            {
                if(!BuildBundles.Contains(depBundle))
                {
                    BuildBundles.Add(depBundle);
                }
            }
            deps.Clear();
        }
        BuildLogger.LogInfo("GenerateBuildAssets Finish, Bundles Num:{0}", BuildBundles.Count);
        EditorUtility.DisplayProgressBar("GenerateBuildAssets", "Finished. BuildBundles Num:" + BuildBundles.Count, 1);
        EditorUtility.ClearProgressBar();
    }

    static void GetDepsBundleRecursive(string bundleName, HashSet<string> deps)
    {
        if (!AllBundleInfos.ContainsKey(bundleName))
        {
            return;
        }
        BundleItemInfo bundle = AllBundleInfos[bundleName];
        foreach(BundleItemInfo depBundle in bundle.GetRefChilds())
        {
            if(deps.Add(depBundle.bundleName))
            {
                GetDepsBundleRecursive(depBundle.bundleName, deps);
            }
        }
    }

    static void AnalyzeBundleDeps(string bundleName, AssetItemInfo asset)
    {
        if (!AllBundleInfos.ContainsKey(bundleName))
        {
            AllBundleInfos.Add(bundleName, new BundleItemInfo(asset.bundleName));
        }
        var bundle = AllBundleInfos[bundleName];
        foreach(var child in asset.GetRefChilds())
        {
            //独立打包的才需要分析依赖 因为非独立打包的 都已经跟自己合并到一个包里了
            if(child.needSelfExport && !child.bundleName.Equals(bundleName))
            {
                if(!AllBundleInfos.ContainsKey(child.bundleName))
                {
                    AllBundleInfos.Add(child.bundleName, new BundleItemInfo(child.bundleName));
                }
                var childBundle = AllBundleInfos[child.bundleName];
                bundle.AddRefChildAsset(childBundle);
            }
            else
            {
                AnalyzeBundleDeps(bundleName, child);
            }
        }
    }

    public static void GenerateBundleDepConfig()
    {
        int index = 0;
        foreach(var pair in AllAssetInfos)
        {
            var asset = pair.Value;
            if (!asset.needSelfExport) continue;
            if(!AllBundleInfos.ContainsKey(asset.bundleName))
            {
                AllBundleInfos.Add(asset.bundleName, new BundleItemInfo(asset.bundleName));
            }
            var bundle = AllBundleInfos[asset.bundleName];

            EditorUtility.DisplayProgressBar("Analyze AB Dependencies", bundle.bundleName, (float)(index) / AllAssetInfos.Count);
            AnalyzeBundleDeps(asset.bundleName, asset);
        }
        EditorUtility.ClearProgressBar();
        string path = string.Format("{0}/{1}", Application.dataPath, DependFileName);
        if(File.Exists(path))
        {
            File.Delete(path);
        }
        StringBuilder sb = new StringBuilder();
        foreach(var pair in AllBundleInfos)
        {
            var bundleInfo = pair.Value;
            if(bundleInfo.RefCount > 0)
            {
                sb.AppendLine(bundleInfo.bundleName);
                sb.AppendLine(bundleInfo.RefCount.ToString());
                foreach(BundleItemInfo depBundle in bundleInfo.GetRefChilds())
                {
                    sb.AppendLine(depBundle.bundleName);
                }
                // writer.WriteLine();
            }
        }
        File.WriteAllText(path, sb.ToString());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string depAssetPath = path.Replace(Application.dataPath, "Assets");
        AssetItemInfo assetItem = AssetBundleBuildUtils.LoadAssetItem(depAssetPath);
        assetItem.bundleName = AssetBundleBuildUtils.ToBundleName(depAssetPath);
        assetItem.assetType = AssetType.Root;
        assetItem.analyzeDependency = false;
        assetItem.noPackIfNoRef = false;
        assetItem.mergeIfOneRef = false;
        assetItem.certainlyPack = true;
        AllBundleInfos.Add(assetItem.bundleName, new BundleItemInfo(assetItem.bundleName));
    }

    private static string AssetDepsLogPath 
    {
        get
        {
            string path = string.Format("{0}/AssetDepsLog/assetDeps_{1}-{2}.txt", AssetBuildInfoUtils.BuildInfoPath, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), BuildSetting.Instance.Version);
            string directoryName = Path.GetDirectoryName(path);
            if(!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            return path;
        }
    }

    public static void SaveAllAssetsDependencies()
    {
        string path = AssetDepsLogPath;
        if(File.Exists(path))
        {
            File.Delete(path);
        }
        StringBuilder sb = new StringBuilder();

        var all = GetAllAssets();
        sb.AppendLine("<<<<<<All Asset Dependencies>>>>>>");
        sb.AppendFormat("Total Num:{0}\n\n", all.Count);
        for(int i=0;i<all.Count;i++)
        {
            var asset = all[i];
            var deps = asset.GetRefChilds();
            sb.AppendFormat("filePath: {0}\n", asset.filePath);
            sb.AppendFormat("assetType: {0}\n", asset.assetType.ToString());
            sb.AppendFormat("bundleName: {0}\n", asset.bundleName);
            sb.AppendFormat("dependNum: {0}\n", deps.Length.ToString());
            sb.AppendLine("{");
            for (int j=0; j < deps.Length; j++)
            {
                var dep = deps[j];
                sb.AppendLine(dep.filePath);
            }
            sb.AppendLine("}");
            sb.AppendLine("-----------------------------------------------\n");
        }
        File.WriteAllText(path, sb.ToString());
        sb.Clear();
        sb = null;
    }

}
