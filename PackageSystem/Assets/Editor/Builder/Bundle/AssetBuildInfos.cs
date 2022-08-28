using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public class AssetBuildItem
{
    public string filePath;
    public string assetType;
    public string bundleName;
    public string md5;
    public string metaMd5;
}

[Serializable]
public class AssetBuildInfos
{
    public string version;
    public string time;
    public int count;
    public List<AssetBuildItem> buildInfos = new List<AssetBuildItem>();

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
            md5 = Md5Tool.fileMd5(filePath),
            metaMd5 = Md5Tool.fileMd5(string.Format("{0}.meta", filePath))
        };
        buildInfos.Add(buildItem);
        count++;
        return buildItem;
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
}
