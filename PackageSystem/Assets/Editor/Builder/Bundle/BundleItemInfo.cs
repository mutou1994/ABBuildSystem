using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class BundleItemInfo
{
    public string bundleName;

    /// <summary>
    /// 依赖我的AB
    /// </summary>
    private HashSet<BundleItemInfo> RefParentSet = new HashSet<BundleItemInfo>();

    /// <summary>
    /// 我依赖的AB
    /// </summary>
    private HashSet<BundleItemInfo> RefChildSet = new HashSet<BundleItemInfo>();

    public BundleItemInfo(string bundleName)
    {
        this.bundleName = bundleName;
    }

    public int RefCount
    {
        get
        {
            return RefChildSet.Count;
        }
    }

    public BundleItemInfo[] GetRefChilds()
    {
        return RefChildSet.ToArray();
    }

    /// <summary>
    /// 添加依赖我的项
    /// </summary>
    /// <param name="item"></param>
    public void AddRefParentAsset(BundleItemInfo item)
    {
        if (!RefParentSet.Contains(item))
        {
            RefParentSet.Add(item);
        }
    }

    /// <summary>
    /// 添加我依赖的项
    /// </summary>
    /// <param name="item"></param>
    public void AddRefChildAsset(BundleItemInfo item)
    {
        //若已经是我的直接依赖或间接依赖则无需再添加
        if (!AlreadyRefed(item))
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
    public void ClearChildRefRecursive(BundleItemInfo item)
    {
        List<BundleItemInfo> removeList = new List<BundleItemInfo>();
        foreach(var child in RefChildSet)
        {
            if(item.AlreadyRefed(child))
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
    /// 是否已经直接或间接依赖
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool AlreadyRefed(BundleItemInfo item)
    {
        //直接依赖
        if (RefChildSet.Contains(item)) return true;

        //间接依赖
        foreach(var child in RefChildSet)
        {
            if (child.AlreadyRefed(item))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 我依赖了这个项，那么直接或间接依赖我的项就都不需要直接依赖这个项了
    /// </summary>
    /// <param name="item"></param>
    public void ClearParentRefRecursive(BundleItemInfo item)
    {
        foreach(var parent in RefParentSet)
        {
            if(parent.RefChildSet.Contains(item))
            {
                parent.RemoveRefChild(item);
            }
            parent.ClearChildRefRecursive(item);
        }
    }

    /// <summary>
    /// 移除我直接依赖的项
    /// </summary>
    /// <param name="item"></param>
    public void RemoveRefChild(BundleItemInfo item)
    {
        if(this.RefChildSet.Contains(item))
        {
            this.RefChildSet.Remove(item);
        }

        //同时移除目标中的被依赖集合
        if(item.RefParentSet.Contains(this))
        {
            item.RefParentSet.Remove(this);
        }
    }
}
