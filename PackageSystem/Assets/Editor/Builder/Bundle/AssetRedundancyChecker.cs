using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Text;

public class AssetRedundancyChecker
{
    struct RedundancyInfo
    {
        public string refedAsset;
        public string redundancyAsset;
    }

    static string ABPath = AssetBundleBuildUtils.BundleSavePath;

    /// <summary>
    /// 隐式打包冗余的资源数量
    /// </summary>
    static int impInMultiABNum = 0;

    /// <summary>
    /// 所有显式打包的资源与AB的映射
    /// </summary>
    static Dictionary<string, string> allAsset2ABMap = new Dictionary<string, string>();

    /// <summary>
    /// 所有被隐式打包的资源与AB的映射 若对应的AB数量超过1个，则表明该资源被多次打进不同的AB里，存在冗余
    /// </summary>
    /// redundancyAssetName
    /// {
    ///     bundleName
    ///     [
    ///         refedAsset1,
    ///         refedAsset2,
    ///         ...
    ///     ]
    ///     
    ///     buundleName
    ///     [
    ///         ...
    ///     ]
    ///     
    ///     ...
    /// }
    static Dictionary<string, Dictionary<string, List<string>>> assetImpRefInAB = new Dictionary<string, Dictionary<string, List<string>>>();

    /// <summary>
    /// 显式打到多个AB里的资源 显式打包冗余 属于打包错误
    /// </summary>
    static Dictionary<string, List<string>> assetInMultiAB = new Dictionary<string, List<string>>();

    public static string RedundancyInfoPath
    {
        get
        {
            string path = string.Format("{0}/RedundancyInfoReport/redundancyInfo_{1}-{2}.txt", AssetBuildInfoUtils.BuildInfoPath, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), BuildSetting.Instance.Version);
            string directoryName = Path.GetDirectoryName(path);
            if(!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            return path;
        }
    }

    [MenuItem("ABTools / 检查AB冗余")]
    public static void CheckBundleRedundancy()
    {
        impInMultiABNum = 0;
        Clear();
        InitAllAsset2ABMap();
        AnalyzeRedundancy();
        SaveToFile();
        Clear();
    }

    public static void Clear()
    {
        allAsset2ABMap.Clear();
        assetImpRefInAB.Clear();
        assetInMultiAB.Clear();
    }

    static void InitAllAsset2ABMap()
    {
        var all = AssetBundleBuildUtils.GetAllAssets();
        string[] abFiles = Directory.GetFiles(ABPath, "*.ab", SearchOption.AllDirectories);
        int index = 0;
        int count = abFiles.Length;
        EditorUtility.DisplayProgressBar("InitAllAsset2ABMap", "start", 0);
        foreach(string abFile in abFiles)
        {
            AssetBundle ab = AssetBundle.LoadFromFile(abFile);
            string bundleName = Path.GetFileName(abFile);
            foreach (string asset in ab.GetAllAssetNames())
            {
                string assetPath = asset.ToLower();
                if(!allAsset2ABMap.ContainsKey(assetPath))
                {
                    allAsset2ABMap.Add(assetPath, bundleName);
                }
                else
                {
                    Debug.LogError("RedundancyChecker, asset In MultiAB:" + assetPath + " BundleName:" + bundleName);
                    BuildLogger.LogError("RedundancyChecker, asset In MultiAB:{0} ABName:{1}", assetPath, bundleName);
                    if (!assetInMultiAB.ContainsKey(assetPath))
                    {
                        assetInMultiAB.Add(assetPath, new List<string>());
                    }
                    assetInMultiAB[assetPath].Add(bundleName);
                }
            }
            foreach(string asset in ab.GetAllScenePaths())
            {
                string assetPath = asset.ToLower();
                if(!allAsset2ABMap.ContainsKey(assetPath))
                {
                    allAsset2ABMap.Add(assetPath, bundleName);
                }
                else
                {
                    Debug.LogError("RedundancyChecker, asset In MultiAB:" + assetPath + " ABName:" + bundleName);
                    BuildLogger.LogError("RedundancyChecker, asset In MultiAB:{0} ABName:{1}", assetPath, bundleName);
                    if(!assetInMultiAB.ContainsKey(assetPath))
                    {
                        assetInMultiAB.Add(assetPath, new List<string>());
                    }
                    assetInMultiAB[assetPath].Add(bundleName);
                }
            }
            ab.Unload(true);
            index++;
            EditorUtility.DisplayProgressBar("InitAllAsset2ABMap", bundleName, (float)index / (float)count);
        }
        EditorUtility.ClearProgressBar();
    }

