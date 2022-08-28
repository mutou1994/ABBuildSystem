using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public enum AssetType 
{
    //不打包 NoPackIfNoRef类型的资源可能会变成NoPack
    NoPack = 0,
    //被标记需要独立打包的资源
    Root = 1,
    //被依赖的资源
    Asset = 2,
    //需要独立打包的依赖资源
    StandAlone = 3,
    //需要与Root显示合并打包
    Merge = 4,
    //被显示合并打包的Root资源
    RootMerge = 5,
    //被隐式合并打包的Root资源
    RootImpMerge = 6,
}


public class AssetItemInfo
{
    public AssetType assetType = AssetType.Asset;
    public UnityEngine.Object asset;
    public string filePath;
    public string bundleName;
    public bool analyzeDependency = true;
    //只有一个引用时是否合并打包
    public bool mergeIfOneRef = true;
    //没有引用时是否不打包
    public bool noPackIfNoRef = true;

    public bool alreadyAnalyzed = false;

    public override int GetHashCode()
    {
        return asset.GetInstanceID();
    }

    /// <summary>
    /// 依赖我的资源
    /// </summary>
    private HashSet<AssetItemInfo> RefParentSet = new HashSet<AssetItemInfo>();

    /// <summary>
    /// 我依赖的资源
    /// </summary>
    private HashSet<AssetItemInfo> RefChildSet = new HashSet<AssetItemInfo>();

    public AssetItemInfo(UnityEngine.Object asset, string filePath)
    {
        this.asset = asset;
        this.filePath = filePath;
        this.analyzeDependency = true;
        this.mergeIfOneRef = true;
        this.noPackIfNoRef = true;

        this.alreadyAnalyzed = false;
    }

    /// <summary>
    /// 是否需要显式导出AB
    /// </summary>
    public bool needExport
    {
        get
        {
            return (assetType != AssetType.Asset) && (assetType != AssetType.NoPack) &&
                (assetType != AssetType.RootImpMerge);
        }
    }

    /// <summary>
    /// 是否需要独立导出AB包
    /// </summary>
    public bool needSelfExport
    {
        get
        {
            return assetType == AssetType.Root || assetType == AssetType.StandAlone;
        }
    }

    public AssetItemInfo[] GetRefChilds()
    {       
        return RefChildSet.ToArray();
    }

    /// <summary>
    /// 添加依赖我的项
    /// </summary>
    /// <param name="item"></param>
    public void AddRefParentAsset(AssetItemInfo item)
    {
        if(!RefParentSet.Contains(item))
        {
            RefParentSet.Add(item);
        }
    }

    /// <summary>
    /// 添加我依赖的项
    /// </summary>
    /// <param name="item"></param>
    public void AddRefChildAsset(AssetItemInfo item)
    {
        //若已经是我的直接依赖或间接依赖则无需再添加
        if(!AlreadyRefed(item))
        {
            //我依赖了这个项，那么该项所依赖的项我就不需要直接依赖了
            ClearChildRefRecursive(item);

            RefChildSet.Add(item);
            item.AddRefParentAsset(this);

            //我依赖了这个项，那么依赖我的项就不需要直接依赖这个项了
            ClearParentRefRecursive(item);
        }
    }

    /// <summary>
    /// 我依赖了这个项，那么该项所依赖的项我就不需要直接依赖了
    /// </summary>
    /// <param name="item"></param>
    public void ClearChildRefRecursive(AssetItemInfo item)
    {
        List<AssetItemInfo> removeList = new List<AssetItemInfo>();
        foreach(var child in RefChildSet)
        {
            if (item.AlreadyRefed(child))
            {
                removeList.Add(child);
            }
        }
        if(removeList.Count > 0)
        {
            foreach(var child in removeList)
            {
                RemoveRefChild(child);
            }
        }
    }

