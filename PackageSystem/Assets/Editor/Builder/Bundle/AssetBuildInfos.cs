using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEditor;

[Serializable]
public class AssetBuildItem
{
    public string filePath;
    public string assetType;
    public string bundleName;
    public string guid;
    public string md5;
    public string metaMd5;
}

[Serializable]
public class BuildBundleItem
{
    public string bundle;
    public string[] refBundles;
    public string[] assets;
}

[Serializable]
public class AssetBuildInfos
{
    public string version;
    public string time;
    public int count;
    public List<AssetBuildItem> buildInfos = new List<AssetBuildItem>();
    public List<BuildBundleItem> bundleInfos = new List<BuildBundleItem>();

    public AssetBuildInfos()
    {
        time = DateTime.Now.ToString("yyy-MM-dd hh:mm:ss");
        count = 0;
    }

    public AssetBuildItem AddBuildItem(string filePath, string assetType, string bundleName)
    {
        AssetBuildItem buildItem = new AssetBuildItem {
            filePath = filePath,
            assetType = assetType,
            bundleName = bundleName,
            guid = AssetDatabase.AssetPathToGUID(filePath),
            md5 = Md5Tool.fileMd5(filePath),
            metaMd5 = Md5Tool.fileMd5(string.Format("{0}.meta", filePath))
        };
        buildInfos.Add(buildItem);
        count++;
        return buildItem;
    }

    public void AddBuildBundleItem(string bundleName, string[] refBundleNames, string[] assets)
    {
        bundleInfos.Add(new BuildBundleItem
        {
            bundle = bundleName,
            refBundles = refBundleNames,
            assets = assets,
        });
    }

    public Dictionary<string, AssetBuildItem> GetBuildInfosMap()
    {
        Dictionary<string, AssetBuildItem> map = new Dictionary<string, AssetBuildItem>();
        for(int i = 0; i < buildInfos.Count; i++)
        {
            var buildItem = buildInfos[i];
            if(!map.ContainsKey(buildItem.filePath))
            {
                map.Add(buildItem.filePath, buildItem);
            }
            else
            {
                Debug.LogError(string.Format("Error: Dunplicate BuildInfos path:{0}", buildItem.filePath));
                BuildLogger.LogError("Error: Dunplicate BuildInfos path:{0}", buildItem.filePath);
                map[buildItem.filePath] = buildItem;
            }
        }
        return map;
    }

    public Dictionary<string, BundleInfo> GetBundleInfosMap()
    {
        Dictionary<string, BundleInfo> bundleInfoMap = new Dictionary<string, BundleInfo>();
        for(int i=0; i < bundleInfos.Count; i++)
        {
            var bundleItem = bundleInfos[i];
            if(!bundleInfoMap.ContainsKey(bundleItem.bundle))
            {
                bundleInfoMap.Add(bundleItem.bundle, new BundleInfo
                {
                    bundleName = bundleItem.bundle,
                    refBundles = new HashSet<string>(bundleItem.refBundles),
                    assets = new HashSet<string>(bundleItem.assets),
                });
            }
            else
            {
                Debug.LogError(string.Format("Error: Dunplicate BundleInfos BundleName:{0}", bundleItem.bundle));
                BuildLogger.LogError("Error: Dunplicate BundleInfos bundleName:{0}", bundleItem.bundle);
                bundleInfoMap[bundleItem.bundle] = new BundleInfo
                {
                    bundleName = bundleItem.bundle,
                    refBundles = new HashSet<string>(bundleItem.refBundles),
                    assets = new HashSet<string>(bundleItem.assets),
                };
            }
        }
        return bundleInfoMap;
    }
}