    static void AnalyzeRedundancy()
    {
        impInMultiABNum = 0;
        int index = 0;
        int count = allAsset2ABMap.Count;
        EditorUtility.DisplayProgressBar("AnalyzeRedundancy", "start", 0);
        //HashSet<string> cache = new HashSet<string>();
        foreach (var pair in allAsset2ABMap)
        {
            string assetPath = pair.Key;
            string bundleName = pair.Value;

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            string filePath;
            var dps = EditorUtility.CollectDependencies(new UnityEngine.Object[] { asset });
            //cache.Clear();
            if (dps != null && dps.Length > 0)
            {
                foreach (var dpAsset in dps)
                {
                    if (dpAsset is MonoScript)
                        continue;
                    //图集相关依赖，由于取的式本地资源的依赖，所以取到了本地图集，打包的时候实际会指向图集对应的AB包，所以此处可忽略
                    if (dpAsset.name.StartsWith("SpriteAtlasTexture-"))
                        continue;
                    filePath = AssetDatabase.GetAssetPath(dpAsset);
                    if (string.IsNullOrEmpty(filePath))
                        filePath = dpAsset.name;
                    filePath = filePath.Replace("\\", "/");
                    filePath = filePath.ToLower();
                    if (filePath.Contains("library/unity default resources") || filePath.Contains("resources/unity_builtin_extra"))
                    {
                        filePath = filePath + "/" + dpAsset.name;
                    }
                    //if (cache.Add(file))
                    //{
                    if (!allAsset2ABMap.ContainsKey(filePath))
                    {
                        if (!assetImpRefInAB.ContainsKey(filePath))
                        {
                            assetImpRefInAB.Add(filePath, new Dictionary<string, List<string>>());
                        }
                        if (!assetImpRefInAB[filePath].ContainsKey(bundleName))
                        {
                            assetImpRefInAB[filePath].Add(bundleName, new List<string>());
                            if (assetImpRefInAB[filePath].Count == 2)
                            {
                                impInMultiABNum++;
                            }
                        }
                        assetImpRefInAB[filePath][bundleName].Add(assetPath);
                    }
                    //}
                }
            }
            EditorUtility.DisplayProgressBar("AnalyzeRedundancy", assetPath, (float)index / (float)count);
        }
        EditorUtility.ClearProgressBar();
    }
    
    static void SaveToFile()
    { 
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<<<<<<Bundle RedundancyInfos>>>>>>>");
        sb.AppendFormat("<<<<<<Redundancy Num:{0}>>>>>>\n", impInMultiABNum + assetInMultiAB.Count);
        sb.AppendLine();

        sb.AppendFormat("<<<<<<<<AssetInMultiABObvious Num:{0}>>>>>>>>\n", assetInMultiAB.Count);
        foreach(var pair in assetInMultiAB)
        {
            var redundancyAssetPath = pair.Key;
            var obviousABs = pair.Value;

            if(obviousABs.Count > 1)
            {
                sb.AppendFormat("redundancyAssetPath:{0}\n", redundancyAssetPath);
                sb.AppendFormat("obvious AB Num:{0}\n", obviousABs.Count.ToString());
                sb.AppendLine("{");
                foreach(var abName in obviousABs)
                {
                    sb.AppendFormat("    BundleName:{0}\n", abName);
                }
                sb.AppendLine("}");
                sb.AppendLine("-------------------------------------------------------\n");
            }
        }

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendFormat("<<<<<<<AssetInMultiABImplicit Num:{0}>>>>>>>>\n", impInMultiABNum);
        foreach(var pair in assetImpRefInAB)
        {
            var redundancyAssetPath = pair.Key;
            var redundancyInfos = pair.Value;

            if(redundancyInfos.Count > 1)
            {
                sb.AppendFormat("redundancyAssetPath: {0}\n", redundancyAssetPath);
                sb.AppendFormat("refed AB Num: {0}\n", redundancyInfos.Count.ToString());
                sb.AppendLine("{");
                foreach(var redundancyInfo in redundancyInfos)
                {
                    var abName = redundancyInfo.Key;
                    var refedAssets = redundancyInfo.Value;

                    sb.AppendFormat("    BundleName:{0}\n", abName);
                    sb.AppendFormat("    RefedAsset Num:{0}\n", refedAssets.Count.ToString());
                    sb.AppendLine("    [");
                    for(int i = 0; i < refedAssets.Count; i++)
                    {
                        sb.AppendFormat("        {0}\n", refedAssets[i]);
                    }
                    sb.AppendLine("    ]");
                    sb.AppendLine();
                }
                sb.AppendLine("}");
                sb.AppendLine("----------------------------------------------------------------------\n");
            }
        }
        File.WriteAllText(RedundancyInfoPath, sb.ToString());
        sb.Clear();
    }
}
