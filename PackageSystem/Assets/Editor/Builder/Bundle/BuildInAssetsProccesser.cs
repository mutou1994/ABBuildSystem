using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;


public class BuildInAssetsProccesser
{
    struct AssetInfo
    {
        public long fileId;
        public string guid;
        public int type;

        public string name;
        public UnityEngine.Object asset;

        public AssetInfo(long fileId, string guid, int type, string name, UnityEngine.Object asset)
        {
            this.fileId = fileId;
            this.guid = guid;
            this.type = type;

            this.name = name;
            this.asset = asset;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is AssetInfo)) return false;
            AssetInfo _obj = (AssetInfo)obj;
            return fileId == _obj.fileId && guid.Equals(_obj.guid);
        }

        public override int GetHashCode()
        {
            return string.Format("{0}{1}", fileId, guid).GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("{{fileID: {0}, guid: {1}, type: {2}}}", fileId.ToString(), guid, type.ToString());
        }
    }

    static string meshRelaPath = "Assets/BuildInAssets/Mesh/";
    static string matRelaPath = "Assets/BuildInAssets/Material/";
    static string texRelaPath = "Assets/BuildInAssets/Texture/";
    static string spriteRelaPath = "Assets/BuildInAssets/Image/";
    static string shaderRelaPath = "Assets/BuildInAssets/Shader/";

    static string buildInAssetsPath = Application.dataPath + "/BuildInAssets/";
    static string meshPath = buildInAssetsPath + "Mesh/";
    static string matPath = buildInAssetsPath + "Material/";
    static string texPath = buildInAssetsPath + "Texture/";
    static string spritePath = buildInAssetsPath + "Image/";
    static string shaderPath = buildInAssetsPath + "Shader/";

    static string buildInAssetsOutPath = Application.dataPath + "/../BuildInAssets/";

    static string buildInPath1 = "Resources/unity_builtin_extra";
    static string buildInPath2 = "Library/unity default resources";

    static string pattern = @"\{\s*?fileID:\s*?(\d+?)\s*?,\s*?guid:\s*?(\w+?),\s*?type:\s*?(\d+?)\s*?\}";

    static string buildInGuid1 = "0000000000000000f000000000000000";
    static string buildInGuid2 = "0000000000000000e000000000000000";

    static Dictionary<AssetInfo, AssetInfo> AssetInfoMap = new Dictionary<AssetInfo, AssetInfo>();

    static string shaderNamePattern = "Shader\\s*?\"(.+?)\"\\s*?{";
    static Dictionary<string, string> ShaderName2RelaPath = new Dictionary<string, string>();

    static Dictionary<string, List<AssetInfo[]>> BuildInReplacedSuccessAssets = new Dictionary<string, List<AssetInfo[]>>();
    static Dictionary<string, List<AssetInfo>> BuildInReplacedFailAssets = new Dictionary<string, List<AssetInfo>>();

    static Dictionary<string, List<AssetInfo[]>> BuildInRevertedAssets = new Dictionary<string, List<AssetInfo[]>>();

    static bool AssetInLocal = false;

    [MenuItem("BuildInAssets/CreateBuildInAssetsToEditor")]
    public static void CreateBuildInAssetsToEditor()
    {
        if (!Directory.Exists(meshPath))
            Directory.CreateDirectory(meshPath);
        if (!Directory.Exists(matPath))
            Directory.CreateDirectory(matPath);
        if (!Directory.Exists(texPath))
            Directory.CreateDirectory(texPath);
        if (!Directory.Exists(spritePath))
            Directory.CreateDirectory(spritePath);

        ClearFiles(meshPath);
        ClearFiles(matPath);
        ClearFiles(texPath);
        ClearFiles(spritePath);

        CreateBuildInAssetsByPath(buildInPath1);
        CreateBuildInAssetsByPath(buildInPath2);

        AssetDatabase.Refresh();
    }

    [MenuItem("BuildInAssets/CopyBuildInAssetsToEditor")]
    public static void CopyBuildInAssetsToEditor()
    {
        AssetInLocal = true;
        CopyAllFiles(buildInAssetsOutPath, buildInAssetsPath);
        AssetDatabase.Refresh();
    }

    [MenuItem("BuildInAssets/ClearLocalBuildInAssets")]
    public static void ClearLocalBuildInAssets()
    {
        Clear();
        AssetDatabase.Refresh();
    }

    public static void Clear()
    {
        AssetInLocal = false;
        ClearAllBuildInAssets();
        AssetInfoMap.Clear();
        ShaderName2RelaPath.Clear();
        BuildInReplacedSuccessAssets.Clear();
        BuildInReplacedFailAssets.Clear();
        BuildInRevertedAssets.Clear();
    }

    [MenuItem("BuildInAssets/ReplaceBuildInAssetsReference")]
    public static void ReplaceBuildInAssetsReference()
    {
        if(!AssetInLocal)
        {
            CopyBuildInAssetsToEditor();
        }
        var list = AssetBundleBuilder.GetWillPackageAssets();
        if(list != null)
        {
            ReplaceBuildInAssetsReference(list);
            SaveLogToFile();
            AssetDatabase.Refresh();
        }
    }

    public static void InitBuildIn2LocalAssetInfo()
    {
        if(!AssetInLocal)
        {
            CopyBuildInAssetsToEditor();
        }
        if(ShaderName2RelaPath.Count == 0)
        {
            GenerateShaderName2RelaPath();
        }
        if(AssetInfoMap.Count == 0)
        {
            InitBuildInAndLocalInfo();
        }
    }

    public static void ReplaceBuildInAssetsReference(List<UnityEngine.Object> list)
    {
        InitBuildIn2LocalAssetInfo();
        BuildInReplacedSuccessAssets.Clear();
        BuildInReplacedFailAssets.Clear();
        HashSet<string> checkedMap = new HashSet<string>();

        //先把提取出来的内置资源替换一遍 主要是去掉Mat对内置Shader和图片的引用
        foreach(var file in Directory.GetFiles(buildInAssetsPath, "*.*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".meta") || file.Contains(".svn") || file.EndsWith(".bat") || file.EndsWith(".ds_store")) continue;
            string filePath = file.Replace("\\", "/").Replace(Application.dataPath, "Assets");
            UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(filePath);
            ChangeBuildInAssetsReference(obj, checkedMap, BuildInReplacedSuccessAssets);
        }
        AssetDatabase.Refresh();

        int count = 0;
        int totalCount = list.Count;
        foreach(var obj in list)
        {
            EditorUtility.DisplayProgressBar("ReplaceBuildInAssets", string.Format("{0} Replace Start... {1:P2}", obj.name, (float)count / totalCount), (float)count / totalCount);
            ChangeBuildInAssetsReference(obj, checkedMap, BuildInReplacedSuccessAssets);
            count++;
            EditorUtility.DisplayProgressBar("ReplaceBuildInAssets", string.Format("{0} Replace Finished... {1:P2}", obj.name, (float)count / totalCount), (float)count / totalCount);
        }
        if(count > 0)
        {
            EditorUtility.ClearProgressBar();
        }
        AssetDatabase.Refresh();
    }

    [MenuItem("BuildInAssets/RevertBuildInAssetsReference")]
    public static void RevertBuildInAssetsReference()
    {
        if(!AssetInLocal)
        {
            CopyBuildInAssetsToEditor();
        }
        var list = AssetBundleBuilder.GetWillPackageAssets();
        if(list != null)
        {
            RevertBuildInAssetsReference(list);
            SaveLogToFile();
            Clear();
            AssetDatabase.Refresh();
        }
    }

    public static void RevertBuildInAssetsReference(List<UnityEngine.Object> list)
    {
        InitBuildIn2LocalAssetInfo();
        int count = 0;
        int totalCount = list.Count;
        BuildInRevertedAssets.Clear();
        HashSet<string> checkedMap = new HashSet<string>();
        //先把提取出来的内置资源替换一遍 主要是对Mat进行还原，这样还原的资源和前面替换的资源，打印出来的日志是对称的
        foreach(var file in Directory.GetFiles(buildInAssetsPath, "*.*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".meta") || file.Contains(".svn") || file.EndsWith(".bat") || file.EndsWith(".ds_store")) continue;
            string filePath = file.Replace("\\", "/").Replace(Application.dataPath, "Assets");
            UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(filePath);
            ChangeBuildInAssetsReference(obj, checkedMap, BuildInRevertedAssets);
        }
        AssetDatabase.Refresh();

        foreach(var obj in list)
        {
            EditorUtility.DisplayProgressBar("RevertBuildInAssets", string.Format("{0} Revert Start... {1:P2}", obj.name, (float)count / totalCount), (float)count / totalCount);
            ChangeBuildInAssetsReference(obj, checkedMap, BuildInRevertedAssets);
            count++;
            EditorUtility.DisplayProgressBar("RevertBuildInAssets", string.Format("{0} Revert Finished... {1:P2}", obj.name, (float)count / totalCount), (float)count / totalCount);
        }
        if(count > 0)
        {
            EditorUtility.ClearProgressBar();
        }
        AssetDatabase.Refresh();
    }

    public static void GenerateShaderName2RelaPath()
    {
        ShaderName2RelaPath.Clear();
        if(!Directory.Exists(shaderPath))
        {
            Debug.LogError("Directory Not Exits:" + shaderPath);
            return;
        }
        DirectoryInfo shaderDic = new DirectoryInfo(shaderPath);
        string[] shaderFiles = Directory.GetFiles(shaderPath, "*.*", SearchOption.AllDirectories);
        if(shaderFiles != null && shaderFiles.Length > 0)
        {
            foreach(var path in shaderFiles)
            {
                if (path.EndsWith(".meta"))
                    continue;
                string name = string.Empty;
                string filePath = path.Replace("\\", "/").Replace(Application.dataPath, "Assets");

                string content = File.ReadAllText(path);
                Match match = Regex.Match(content, shaderNamePattern);
                if(match != null && match.Success)
                {
                    name = match.Groups[1].Value;
                }
                else
                {
                    name = Path.GetFileNameWithoutExtension(path);
                }
                if(!ShaderName2RelaPath.ContainsKey(name))
                {
                    ShaderName2RelaPath.Add(name, filePath);
                }
                else
                {
                    Debug.LogError("Multi Shder When Gen Name2RelaPath:" + name + " " + ShaderName2RelaPath[name] + " And:" + filePath);
                }
            }
        }
    }

    static void ChangeBuildInAssetsReference(UnityEngine.Object obj, HashSet<string> checkedMap, Dictionary<string, List<AssetInfo[]>> changedSuccessAssets)
    {
        if (obj is Texture || obj is Texture2D || obj is Sprite || obj is Shader || obj is ComputeShader || obj is TextAsset || obj is Mesh || obj is AudioClip || obj is AnimationClip) return;
        string relaPath = AssetDatabase.GetAssetPath(obj).Replace("\\", "/");
        if (relaPath.EndsWith(".ttf") && !(obj is Font)) return;
        if (string.IsNullOrEmpty(relaPath)) return;
        if (relaPath.Equals(buildInPath1) || relaPath.Equals(buildInPath2)) return;
        if(!(relaPath.EndsWith(".prefab") || relaPath.EndsWith(".unity") || relaPath.EndsWith(".mat") || relaPath.EndsWith(".asset") || relaPath.EndsWith(".ttf"))) return;

        if (!checkedMap.Add(relaPath)) return;
        
        if(obj is Font)
        {
            Font font = obj as Font;
            long fileId;
            string guid;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(font.material.shader, out guid, out fileId);
            int type = (guid.Equals(buildInGuid1) || guid.Equals(buildInGuid2)) ? 0 : 3;
            string name = type == 0 ? font.material.shader.name : AssetDatabase.GetAssetPath(font.material.shader);
            AssetInfo originInfo = new AssetInfo(fileId, guid, type, name, font.material.shader);
            bool isReplace = changedSuccessAssets == BuildInReplacedSuccessAssets && (guid.Equals(buildInGuid1) || guid.Equals(buildInGuid2));
            bool isRevert = changedSuccessAssets == BuildInRevertedAssets && !(guid.Equals(buildInGuid1) || guid.Equals(buildInGuid2));
            if(isReplace || isRevert)
            {
                if(AssetInfoMap.ContainsKey(originInfo))
                {
                    AssetInfo replaceInfo = AssetInfoMap[originInfo];
                    originInfo = AssetInfoMap[replaceInfo];
                    font.material.shader = replaceInfo.asset as Shader;
                    if (!changedSuccessAssets.ContainsKey(relaPath))
                    {
                        changedSuccessAssets.Add(relaPath, new List<AssetInfo[]>());
                    }
                    changedSuccessAssets[relaPath].Add(new AssetInfo[] { originInfo, replaceInfo });
                }
                else if(isReplace)
                {
                    if(!BuildInReplacedFailAssets.ContainsKey(relaPath))
                    {
                        BuildInReplacedFailAssets.Add(relaPath, new List<AssetInfo>());
                    }
                    BuildInReplacedFailAssets[relaPath].Add(originInfo);
                }
            }
        }

        //去掉 Assets获得绝对路径
        string path = Application.dataPath + relaPath.Substring(6);
        string content = File.ReadAllText(path);
        HashSet<string> matchAlreadyReplaced = new HashSet<string>();
        foreach(Match match in Regex.Matches(content, pattern, RegexOptions.Multiline))
        {
            if(match.Success)
            {
                long fileId = long.Parse(match.Groups[1].Value);
                string guid = match.Groups[2].Value;
                int type = int.Parse(match.Groups[3].Value);
                if (!string.IsNullOrEmpty(guid) /*&& (guid.Equals(buildInGuid1) || guid.Equals(buildInGuid2))*/)
                {
                    bool isReplace = changedSuccessAssets == BuildInReplacedSuccessAssets && (guid.Equals(buildInGuid1) || guid.Equals(buildInGuid2));
                    bool isRevert = changedSuccessAssets == BuildInRevertedAssets && !(guid.Equals(buildInGuid1) || guid.Equals(buildInGuid2));
                    if (isReplace || isRevert)
                    {
                        if(!matchAlreadyReplaced.Contains(match.Groups[0].Value))
                        {
                            AssetInfo originInfo = new AssetInfo(fileId, guid, type, "", null);
                            if(AssetInfoMap.ContainsKey(originInfo))
                            {
                                matchAlreadyReplaced.Add(match.Groups[0].Value);
                                AssetInfo replaceInfo = AssetInfoMap[originInfo];
                                originInfo = AssetInfoMap[replaceInfo];
                                string replaceStr = replaceInfo.ToString();
                                content = content.Replace(match.Groups[0].Value, replaceStr);
                                if(!changedSuccessAssets.ContainsKey(relaPath))
                                {
                                    changedSuccessAssets.Add(relaPath, new List<AssetInfo[]>());
                                }
                                changedSuccessAssets[relaPath].Add(new AssetInfo[] { originInfo, replaceInfo });
                            }
                            else if(isReplace)
                            {
                                if(!BuildInReplacedFailAssets.ContainsKey(relaPath))
                                {
                                    BuildInReplacedFailAssets.Add(relaPath, new List<AssetInfo>());
                                }
                                BuildInReplacedFailAssets[relaPath].Add(originInfo);
                            }
                        }
                    }
                }
            }
        }
        if (matchAlreadyReplaced.Count > 0)
        {
            File.WriteAllText(path, content);
        }

        //递归处理依赖资源
        var pathSet = new HashSet<string>();
        var dps = EditorUtility.CollectDependencies(new UnityEngine.Object[] { obj });
        if(dps != null && dps.Length > 0)
        {
            foreach (UnityEngine.Object dpAsset in dps)
            {
                if (dpAsset is MonoScript || dpAsset is LightingDataAsset)
                    continue;
                if (dpAsset is Texture || dpAsset is Texture2D || dpAsset is Sprite || dpAsset is Shader || dpAsset is ComputeShader || dpAsset is TextAsset || dpAsset is Mesh || dpAsset is AudioClip || dpAsset is AnimationClip)
                    continue;
                string filePath = AssetDatabase.GetAssetPath(dpAsset).Replace("\\", "/");
                if (filePath.EndsWith(".ttf") && !(dpAsset is Font))
                    continue;
                if (string.IsNullOrEmpty(filePath))
                    continue;
                if (filePath.Equals(buildInPath1) || filePath.Equals(buildInPath2))
                    continue;
                if (!(filePath.EndsWith(".prefab") || filePath.EndsWith(".unity") || filePath.EndsWith(".mat") || filePath.EndsWith(".asset") || filePath.EndsWith(".ttf")))
                    continue;
                if (filePath.Equals(relaPath))
                    continue;
                //引用的资源有可能指向同一个资源，所以要判一下重
                if(pathSet.Add(filePath))
                {
                    ChangeBuildInAssetsReference(dpAsset, checkedMap, changedSuccessAssets);
                }
            }
        }
    }

    private static PropertyInfo inspectorMode = typeof(SerializedObject).GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
    public static long GetFileID(UnityEngine.Object target)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        inspectorMode.SetValue(serializedObject, InspectorMode.Debug, null);
        SerializedProperty localIdProp = serializedObject.FindProperty("m_LocalIdentfierInFile");
        return localIdProp.longValue;
    }

    static void InitBuildInAndLocalInfo()
    {
        AssetInfoMap.Clear();
        GenerateBuildInAndLocalInfo(buildInPath1);
        GenerateBuildInAndLocalInfo(buildInPath2);
    }

    static void GenerateBuildInAndLocalInfo(string path)
    {
        UnityEngine.Object[] UnityAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach(var asset in UnityAssets)
        {
            UnityEngine.Object localObj = null;
            int typeVal = 3;
            string localPath = string.Empty;
            Type type = null;
            if(asset is Mesh)
            {
                localPath = meshRelaPath + asset.name + ".mesh";
                type = typeof(Mesh);
            }
            else if(asset is Material)
            {
                typeVal = 2;
                localPath = matRelaPath + asset.name + ".mat";
                type = typeof(Material);
            }
            else if(asset is Sprite)
            {
                localPath = spriteRelaPath + asset.name + ".png";
                type = typeof(Sprite);
            }
            else if(asset is Texture)
            {
                if(asset is Texture2D)
                {
                    type = typeof(Texture2D);
                    if(File.Exists(texRelaPath + asset.name + ".png"))
                    {
                        localPath = texRelaPath + asset.name + ".png";
                    }
                    else
                    {
                        localPath = spriteRelaPath + asset.name + ".png";
                    }
                }
                else
                {
                    localPath = texRelaPath + asset.name + ".png";
                    type = typeof(Texture);
                }
            }
            else if(asset is Shader)
            {
                type = typeof(Shader);
                if(ShaderName2RelaPath.ContainsKey(asset.name))
                {
                    localPath = ShaderName2RelaPath[asset.name];
                }
                else
                {
                    Debug.LogError("ShaderName2RelaPath Not Exits!!! name: " + asset.name);
                }
            }
            else if(asset is ComputeShader)
            {
                type = typeof(ComputeShader);
                if(ShaderName2RelaPath.ContainsKey(asset.name))
                {
                    localPath = ShaderName2RelaPath[asset.name];
                }
                else
                {
                    Debug.LogError("ShaderName2RelaPath Not Exits!!! name: " + asset.name);
                }
            }
            else if(!(asset is MonoScript))
            {
                Debug.LogError("Other Res:" + asset.name + " " + asset.GetType());
            }

            if(!string.IsNullOrEmpty(localPath))
            {
                localObj = AssetDatabase.LoadAssetAtPath(localPath, type);
            }
            if(localObj != null)
            {
                long fileId, buildInFileId;
                string guid, buildInGuId;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(localObj, out guid, out fileId);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out buildInGuId, out buildInFileId);

                AssetInfo localInfo = new AssetInfo(fileId, guid, typeVal, localPath, localObj);
                AssetInfo buildInfo = new AssetInfo(buildInFileId, buildInGuId, 0, asset.name, asset);
                if(!AssetInfoMap.ContainsKey(buildInfo))
                {
                    AssetInfoMap.Add(localInfo, buildInfo);
                    AssetInfoMap.Add(buildInfo, localInfo);
                }
            }
            else if(!string.IsNullOrEmpty(localPath))
            {
                Debug.LogError("LoadAssetFailed When GenerateBuildInAndLocalInfo, localPath:" + localPath + " assetName:" + asset.name + " type:" + type.ToString());
            }
        }
    }

    static void CopyAllFiles(string sourcePath, string destPath)
    {
        if (!Directory.Exists(sourcePath)) return;
        if(Directory.Exists(destPath))
        {
            Directory.Delete(destPath, true);
        }
        Directory.CreateDirectory(destPath);
        DirectoryInfo source = new DirectoryInfo(sourcePath);
        sourcePath = source.FullName.Replace("\\", "/");
        foreach (var file in source.GetFiles("*.*", SearchOption.AllDirectories))
        {
            string destFile = file.FullName.Replace("\\", "/").Replace(sourcePath, destPath);
            string destDirPath = Path.GetDirectoryName(destFile);
            if(!Directory.Exists(destDirPath))
            {
                Directory.CreateDirectory(destDirPath);
            }
            if(File.Exists(destFile))
            {
                File.Delete(destFile);
            }
            file.CopyTo(destFile);
        }
    }

    static void ClearFiles(string path)
    {
        foreach(var filePath in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
        {
            if(!filePath.EndsWith(".meta"))
            {
                File.Delete(filePath);
            }
        }
    }

    static void ClearAllBuildInAssets()
    {
        if (Directory.Exists(meshPath))
            Directory.Delete(meshPath, true);
        if (Directory.Exists(matPath))
            Directory.Delete(matPath, true);
        if (Directory.Exists(texPath))
            Directory.Delete(texPath, true);
        if (Directory.Exists(spritePath))
            Directory.Delete(spritePath, true);
        if (Directory.Exists(shaderPath))
            Directory.Delete(shaderPath, true);

        Directory.CreateDirectory(meshPath);
        Directory.CreateDirectory(matPath);
        Directory.CreateDirectory(texPath);
        Directory.CreateDirectory(spritePath);
        Directory.CreateDirectory(shaderPath);
    }

    static void CreateBuildInAssetsByPath(string path)
    {
        UnityEngine.Object[] UnityAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        Dictionary<UnityEngine.Object, bool> spriteMap = new Dictionary<UnityEngine.Object, bool>();
        foreach(var asset in UnityAssets)
        {
            if(asset is Sprite)
            {
                Sprite sp = asset as Sprite;
                Texture tex = sp.texture;
                if(!spriteMap.ContainsKey(tex))
                {
                    spriteMap.Add(tex, true);
                }
                RenderTexture tmp = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
                Graphics.Blit(tex, tmp);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = tmp;
                Rect rect = sp.rect;
                Texture2D _tex = new Texture2D(Mathf.CeilToInt(rect.width), Mathf.CeilToInt(rect.height));
                _tex.ReadPixels(rect, 0, 0);
                _tex.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(tmp);
                byte[] buffer = _tex.EncodeToPNG();
                File.WriteAllBytes(spritePath + sp.name + ".png", buffer);
            }
        }
        foreach(var asset in UnityAssets)
        {
            if(asset is Mesh)
            {
                var obj = UnityEngine.Object.Instantiate(asset);
                AssetDatabase.CreateAsset(obj, meshRelaPath + asset.name + ".mesh");
            }
            else if(asset is Material)
            {
                var obj = UnityEngine.Object.Instantiate(asset);
                AssetDatabase.CreateAsset(obj, matRelaPath + asset.name + ".mat");
            }
            else if(asset is Texture)
            {
                if(!spriteMap.ContainsKey(asset))
                {
                    Texture tex = asset as Texture;
                    RenderTexture tmp = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
                    Graphics.Blit(tex, tmp);
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = tmp;
                    Texture2D _tex = new Texture2D(tex.width, tex.height);
                    _tex.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
                    _tex.Apply();
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(tmp);
                    byte[] buffer = _tex.EncodeToPNG();
                    File.WriteAllBytes(texPath + tex.name + ".png", buffer);
                }
            }
            else if(!(asset is Sprite || asset is Shader || asset is ComputeShader || asset is MonoScript))
            {
                Debug.LogError("Other Res:" + asset.name + " " + asset.GetType());
            }
        }
    }

    static string BuildInAssetsReplaceLogPath
    {
        get
        {
            string path = string.Format("{0}/BuildInAssetsReplaceLog/buildInAssetsReplaced_{1}-{2}.txt", AssetBuildInfoUtils.BuildInfoPath, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss"), BuildSetting.Instance.Version);
            string directoryName = Path.GetDirectoryName(path);
            if(!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            return path;
        }
    }

    public static void SaveLogToFile()
    {
        string path = BuildInAssetsReplaceLogPath;
        if(File.Exists(path))
        {
            File.Delete(path);
        }
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<<<<<<<<<<BuildInAssetts Replaced Fail>>>>>>>>>>");
        sb.AppendFormat("Total Num:{0}\n\n", BuildInReplacedFailAssets.Count);
        foreach(var pair in BuildInReplacedFailAssets)
        {
            string filePath = pair.Key;
            var list = pair.Value;
            sb.AppendLine(filePath);
            sb.AppendLine("[");
            for(int i=0; i < list.Count; i++)
            {
                var assetInfo = list[i];
                if(assetInfo.asset != null)
                {
                    sb.AppendLine(assetInfo.asset.ToString());
                }
                if(!string.IsNullOrEmpty(assetInfo.name))
                {
                    sb.AppendLine(assetInfo.name);
                }
                sb.AppendLine("    " + assetInfo.ToString());
                if(i < list.Count - 1)
                {
                    sb.AppendLine();
                }
            }
            sb.AppendLine("]");
            sb.AppendLine();
        }
        sb.AppendLine("--------------------------------------------------------------------");

        sb.AppendLine("<<<<<<<<<<BuildInAssetts Replaced Success>>>>>>>>>>");
        sb.AppendFormat("Total Num:{0}\n\n", BuildInReplacedSuccessAssets.Count);
        foreach(var pair in BuildInReplacedSuccessAssets)
        {
            string filePath = pair.Key;
            var list = pair.Value;
            sb.AppendLine(filePath);
            sb.AppendLine("[");
            for (int i=0; i < list.Count; i++)
            {
                var originInfo = list[i][0];
                var replaceInfo = list[i][1];
                sb.AppendLine("    " + originInfo.asset + "  ===>>>  " + replaceInfo.asset);
                sb.AppendLine("    " + originInfo.name + "  ===>>>  " + replaceInfo.name);
                sb.AppendLine("    " + originInfo.ToString() + "  ===>>>  " + replaceInfo.ToString());
                if(i < list.Count - 1)
                {
                    sb.AppendLine();
                }
            }
            sb.AppendLine("]");
            sb.AppendLine();
        }
        sb.AppendLine("--------------------------------------------------------------------");

        sb.AppendLine("<<<<<<<<<<BuildInAssetts Reverted Success>>>>>>>>>>");
        sb.AppendFormat("Total Num:{0}\n\n", BuildInRevertedAssets.Count);
        foreach(var pair in BuildInRevertedAssets)
        {
            string filePatrh = pair.Key;
            var list = pair.Value;
            sb.AppendLine(filePatrh);
            sb.AppendLine("[");
            for(int i=0; i < list.Count; i++)
            {
                var originInfo = list[i][0];
                var replaceInfo = list[i][1];
                sb.AppendLine("    " + originInfo.asset + "  ===>>>  " + replaceInfo.asset);
                sb.AppendLine("    " + originInfo.name + "  ===>>>  " + replaceInfo.name);
                sb.AppendLine("    " + originInfo.ToString() + "  ===>>>  " + replaceInfo.ToString());
                if (i < list.Count - 1)
                {
                    sb.AppendLine();
                }
            }
            sb.AppendLine("]");
            sb.AppendLine();
        }
        sb.AppendLine("--------------------------------------------------------------------");

        File.WriteAllText(path, sb.ToString());
        sb.Clear();
        sb = null;
    }
}