    /// <summary>
    /// 是否已经直接依赖或间接依赖
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool AlreadyRefed(AssetItemInfo item)
    {
        //直接依赖
        if (RefChildSet.Contains(item)) return true;

        //间接依赖
        foreach(var child in RefChildSet)
        {
            if (child.AlreadyRefed(item))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 我依赖了这个项，那么直接或间接依赖我的项就都不需要直接依赖这个项了
    /// </summary>
    /// <param name="item"></param>
    public void ClearParentRefRecursive(AssetItemInfo item)
    {
        foreach(var parent in RefParentSet)
        {
            if(parent.RefChildSet.Contains(item))
            {
                parent.RemoveRefChild(item);
            }
            parent.ClearParentRefRecursive(item);
        }
    }

    /// <summary>
    /// 移除我直接依赖的项
    /// </summary>
    /// <param name="item"></param>
    public void RemoveRefChild(AssetItemInfo item)
    {
        if (this.RefChildSet.Contains(item))
        {
            this.RefChildSet.Remove(item);
        }
        //同时移除目标中的被依赖集合
        if(item.RefParentSet.Contains(this))
        {
            item.RefParentSet.Remove(this);
        }
    }

    /// <summary>
    /// 统计来自Root的依赖
    /// </summary>
    /// <param name="rootSet"></param>
    public void CollectRoot(HashSet<AssetItemInfo> rootSet)
    {
        switch (this.assetType)
        {
            case AssetType.StandAlone:
            case AssetType.Root:
                if(!rootSet.Contains(this))
                {
                    rootSet.Add(this);
                }
                break;
            default:
                foreach(var item in RefParentSet)
                {
                    item.CollectRoot(rootSet);
                }
                break;
        }
    }

    /// <summary>
    /// 分析来自Root依赖的关系 确定打包关系  不打包，独立打包，或 合并打包
    /// </summary>
    public void AnalyzeAssetType()
    {
        if (this.assetType != AssetType.Asset && this.assetType != AssetType.Root) return;
        if(this.noPackIfNoRef && this.RefParentSet.Count == 0)
        {
            //Asset类型的资源必然有一个Root引用,只可能是NoPackIfNoRef的root资源才有可能没有引用走到这里
            this.assetType = AssetType.NoPack;
        }
        else if(this.mergeIfOneRef)
        {
            //先统计好依赖我的资源的类型 因为assetType是根据依赖我的资源，也就是前置节点来决定的，最终追溯到Root或StandAlone节点
            foreach(var parent in RefParentSet)
            {
                parent.AnalyzeAssetType();
            }
            HashSet<AssetItemInfo> rootSet = new HashSet<AssetItemInfo>();
            foreach(var parent in RefParentSet)
            {
                parent.CollectRoot(rootSet);
            }
            if(rootSet.Count > 1)
            {
                //被需要独立打包的资源(Root或StandAlone)依赖数量超过1个
                if(this.assetType == AssetType.Asset)
                {
                    this.assetType = AssetType.StandAlone;
                    this.bundleName = AssetBundleBuildUtils.ToBundleName(this.filePath);
                }
            }
            else if(rootSet.Count == 1)
            {
                var itr = rootSet.GetEnumerator();
                itr.MoveNext();
                var parent = itr.Current;
                //虽然不必显式合并打包，但是也给设置一个bundleName，方便查看它被自动合到哪个Bundle里了
                this.bundleName = parent.bundleName;
                //parent为standAlone或Root节点，若父节点个数大于0 则说明至少存在2个Root，需要显式合并
                if(parent.RefParentSet.Count > 0)
                {
                    if(this.assetType == AssetType.Root)
                    {
                        this.assetType = AssetType.RootMerge;
                    }
                    else if(this.assetType == AssetType.Asset)
                    {
                        this.assetType = AssetType.Merge;
                    }
                }
                else
                {
                    if(this.assetType == AssetType.Root)
                    {
                        //只有一个依赖的Root MergeIfOneRef资源，则隐式合并打包
                        this.assetType = AssetType.RootImpMerge;
                    }
                    else if(this.assetType == AssetType.Asset)
                    {
                        //Asset资源本身就是隐式合并打包，不需要处理
                    }
                }
            }
        }
    }

    /// <summary>
    /// 分析依赖关系节点
    /// </summary>
    public void AnalyzeDependencies()
    {
        if (!this.analyzeDependency) return;
        if (this.alreadyAnalyzed) return;
        this.alreadyAnalyzed = true;
        var dps = EditorUtility.CollectDependencies(new UnityEngine.Object[] { this.asset });
        var pathSet = new HashSet<string>();
        string filePath;
        AssetItemInfo refItem;
        if (dps != null && dps.Length > 0)
        {
            foreach(var dpAsset in dps)
            {
                if(dpAsset is MonoScript || dpAsset is LightingDataAsset)
                    continue;
                filePath = AssetDatabase.GetAssetPath(dpAsset);
                filePath = filePath.Replace("\\", "/");
                if (dpAsset.name.StartsWith("SpriteAtlasTexture-"))
                    continue;
                if(string.IsNullOrEmpty(filePath))
                {
                    Debug.LogError("Depend Not Exits:" + dpAsset.name + " " + this.filePath);
                    BuildLogger.LogError("Error!!! Depend Not Exits:{0}  {1}", dpAsset.name, this.filePath);
                    continue;
                }
                if (filePath.Equals("Library/unity default resources"))
                    continue;
                if (filePath.StartsWith("Resources"))
                    continue;
                if(!File.Exists(filePath))
                {
                    Debug.LogError("Asset Not Exits:" + this.filePath);
                    BuildLogger.LogError("Asset Not Exits:{0}", filePath);
                    continue;
                }
                if(!filePath.Equals(this.filePath))
                {
                    pathSet.Add(filePath);
                }
            }
            foreach(var path in pathSet)
            {
                if (path.Equals(this.filePath))
                    continue;
                refItem = AssetBundleBuildUtils.LoadAssetItem(path);
                if(refItem.assetType != AssetType.Root)
                {
                    refItem.assetType = AssetType.Asset;
                    refItem.analyzeDependency = true;
                    refItem.mergeIfOneRef = true;
                    refItem.noPackIfNoRef = true;
                }
                this.AddRefChildAsset(refItem);
                refItem.AnalyzeDependencies();
            }
        }
    }
}
