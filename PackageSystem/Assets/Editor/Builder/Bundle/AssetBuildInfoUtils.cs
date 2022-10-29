using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using System.Linq;

public struct BundleInfo 
{
    public string bundleName;
    public HashSet<string> refBundles;
    public HashSet<string> assets;
}


public class AssetBuildInfoUtils
{
    static string buildInfoPath;
    static string projName;
    static string configSavePath;
    static string preGameVersion;
    static int prePatchVersion = 0;
    static AssetBuildInfos curBuildInfos;
    static AssetBuildInfos preBuildInfos;
    static Dictionary<string, AssetBuildItem> CurBuildInfoMap;
    static Dictionary<string, BundleInfo> CurBundleInfoMap;
    static Dictionary<string, AssetBuildItem> PreBuildInfoMap;
    static Dictionary<string, BundleInfo> PreBundleInfoMap;

    public static List<AssetItemInfo> ChangedAssets = new List<AssetItemInfo>();

    public static string GetPlatformName()
    {
        string str = "win32";
        BuildTarget activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
        if (activeBuildTarget == BuildTarget.StandaloneWindows)
        {
            str = "win32";
        }
        else if (activeBuildTarget == BuildTarget.StandaloneWindows64)
        {
            str = "win64";
        }
        else if (activeBuildTarget == BuildTarget.iOS)
        {
            str = "ios";
        }
        else if (activeBuildTarget == BuildTarget.Android)
        {
            str = "android";
        }
        return str;
    }

    public static string ProjRootName
    {
        get
        {
            if (string.IsNullOrEmpty(projName))
            {
                string dataPath = Application.dataPath.Replace("\\", "/");
                dataPath = dataPath.Substring(0, dataPath.LastIndexOf("/Assets"));
                projName = dataPath.Substring(dataPath.LastIndexOf("/") + 1);
            }
            return projName;
        }
    }

    public static string BuildInfoPath
    {
        get
        {
            if (string.IsNullOrEmpty(buildInfoPath))
            {
                buildInfoPath = string.Format("{0}/../../AssetBuildInfo/{1}/{2}", Application.dataPath, ProjRootName, GetPlatformName());
            }
            if (!Directory.Exists(buildInfoPath))
            {
                Directory.CreateDirectory(buildInfoPath);
            }
            return buildInfoPath;
        }
    }

    public static string ConfigSavePath
    {
        get
        {
            if (string.IsNullOrEmpty(configSavePath))
            {
                configSavePath = string.Format("{0}/Config/BuildInfo", BuildInfoPath);
            }
            return configSavePath;
        }
    }


    public static string BuildVersionPath
    {
        get
        {
            return string.Format("{0}/Config/BuildInfo/buildversion.txt", BuildInfoPath);
        }
    }

    public static void Clear()
    {
        preGameVersion = string.Empty;
        prePatchVersion = 0;
        curBuildInfos = null;
        preBuildInfos = null;
        if (CurBuildInfoMap != null)
        {
            CurBuildInfoMap.Clear();
            CurBuildInfoMap = null;
        }
        if (CurBundleInfoMap != null)
        {
            CurBundleInfoMap.Clear();
            CurBundleInfoMap = null;
        }
        if (PreBuildInfoMap != null)
        {
            PreBuildInfoMap.Clear();
            PreBuildInfoMap = null;
        }
        if (PreBundleInfoMap != null)
        {
            PreBundleInfoMap.Clear();
            PreBundleInfoMap = null;
        }
        ChangedAssets.Clear();
    }

