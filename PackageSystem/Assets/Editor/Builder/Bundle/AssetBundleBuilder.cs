using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text.RegularExpressions;

public class AssetBundleBuilder
{
    static string[] AnyPatterns = { "*.*" };

    [MenuItem("ABTools/BuildABTotal")]
    public static void BuildABTotal()
    {
        BuildAB(false);
    }

    [MenuItem("ABTools/BuildABTotalAdditive")]
    public static void BuildABTotalAdditive()
    {
        BuildAB(true);
    }

    [MenuItem("ABTools/BuildABPatch")]
    public static void BuildABPatch()
    {
        BuildPatch();
    }

    static void BuildAB(bool isAdditive)
    {
        BuildLogger.LogInfo("BuildAB begain on platform: {0}......", EditorUserBuildSettings.activeBuildTarget);
        DateTime startTime = DateTime.Now;
        DateTime time = DateTime.Now;
        if(!isAdditive)
        {
            ClearPreBuildAB();
            BuildLogger.LogInfo("ClearPreBuildAB cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
        }
        AssetBundleBuildUtils.Clear();
        AssetBuildInfoUtils.Clear();
        BuildSetting.Instance.Clear();
        BuildInAssetsProccesser.CopyBuildInAssetsToEditor();
        time = DateTime.Now;
        MakeABSetting();
        BuildLogger.LogInfo("MakeABSetting cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
        time = DateTime.Now;
        ReplaceBuildInAssetsReference();
        BuildLogger.LogInfo("ReplaceBuildInAssetsReference cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
        time = DateTime.Now;
        Analyze();
        BuildLogger.LogInfo("Analyze cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
        time = DateTime.Now;
        AssetBundleBuildUtils.GenerateBundleDepConfig();
        BuildLogger.LogInfo("GenerateDependenciesConfig cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
        time = DateTime.Now;
        if(isAdditive)
        {
            AssetBuildInfoUtils.ReadPreBuildInfos();
        }
        AssetBuildInfoUtils.GenerateBuildInfos();
        AssetBundleBuildUtils.GenerateBuildAssets();
        BuildLogger.LogInfo("Compare PreVersion cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
        time = DateTime.Now;
        AssetBundleManifest manifest = ExportAB();
        RemoveManifest();
        if(isAdditive)
        {
            time = DateTime.Now;
            RemoveUnUsedAB();
            BuildLogger.LogInfo("RemoveUnUsedAB cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
        }
        BuildLogger.LogInfo("ExportAB cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
        time = DateTime.Now;
        AssetBuildInfoUtils.UpdateGameVersion(false);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AssetRedundancyChecker.CheckBundleRedundancy();
        AssetBuildInfoUtils.SaveBuildInfos();
        AssetBuildInfoUtils.SaveAssetChangeInfos();
        AssetBundleBuildUtils.SaveAllAssetsDependencies();
        AssetBuildInfoUtils.SaveAllBundleInfos();
        BuildLogger.LogInfo("SaveBuildInfos cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
        time = DateTime.Now;
        RevertBuildInAssetsReference();
        BuildLogger.LogInfo("RevertBuildInAssetsReference cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
        AssetBuildInfoLogger.SaveLog();
        BuildLogger.LogInfo("Build AB Finish cost time: {0}s", (int)(DateTime.Now - startTime).TotalSeconds);
        BuildLogger.SaveLog();
        Debug.LogError("BuildAB Finished");

        AssetBundleBuildUtils.Clear();
        AssetBuildInfoUtils.Clear();
    }

    static void RemoveManifest()
    {
        DateTime time = DateTime.Now;
        BuildLogger.LogInfo("Begain Remove Manifest");
        DirectoryInfo di = new DirectoryInfo(AssetBundleBuildUtils.BundleSavePath);
        string manifestName = AssetBundleBuildUtils.BundleSavePath.Substring(AssetBundleBuildUtils.BundleSavePath.LastIndexOf("/") + 1);
        string manifestPath = string.Format("{0}/{1}", AssetBundleBuildUtils.BundleSavePath, manifestName);
        if(File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
            if (File.Exists(manifestPath + ".meta"))
            {
                File.Delete(manifestPath + ".meta");
            }
        }
        FileInfo[] abFiles = di.GetFiles("*.manifest");
        for(int i = 0; i < abFiles.Length; i++)
        {
            FileInfo fi = abFiles[i];
            fi.Delete();
            if(File.Exists(fi.FullName + ".meta"))
            {
                File.Delete(fi.FullName + ".meta");
            }
        }
        BuildLogger.LogInfo("Remove Manifest Finish Cost Time: {0}", (int)(DateTime.Now - time).TotalSeconds);
        //AssetDatabase.SaveAssets();
        //AssetDatabase.Refresh();
    }

    static void RemoveUnUsedAB()
    {
        HashSet<string> usedSet = new HashSet<string>();
        foreach(var asset in AssetBundleBuildUtils.GetAllAssets())
        {
            
            if(asset.needSelfExport)
            {
                usedSet.Add(asset.bundleName);
            }
        }
        DirectoryInfo di = new DirectoryInfo(AssetBundleBuildUtils.BundleSavePath);
        FileInfo[] abFiles = di.GetFiles("*.ab");
        for(int i=0; i<abFiles.Length; i++)
        {
            FileInfo fi = abFiles[i];
            if(!usedSet.Contains(fi.Name))
            {
                fi.Delete();
                if(File.Exists(fi.FullName + ".meta"))
                {
                    File.Delete(fi.FullName + ".meta");
                }
            }
        }
        //AssetDatabase.SaveAssets();
        //AssetDatabase.Refresh();
    }

    public static void BuildPatch()
    {
        BuildLogger.LogInfo("BuildAB_Patch begain on platform: {0}......", EditorUserBuildSettings.activeBuildTarget);
        AssetBuildInfoUtils.Clear();
        PatchBuildUtils.Clear();
        AssetBundleBuildUtils.Clear();
        BuildSetting.Instance.Clear();
        DateTime startTime = DateTime.Now;
        DateTime time = DateTime.Now;
        if(!AssetBuildInfoUtils.ReadPreBuildInfos())
        {
            BuildLogger.LogError("BuildAB_Patch Error!!! please check buildversion.txt");
            BuildLogger.LogError("ParseBuildInfo cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
            Debug.LogError("BuildAB_Patch Error!!! please check buildversion.txt");
        }
        else
        {
            BuildLogger.LogInfo("Read ParseBuildInfo success!!! cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
            BuildInAssetsProccesser.CopyBuildInAssetsToEditor();
            time = DateTime.Now;
            MakeABSetting();
            BuildLogger.LogInfo("MakeABSetting cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
            time = DateTime.Now;
            ReplaceBuildInAssetsReference();
            BuildLogger.LogInfo("ReplaceBuildInAssetsReference cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
            time = DateTime.Now;
            Analyze();
            BuildLogger.LogInfo("Analyze cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
            time = DateTime.Now;
            AssetBundleBuildUtils.GenerateBundleDepConfig();
            BuildLogger.LogInfo("GenerateDependenciesConfig cost time:{0}s", (int)(DateTime.Now - time).TotalSeconds);
            time = DateTime.Now;
            AssetBuildInfoUtils.GenerateBuildInfos();
            AssetBundleBuildUtils.GenerateBuildAssets();
            BuildLogger.LogInfo("Compare PreVersion cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
            if(AssetBundleBuildUtils.BuildBundles.Count > 0)
            {
                time = DateTime.Now;
                AssetBundleManifest bundleManifest = ExportAB();
                RemoveManifest();
                time = DateTime.Now;
                RemoveUnUsedAB();
                BuildLogger.LogInfo("RemoveUnUsedAB cost time : {0}s", (int)(DateTime.Now - time).TotalSeconds);
                BuildLogger.LogInfo("ExportAB cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
                time = DateTime.Now;
                AssetBuildInfoUtils.UpdateGameVersion(true);
                PatchBuildUtils.GeneratePatchs();
                BuildLogger.LogInfo("GenerateReleasePackage cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                time = DateTime.Now;
                AssetRedundancyChecker.CheckBundleRedundancy();
                AssetBuildInfoUtils.SaveBuildInfos();
                AssetBuildInfoUtils.SaveAssetChangeInfos();
                AssetBundleBuildUtils.SaveAllAssetsDependencies();
                AssetBuildInfoUtils.SaveAllBundleInfos();
                BuildLogger.LogInfo("SaveBuildInfos cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
                time = DateTime.Now;
                RevertBuildInAssetsReference();
                BuildLogger.LogInfo("RevertBuildInAssetsReference cost time: {0}s", (int)(DateTime.Now - time).TotalSeconds);
                AssetBuildInfoLogger.SaveLog();
            }
            else
            {
                BuildLogger.LogInfo("No Asset Changed, No Need To Generate Patch");
            }
            BuildLogger.LogInfo("BuildAB_Patch finish total cost time: {0}s", (int)(DateTime.Now - startTime).TotalSeconds);
            BuildLogger.SaveLog();
            Debug.LogError("BuildAB_Patch finished");

            AssetBuildInfoUtils.Clear();
            PatchBuildUtils.Clear();
            AssetBundleBuildUtils.Clear();
        }
    }

    public static void ClearPreBuildAB()
    {
        if(Directory.Exists(AssetBundleBuildUtils.BundleSavePath))
        {
            Directory.Delete(AssetBundleBuildUtils.BundleSavePath, true);
        }
        if(File.Exists(AssetBundleBuildUtils.BundleSavePath + ".meta"))
        {
            File.Delete(AssetBundleBuildUtils.BundleSavePath + ".meta");
        }
        AssetDatabase.Refresh();
    }

    public static AssetBundleManifest ExportAB()
    {
        List<AssetBundleBuild> list = new List<AssetBundleBuild>();
        Dictionary<string, List<string>> BuildAssets = new Dictionary<string, List<string>>();
        foreach(string ab in AssetBundleBuildUtils.BuildBundles)
        {
            BuildAssets.Add(ab, new List<string>());
        }
        foreach(var asset in AssetBundleBuildUtils.GetAllAssets())
        {
            AssetBuildInfoLogger.LogAssetBuild(asset.assetType.ToString(), asset.bundleName, asset.filePath);
            if(!asset.needExport)
            {
                continue;
            }
            if(BuildAssets.ContainsKey(asset.bundleName))
            {
                BuildAssets[asset.bundleName].Add(asset.filePath);
            }
        }
        foreach(var pair in BuildAssets)
        {
            AssetBundleBuild build = new AssetBundleBuild();
            build.assetBundleName = pair.Key;
            build.assetNames = pair.Value.ToArray();
            list.Add(build);
        }
        if(!Directory.Exists(AssetBundleBuildUtils.BundleSavePath))
        {
            Directory.CreateDirectory(AssetBundleBuildUtils.BundleSavePath);
        }
        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(AssetBundleBuildUtils.BundleSavePath, list.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.DisableWriteTypeTree, EditorUserBuildSettings.activeBuildTarget);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return manifest;
    }

    public static List<UnityEngine.Object> GetWillPackageAssets()
    {
        if(!PackageConfigs.Instance)
        {
            Debug.LogError("Create PackageConfig First!!!!");
            return null;
        }
        HashSet<string> map = new HashSet<string>();
        List<UnityEngine.Object> list = new List<UnityEngine.Object>();
        var groups = PackageConfigs.Instance.assetConfigs;
        string assetPath = string.Empty;
        foreach(var group in groups)
        {
            foreach(var assetInfo in group.assetInfos)
            {
                assetPath = AssetDatabase.GetAssetPath(assetInfo.asset);
                //是目录
                if(Directory.Exists(assetPath))
                {
                    if (assetPath.Contains(".svn")) continue;
                    string[] patterns = string.IsNullOrEmpty(assetInfo.searchPattern) ? AnyPatterns : assetInfo.searchPattern.Split('|');
                    foreach(var pattern in patterns)
                    {
                        var files = Directory.GetFiles(assetPath, pattern, SearchOption.AllDirectories);
                        foreach(var file in files)
                        {
                            if (file.EndsWith(".meta") || file.Contains(".svn") || file.EndsWith(".bat") || file.EndsWith(".ds_store")) continue;
                            string filePath = file.Replace("\\", "/");
                            if(map.Add(filePath))
                            {
                                list.Add(AssetDatabase.LoadMainAssetAtPath(filePath));
                            }
                        }
                    }
                }
                else
                {
                    list.Add(assetInfo.asset);
                }
            }
        }
        map.Clear();
        return list;
    }

    public static void ReplaceBuildInAssetsReference()
    {
        var all = AssetBundleBuildUtils.GetAllAssetObjects();
        BuildInAssetsProccesser.ReplaceBuildInAssetsReference(all);
    }

    public static void RevertBuildInAssetsReference()
    {
        var all = AssetBundleBuildUtils.GetAllAssetObjects();
        BuildInAssetsProccesser.RevertBuildInAssetsReference(all);
        BuildInAssetsProccesser.SaveLogToFile();
        BuildInAssetsProccesser.Clear();
    }

    public static void MakeABSetting()
    { 
        if(!PackageConfigs.Instance)
        {
            Debug.LogError("Create PackageConfig First!!!!");
            return;
        }
        var groups = PackageConfigs.Instance.assetConfigs;
        for(int i=0;i<groups.Count;i++)
        {
            var group = groups[i]; 
            if(group.assetInfos.Count == 0)
            {
                continue;
            }
            if(group.exportType == ExportType.AllInOnePack)
            {
                AllInOnePack(group);
            }
            else if(group.exportType == ExportType.EachFilePack)
            {
                EachFilePack(group);
            }
            else if(group.exportType == ExportType.EachItemPack)
            {
                EachItemPack(group);
            }
            else if(group.exportType == ExportType.FirstChildPack)
            {
                FirstChildPack(group);
            }
            else if(group.exportType == ExportType.EachChildPack)
            {
                EachChildPack(group);
            }
        }
    }

    public static bool CheckChinese(string str)
    {
        if(Regex.IsMatch(str, @"[\u4e00-\u9fa5]"))
        {
            return true;
        }
        return false;
    }

    public static void PackOneFile(string filePath, string bundleName, bool analyzeDps, bool mergeIfOneRef, bool noPackIfNoRef)
    {
        filePath = filePath.Replace('\\', '/');
        if (filePath.EndsWith(".meta") || filePath.Contains(".svn") || filePath.EndsWith(".bat") || filePath.EndsWith(".ds_store")) return;
        if(CheckChinese(filePath))
        {
            Debug.LogError(string.Format("{0} 包含中文", filePath));
            return;
        }
        
        // bool isNew = !AssetBuildUtils.ContainsAsset(filePath);
        var assetItem = AssetBundleBuildUtils.LoadAssetItem(filePath);
        if (assetItem == null) return;
        assetItem.bundleName = bundleName;
        assetItem.assetType = AssetType.Root;
        assetItem.analyzeDependency = analyzeDps;
        assetItem.noPackIfNoRef = noPackIfNoRef;
        assetItem.mergeIfOneRef = mergeIfOneRef;
        assetItem.certainlyPack = !noPackIfNoRef;
        /*if(isNew && analyzeDps)
        {
            AnalyzeDependenciesNode(assetItem);
        }*/
    }

    public static void Analyze()
    {
        EditorUtility.DisplayProgressBar("Analyze", "Begain Analyze", 0);
        var all = AssetBundleBuildUtils.GetAllAssets();
        int index = 0;
        int count = all.Count;
        foreach(var asset in all)
        {
            index++;
            EditorUtility.DisplayProgressBar("Analyze", "AnalyzeDependencies:" + asset.filePath, (float)index / count);
            //if(asset.noPackIfNoRef)
            //{
                //没有引用则不打包的资源，不确定最终是否会打包，所以不主动分析依赖
            //}
            //else
            //{
                //noPackIfNoRef的资源也应该分析依赖，
                //noPackIfNoRef的资源若没有引用会最终会转为NoPack类型，不会影响所引用资源的打包，
                //但如果有引用，却没有分析依赖，则会导致所引用资源的Root依赖次数统计不正确，最终影响打包
                asset.AnalyzeDependencies();
            //}
        }
        all = AssetBundleBuildUtils.GetAllAssets();
        index = 0;
        count = all.Count;
        foreach(var asset in all)
        {
            asset.AnalyzeAssetType();
            index++;
            EditorUtility.DisplayProgressBar("Analyze", string.Format("AnalyzeAssetType: {0} Type:{1}", asset.filePath, asset.assetType.ToString()), (float)index / count);
        }
        EditorUtility.ClearProgressBar();
    }

    public static void AllInOnePack(AssetGroupInfo groupInfo)
    {
        if (groupInfo.exportType != ExportType.AllInOnePack) return;
        string bundleName, assetPath;
        if(!string.IsNullOrEmpty(groupInfo.groupName))
        {
            bundleName = AssetBundleBuildUtils.ToBundleName(groupInfo.groupName);
        }
        else
        {
            assetPath = AssetDatabase.GetAssetPath(groupInfo.assetInfos[0].asset);
            bundleName = AssetBundleBuildUtils.ToBundleName(assetPath);
        }

        foreach(var assetInfo in groupInfo.assetInfos)
        {
            assetPath = AssetDatabase.GetAssetPath(assetInfo.asset);
            if(CheckChinese(assetPath))
            {
                Debug.LogError(string.Format("{0} 包含中文", assetPath));
                continue;
            }
            //是目录
            if(Directory.Exists(assetPath))
            {
                if (assetPath.Contains(".svn")) continue;
                string[] patterns = string.IsNullOrEmpty(assetInfo.searchPattern) ? AnyPatterns : assetInfo.searchPattern.Split('|');
                foreach(var pattern in patterns)
                {
                    var files = Directory.GetFiles(assetPath, pattern, SearchOption.AllDirectories);
                    foreach(var file in files)
                    {
                        PackOneFile(file, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
                    }
                }
            }
            else
            {
                //是文件
                PackOneFile(assetPath, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
            }
        }
    }

    public static void EachFilePack(AssetGroupInfo groupInfo)
    {
        if (groupInfo.exportType != ExportType.EachFilePack) return;
        string bundleName, assetPath;
        foreach(var assetInfo in groupInfo.assetInfos)
        {
            assetPath = AssetDatabase.GetAssetPath(assetInfo.asset);
            if(CheckChinese(assetPath))
            {
                Debug.LogError(string.Format("{0} 包含中文", assetPath));
                continue;
            }
            //是目录
            if(Directory.Exists(assetPath))
            {
                if (assetPath.Contains(".svn")) continue;
                string[] patterns = string.IsNullOrEmpty(assetInfo.searchPattern) ? AnyPatterns : assetInfo.searchPattern.Split('|');
                foreach(var pattern in patterns)
                {
                    var files = Directory.GetFiles(assetPath, pattern, SearchOption.AllDirectories);
                    foreach(string file in files)
                    {
                        bundleName = AssetBundleBuildUtils.ToBundleName(file);
                        PackOneFile(file, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
                    }
                }
            }
            else
            {
                //是文件
                bundleName = AssetBundleBuildUtils.ToBundleName(assetPath);
                PackOneFile(assetPath, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
            }
        }
    }

    public static void EachItemPack(AssetGroupInfo groupInfo)
    {
        if (groupInfo.exportType != ExportType.EachItemPack) return;
        string bundleName, assetPath;
        foreach(var assetInfo in groupInfo.assetInfos)
        {
            assetPath = AssetDatabase.GetAssetPath(assetInfo.asset);
            bundleName = AssetBundleBuildUtils.ToBundleName(assetPath);
            if(CheckChinese(assetPath))
            {
                Debug.LogError(string.Format("{0} 包含中文", assetPath));
                continue;
            }
            //是目录
            if(Directory.Exists(assetPath))
            {
                if (assetPath.Contains(".svn")) continue;
                string[] patterns = string.IsNullOrEmpty(assetInfo.searchPattern) ? AnyPatterns : assetInfo.searchPattern.Split('|');
                foreach (var pattern in patterns)
                {
                    var files = Directory.GetFiles(assetPath, pattern, SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        PackOneFile(file, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
                    }
                }
            }
            else
            {
                //是文件
                PackOneFile(assetPath, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
            }
        }
    }

    /// <summary>
    /// 对Group下每个Item的第一级Child打一个包 包括目录或单个文件
    /// </summary>
    /// <param name="groupInfo"></param>
    public static void FirstChildPack(AssetGroupInfo groupInfo)
    {
        if (groupInfo.exportType != ExportType.FirstChildPack) return;
        string bundleName, assetPath;
        foreach(var assetInfo in groupInfo.assetInfos)
        {
            assetPath = AssetDatabase.GetAssetPath(assetInfo.asset);
            if(CheckChinese(assetPath))
            {
                Debug.LogError(string.Format("{0} 包含中文", assetPath));
                continue;
            }
            //是目录
            if(Directory.Exists(assetPath))
            {
                if (assetPath.Contains(".svn")) continue;
                string[] patterns = string.IsNullOrEmpty(assetInfo.searchPattern) ? AnyPatterns : assetInfo.searchPattern.Split('|');

                //取第一级Child打包
                var dirs = Directory.GetDirectories(assetPath, "*", SearchOption.TopDirectoryOnly);
                foreach(var dir in dirs)
                {
                    bundleName = AssetBundleBuildUtils.ToBundleName(dir);
                    foreach (var pattern in patterns)
                    {
                        var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            PackOneFile(file, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
                        }
                    }
                }

                foreach (var pattern in patterns)
                {
                    var _files = Directory.GetFiles(assetPath, pattern, SearchOption.TopDirectoryOnly);
                    foreach (var file in _files)
                    {
                        bundleName = AssetBundleBuildUtils.ToBundleName(file);
                        PackOneFile(file, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
                    }
                }
            }
            else
            {
                //是文件
                bundleName = AssetBundleBuildUtils.ToBundleName(assetPath);
                PackOneFile(assetPath, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
            }
        }
    }

    /// <summary>
    /// 对Group下每个Item的每一级Child打一个包 包括目录或单个文件
    /// </summary>
    /// <param name="groupInfo"></param>
    public static void EachChildPack(AssetGroupInfo groupInfo)
    {
        if (groupInfo.exportType != ExportType.EachChildPack) return;
        string bundleName, assetPath;
        foreach(var assetInfo in groupInfo.assetInfos)
        {
            assetPath = AssetDatabase.GetAssetPath(assetInfo.asset);
            if(CheckChinese(assetPath))
            {
                Debug.LogError(string.Format("{0} 包含中文", assetPath));
                continue;
            }
            //是目录
            if(Directory.Exists(assetPath))
            {
                if (assetPath.Contains(".svn")) continue;
                string[] patterns = string.IsNullOrEmpty(assetInfo.searchPattern) ? AnyPatterns : assetInfo.searchPattern.Split('|');

                //取所有Child打包
                var dirs = Directory.GetDirectories(assetPath, "*", SearchOption.AllDirectories);
                foreach (var dir in dirs)
                {
                    bundleName = AssetBundleBuildUtils.ToBundleName(dir);
                    foreach(var pattern in patterns)
                    {
                        var files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
                        foreach(var file in files)
                        {
                            PackOneFile(file, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
                        }
                    }
                }

                foreach(var pattern in patterns)
                {
                    var _files = Directory.GetFiles(assetPath, pattern, SearchOption.TopDirectoryOnly);
                    foreach(var file in _files)
                    {
                        bundleName = AssetBundleBuildUtils.ToBundleName(file);
                        PackOneFile(file, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
                    }
                }
            }
            else
            {
                //是文件
                bundleName = AssetBundleBuildUtils.ToBundleName(assetPath);
                PackOneFile(assetPath, bundleName, assetInfo.AnalyzeDependency, assetInfo.mergeIfOneRef, assetInfo.noPackIfNoRef);
            }
        }
    }
}
