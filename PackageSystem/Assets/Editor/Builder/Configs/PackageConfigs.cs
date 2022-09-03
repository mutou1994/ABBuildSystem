using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public enum ExportType 
{ 
    //全部打到一个包里
    AllInOnePack = 0,
    //每个文件分开打包
    EachFilePack = 1,
    //每个Item打一个包
    EachItemPack = 2,
    //每个Item的第一级Child打一个包 包括目录或单个文件
    FirstChildPack = 3,
    //每个Item的每一级Child打一个包 包括目录或单个文件
    EachChildPack = 4,
}

[Serializable]
public class AssetInfo
{
    public UnityEngine.Object asset;
    public string searchPattern = "*.*";
    //是否需要分析依赖  由于生成AB依赖关系需要，每个资源都应分析依赖关系
    //public bool analyzeDependency = true;
    //只有一个引用时是否合并打包
    public bool mergeIfOneRef = false;
    //没有引用时是否不打包
    public bool noPackIfNoRef = false;

    public bool AnalyzeDependency 
    {
        get
        {
            //由于生成AB依赖关系需要，每个资源都应分析依赖关系
            //return analyzeDependency;
            return true;
        }
    }
}

[Serializable]
public class AssetGroupInfo
{
    public string groupName;
    public ExportType exportType = ExportType.EachFilePack;
    public List<AssetInfo> assetInfos = new List<AssetInfo>();
}


[CreateAssetMenu]
public class PackageConfigs : ScriptableObject
{
    public List<AssetGroupInfo> assetConfigs = new List<AssetGroupInfo>();
    private static PackageConfigs _instance;

    public static PackageConfigs Instance 
    {
        get
        {
            if(_instance == null)
            {
                _instance = AssetDatabase.LoadAssetAtPath<PackageConfigs>("Assets/Editor/ConfigSettings/PackageConfigs.asset");
            }
            return _instance;
        }
    }

}