    public static void UpdateGameVersion(bool isPatch)
    {
        int patchVersion = 0;
        if (isPatch)
        {
            if (!BuildSetting.Instance.GameVersion.Equals(preGameVersion))
            {
                BuildSetting.Instance.GameVersion = preGameVersion;
            }
            patchVersion = prePatchVersion + 1;
        }
        BuildSetting.Instance.PatchVersion = patchVersion;
        //UpdateReleaseVersionContext(BuildSetting.Instance.Version);
        UpdateBuildVersionContext(BuildSetting.Instance.Version);
        //UpdateProjectSettingVersion(BuildSetting.Instance.Version);
        EditorUtility.SetDirty(BuildSetting.Instance);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void UpdateBuildVersionContext(string version)
    {
        string directoryName = Path.GetDirectoryName(BuildVersionPath);
        if (!Directory.Exists(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }
        BuildLogger.LogInfo("UpdateBuildVersionContext: path-{0}, version-{1}", BuildVersionPath, version);
        File.WriteAllText(BuildVersionPath, version);
    }

    public static void GenerateBuildInfos()
    {
        BuildLogger.LogInfo("Begain GenerateBuildInfos");
        EditorUtility.DisplayProgressBar("GenerateBuildInfos", "Begain GenerateBuildInfos", 0);
        CurBuildInfoMap = new Dictionary<string, AssetBuildItem>();
        CurBundleInfoMap = new Dictionary<string, BundleInfo>();
        curBuildInfos = new AssetBuildInfos();
        var all = AssetBundleBuildUtils.GetAllAssets();
        int count = all.Count;
        int index = 0;
        foreach (var asset in all)
        {
            index++;
            EditorUtility.DisplayProgressBar("GenerateBuildInfos", string.Format("【{0}】 {1}", asset.assetType.ToString(), asset.filePath), (float)(index) / count);
            //非主动导出资源也应该记录比较差异 因为隐式合并的资源有变动的话，其所合并的包也应该重打
            //NoRefNoPack为没有引用，不需要打包的资源 所以不需要记录MD5
            if (asset.assetType != AssetType.NoPack)
            {
                if (!CurBuildInfoMap.ContainsKey(asset.filePath))
                {
                    var item = curBuildInfos.AddBuildItem(asset.filePath, asset.assetType.ToString(), asset.bundleName);
                    CurBuildInfoMap.Add(asset.filePath, item);
                }
                else
                {
                    var item = CurBuildInfoMap[asset.filePath];
                    item.bundleName = asset.bundleName;
                    Debug.LogError(string.Format("Error!!! Dunplicate Asset When Generate BuildInfos Path:{0}", asset.filePath));
                    BuildLogger.LogError("Error!!! Dunplicate Asset When Generate BuildInfos Path:{0}", asset.filePath);
                }

                if (!CurBundleInfoMap.ContainsKey(asset.bundleName))
                {
                    var refs = AssetBundleBuildUtils.GetRefBundles(asset.bundleName);
                    CurBundleInfoMap.Add(asset.bundleName, new BundleInfo
                    {
                        bundleName = asset.bundleName,
                        refBundles = refs != null ? new HashSet<string>(refs) : new HashSet<string>(),
                        assets = new HashSet<string>(),
                    });
                }
                CurBundleInfoMap[asset.bundleName].assets.Add(asset.filePath);
            }
        }
        foreach (var pair in CurBundleInfoMap)
        {
            var bundleName = pair.Key;
            var bundleInfo = pair.Value;
            curBuildInfos.AddBuildBundleItem(bundleName, bundleInfo.refBundles.ToArray(), bundleInfo.assets.ToArray());
        }
        EditorUtility.ClearProgressBar();
        BuildLogger.LogInfo("Finish GenerateBuildInfos, buildinfo Num:{0}", curBuildInfos.count);
    }

    public static bool HavePreBuildInfo
    {
        get
        {
            return preBuildInfos != null;
        }
    }

    public static bool ReadPreBuildInfos()
    {
        EditorUtility.DisplayProgressBar("Begain ReadPreBuildInfos", "", 0);
        if (File.Exists(BuildVersionPath))
        {
            string preVersion = File.ReadAllText(BuildVersionPath);
            string path = string.Format("{0}/buildinfo_{1}.json", ConfigSavePath, preVersion);
            if (File.Exists(path))
            {
                string content = File.ReadAllText(path);
                string[] strs = preVersion.Split('p');
                preGameVersion = strs[0];
                prePatchVersion = int.Parse(strs[1]);

                preBuildInfos = JsonUtility.FromJson<AssetBuildInfos>(content);
                EditorUtility.DisplayProgressBar("Begain ReadPreBuildInfos", "ReadJson Finished" + preBuildInfos.count, 0.5f);
                PreBuildInfoMap = preBuildInfos.GetBuildInfosMap();
                PreBundleInfoMap = preBuildInfos.GetBundleInfosMap();
                EditorUtility.DisplayProgressBar("Begain ReadPreBuildInfos", "Generate PreBuildInfoMap finished", 1);
                EditorUtility.ClearProgressBar();
                return true;
            }
            else
            {
                BuildLogger.LogError("ReadPreBuildInfo Error!!! {0} not exits!", path);
                Debug.LogError(string.Format("ReadPreBuildInfo Error!!! {0} not exits!", path));
            }
        }
        else
        {
            BuildLogger.LogError("ReadPreBuildInfo Error!!! {0} not exits!", BuildVersionPath);
            Debug.LogError(string.Format("ReadPreBuildInfo Error!!! {0} not exits!", BuildVersionPath));
        }
        EditorUtility.ClearProgressBar();
        return false;
    }

    public static void SaveBuildInfos()
    {
        curBuildInfos.version = BuildSetting.Instance.Version;
        string path = string.Format("{0}/buildinfo_{1}.json", ConfigSavePath, BuildSetting.Instance.Version);
        if (Directory.Exists(ConfigSavePath))
        {
            Directory.CreateDirectory(ConfigSavePath);
        }
        string json = JsonUtility.ToJson(curBuildInfos);
        File.WriteAllText(path, json);
    }

    public static AssetBuildItem GetAssetPreBuildInfo(AssetItemInfo asset)
    {
        string filePath = asset.filePath;
        AssetBuildItem preInfo = null;
        if (PreBuildInfoMap != null)
        {
            PreBuildInfoMap.TryGetValue(filePath, out preInfo);
        }
        return preInfo;
    }

    public static AssetBuildItem GetAssetCurBuildInfo(AssetItemInfo asset)
    {
        string filePath = asset.filePath;
        AssetBuildItem curInfo = null;
        if (CurBuildInfoMap != null)
        {
            CurBuildInfoMap.TryGetValue(filePath, out curInfo);
        }
        return curInfo;
    }

    public static bool IsAssetChanged(AssetItemInfo asset)
    {
        string filePath = asset.filePath;
        if (!CurBuildInfoMap.ContainsKey(filePath))
        {
            Debug.LogError("Error!!! BuildInfo Not Found When Check Asset Change! path:" + filePath);
            BuildLogger.LogError("Error!!! BuildInfo Not Found When Check Asset Change! path:{0}", filePath);
            return false;
        }
        if (PreBuildInfoMap == null || !PreBuildInfoMap.ContainsKey(filePath))
            return true;
        var preInfo = PreBuildInfoMap[filePath];
        var curInfo = CurBuildInfoMap[filePath];
        if (!preInfo.assetType.Equals(curInfo.assetType))
        {
            return true;
        }
        if (!preInfo.bundleName.Equals(curInfo.bundleName))
        {
            return true;
        }
        if (!IsMetaImportant(asset))
        {
            return !preInfo.md5.Equals(curInfo.md5);
        }
        else
        {
            return !preInfo.md5.Equals(curInfo.md5) ||
                    !preInfo.metaMd5.Equals(curInfo.metaMd5);
        }
    }

    public static bool IsBundleChanged(string bundleName)
    {
        if (PreBundleInfoMap == null || !PreBundleInfoMap.ContainsKey(bundleName))
            return true;
        var preBundleInfo = PreBundleInfoMap[bundleName];
        var curBundleInfo = CurBundleInfoMap[bundleName];
        if (preBundleInfo.refBundles.Count != curBundleInfo.refBundles.Count)
            return true;
        if (preBundleInfo.assets.Count != curBundleInfo.assets.Count)
            return true;
        foreach (string refBundle in preBundleInfo.refBundles)
        {
            if (!curBundleInfo.refBundles.Contains(refBundle))
                return true;
        }
        foreach (string asset in preBundleInfo.assets)
        {
            if (!curBundleInfo.assets.Contains(asset))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Texture
    /// AudioClip
    /// Mesh
    /// Model
    /// Shader
    /// 这些类型的Asset的一些配置式放在.meta中的，所以要监视它们的变化
    /// </summary>
    /// <param name="assetItem"></param>
    /// <returns></returns>
    public static bool IsMetaImportant(AssetItemInfo assetItem)
    {
        if (assetItem.asset is Texture || assetItem.asset is AudioClip ||
            assetItem.asset is Mesh || assetItem.asset is Shader)
        {
            return true;
        }

        AssetImporter importer = AssetImporter.GetAtPath(assetItem.filePath);
        return importer is ModelImporter;
    }

    public static bool CacheChangedAsset(AssetItemInfo asset)
    {
        if (IsAssetChanged(asset))
        {
            ChangedAssets.Add(asset);
            return true;
        }
        return false;
    }

    private static string AssetChangeLogPath
    {
        get
        {
            string path = string.Format("{0}/AssetChangeLog/assetChanges_{1}-{2}.txt",
                buildInfoPath, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), BuildSetting.Instance.Version);
            string directoryName = Path.GetDirectoryName(path);
            if(!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            return path;
        }
    }

    public static void SaveAssetChangeInfos()
    {
        string path = AssetChangeLogPath;
        if(File.Exists(path))
        {
            File.Delete(path);
        }
        StringBuilder sb = new StringBuilder();
        AssetBuildItem preInfo, curInfo;
        sb.AppendLine("<<<<<<Pre In Left, Cur In Right>>>>>>");
        sb.AppendFormat("Total Num:{0}\n\n", ChangedAssets.Count);
        for(int i=0; i < ChangedAssets.Count; i++)
        {
            var asset = ChangedAssets[i];
            preInfo = AssetBuildInfoUtils.GetAssetPreBuildInfo(asset);
            curInfo = AssetBuildInfoUtils.GetAssetCurBuildInfo(asset);

            sb.AppendFormat("filePath: {0}\n", asset.filePath);
            sb.AppendFormat("assetType: {0} | {1}\n", preInfo == null ? "Null" : preInfo.assetType, curInfo.assetType);
            sb.AppendFormat("bundleName: {0} | {1}\n", preInfo == null ? "Null" : preInfo.bundleName, curInfo.bundleName);
            sb.AppendFormat("Md5: {0} | {1}\n", preInfo == null ? "Null" : preInfo.md5, curInfo.md5);
            sb.AppendFormat("IsImportMeta: {0}\n", AssetBuildInfoUtils.IsMetaImportant(asset).ToString());
            sb.AppendFormat("MetaMd5: {0} | {1}\n", preInfo == null ? "Null" : preInfo.metaMd5, curInfo.metaMd5);
            sb.AppendLine("--------------------------------------------------------------\n");
        }
        File.WriteAllText(path, sb.ToString());
        sb.Clear();
        sb = null;
    }

    private static string BundleInfosLogPath
    {
        get
        {
            string path = string.Format("{0}/BundleInfosLog/bundleInfos_{1}-{2}.txt",
                buildInfoPath, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), BuildSetting.Instance.Version);
            string directoryName = Path.GetDirectoryName(path);
            if(!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            return path;
        }
    }

    public static void SaveAllBundleInfos()
    {
        string path = BundleInfosLogPath;
        if(File.Exists(path))
        {
            File.Delete(path);
        }
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<<<<<<All BundleInfos Dependencies and Include Assets>>>>>>");
        sb.AppendFormat("Total Num:{0}\n\n", CurBundleInfoMap.Count);
        foreach(var pair in CurBundleInfoMap)
        {
            var bundleInfo = pair.Value;
            sb.AppendFormat("bundleName: {0}\n", bundleInfo.bundleName);
            sb.AppendFormat("dependNum: {0}\n", bundleInfo.refBundles.Count.ToString());
            sb.AppendLine("{");
            foreach(string refBundle in bundleInfo.refBundles)
            {
                sb.AppendLine(refBundle);
            }
            sb.AppendLine("}");

            sb.AppendFormat("assetsNum: {0}\n", bundleInfo.assets.Count.ToString());
            sb.AppendLine("{");
            foreach(string asset in bundleInfo.assets)
            {
                sb.AppendLine(asset);
            }
            sb.AppendLine("}");
            sb.AppendLine("--------------------------------------------------------------\n");
        }
        File.WriteAllText(BundleInfosLogPath, sb.ToString());
        sb.Clear();
    }
}
