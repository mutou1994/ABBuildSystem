using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public class PatchItem
{
    public uint o;
    public uint s;
    public string p;

    public PatchItem(string path, uint offset, uint size)
    {
        this.p = path;
        this.o = offset;
        this.s = size;
    }
}

[Serializable]
public class PatchPkgItem 
{
    public uint o;
    public uint s;
    public string p;
    public List<PatchItem> patchs;

    public PatchPkgItem(IEnumerable<PatchItem> collection, uint offset, uint size, string path)
    {
        patchs = new List<PatchItem>(collection);
        this.o = offset;
        this.s = size;
        this.p = path;
    }
}

[Serializable]
public class PatchInfos
{
    public string allPublishVersion;
    public int number;
    public uint size;
    public List<PatchPkgItem> packages = new List<PatchPkgItem>();

    public int Count
    {
        get
        {
            return packages.Count;
        }
    }
    
    public void AddPackageList(IEnumerable<PatchItem> collection, uint fileSize, string path)
    {
        packages.Add(new PatchPkgItem(collection, this.size, fileSize, path));
        this.size += fileSize;
        this.number = packages.Count;
    }
    
    public void Clear()
    {
        allPublishVersion = string.Empty;
        number = 0;
        size = 0;
        packages.Clear();
    }
}
