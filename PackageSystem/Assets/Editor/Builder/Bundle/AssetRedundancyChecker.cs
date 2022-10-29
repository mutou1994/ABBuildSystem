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
        public string bundleName;
        public string refedAsset;
        public string redundancyAsset;
    }

    static string ABPath = AssetBundleBuildUtils.BundleSavePath;

    /// <summary>
    /// ������ʽ�������Դ��AB��ӳ��
    /// </summary>
    static Dictionary<string, string> allAsset2ABMap = new Dictionary<string, string>();

    /// <summary>
    /// ���б���ʽ�������Դ��AB��ӳ�� ����Ӧ��AB��������1�������������Դ����δ����ͬ��AB���������
    /// </summary>
    static Dictionary<string, List<RedundancyInfo>> assetImpRefInAB = new Dictionary<string, List<RedundancyInfo>>();

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

    [MenuItem("ABTools / ���AB����")]
    public static void CheckBundleRedundancy()
    {
        Clear();
        InitAllAsset2ABMap();
        AnalyzeRedundancy();
        Clear();
    }

    public static void Clear()
    {
        allAsset2ABMap.Clear();
        assetImpRefInAB.Clear();
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
                allAsset2ABMap.Add(asset.ToLower(), bundleName);
            }
            foreach(string asset in ab.GetAllScenePaths())
            {
                allAsset2ABMap.Add(asset.ToLower(), bundleName);
            }
            ab.Unload(true);
            index++;
            EditorUtility.DisplayProgressBar("InitAllAsset2ABMap", bundleName, (float)index / (float)count);
        }
        EditorUtility.ClearProgressBar();
    }

    static void AnalyzeRedundancy()
    {
        int totalCount = 0;
        int index = 0;
        int count = allAsset2ABMap.Count;
        EditorUtility.DisplayProgressBar("AnalyzeRedundancy", "start", 0);
        HashSet<string> cache = new HashSet<string>();
        foreach(var pair in allAsset2ABMap)
        {
            string assetPath = pair.Key;
            string bundleName = pair.Value;

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            string file;
            var dps = EditorUtility.CollectDependencies(new UnityEngine.Object[] { asset });
            cache.Clear();
            if (dps != null && dps.Length > 0)
            {
                foreach (var dpAsset in dps)
                {
                    if (dpAsset is MonoScript)
                        continue;
                    //ͼ���������������ȡ��ʽ������Դ������������ȡ���˱���ͼ���������ʱ��ʵ�ʻ�ָ��ͼ����Ӧ��AB�������Դ˴��ɺ���
                    if (dpAsset.name.StartsWith("SpriteAtlasTexture-"))
                        continue;
                    file = AssetDatabase.GetAssetPath(dpAsset);
                    file = file.Replace("\\", "/");
                    if (string.IsNullOrEmpty(file))
                        file = dpAsset.name;
                    file = file.ToLower();
                    if (cache.Add(file))
                    {
                        if (!allAsset2ABMap.ContainsKey(file))
                        {
                            if (!assetImpRefInAB.ContainsKey(file))
                            {
                                assetImpRefInAB.Add(file, new List<RedundancyInfo>());
                            }
                            assetImpRefInAB[file].Add(new RedundancyInfo { bundleName = bundleName, refedAsset = assetPath, redundancyAsset = dpAsset.name });
                            if (assetImpRefInAB[file].Count == 2)
                            {
                                totalCount++;
                            }
                        }
                    }
                }
            }
            EditorUtility.DisplayProgressBar("AnalyzeRedundancy", assetPath, (float)index / (float)count);
        }
        EditorUtility.ClearProgressBar();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<<<<<<Bundle RedundancyInfos>>>>>>>");
        sb.AppendFormat("<<<<<<Redundancy Num:{0}>>>>>>\n", totalCount);
        sb.AppendLine();
        foreach(var pair in assetImpRefInAB)
        {
            var file = pair.Key;
            var redundancyInfos = pair.Value;

            if(redundancyInfos.Count > 1)
            {
                sb.AppendFormat("redundancyAssetPath: {0}\n", file);
                sb.AppendFormat("refed AB Num: {0}\n", redundancyInfos.Count.ToString());
                sb.AppendLine("{");
                sb.AppendLine();
                foreach(var info in redundancyInfos)
                {
                    sb.AppendFormat("BundleName:{0}\n", info.bundleName);
                    sb.AppendFormat("Refed By{0}\n", info.refedAsset);
                    sb.AppendFormat("redundancyAsset:{0}\n", info.redundancyAsset);
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
