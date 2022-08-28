using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CreateAssetMenu]
public class BuildSetting : ScriptableObject
{
    public string GameVersion;
    public int PatchVersion;
    private string _version;
    public string Version
    {
        get
        {
            if(string.IsNullOrEmpty(_version))
            {
                _version = string.Format("{0}p{1}", GameVersion, PatchVersion);
            }
            return _version;
        }
    }

    public void Clear()
    {
        _version = string.Empty;
    }

    private static BuildSetting _instance;
    public static BuildSetting Instance
    {
        get
        {
            if(_instance == null)
            {
                _instance = AssetDatabase.LoadAssetAtPath<BuildSetting>("Assets/Editor/ConfigSettings/BuildSetting.asset");
            }
            return _instance;
        }
    }
}
