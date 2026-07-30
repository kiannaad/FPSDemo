using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using YooAsset;

/// <summary>
/// 核心资源系统逻辑类（负责初始化、预加载等繁重工作）
/// </summary>
public class ResourceManager : Singleton<ResourceManager>
{
    private ResourcePackage _defaultPackage;
    public bool IsReady { get; private set; }

    /// <summary>
    /// 初始化 YooAsset
    /// </summary>
    public async Task InitializeAsync(string packageName = "DefaultPackage")
    {
        IsReady = false;

        // 1. 初始化系统
        YooAssets.Initialize();

        // 2. 创建并设置默认包裹
        _defaultPackage = YooAssets.CreatePackage(packageName);
        YooAssets.SetDefaultPackage(_defaultPackage);

        // 3. 根据环境确定运行模式
        InitializeParameters parameters = null;
#if UNITY_EDITOR
        PackageInvokeBuildResult buildResult = EditorSimulateModeHelper.SimulateBuild(packageName);
        if (buildResult == null || string.IsNullOrWhiteSpace(buildResult.PackageRootDirectory))
        {
            throw new InvalidOperationException($"Failed to build the YooAsset editor simulation manifest for {packageName}.");
        }

        var editorParam = new EditorSimulateModeParameters();
        editorParam.EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
        parameters = editorParam;
#else
        // 默认离线模式，联机模式可根据需求在此扩展逻辑
        var offlineParam = new OfflinePlayModeParameters();
        offlineParam.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
        parameters = offlineParam;
#endif

        // 4. 执行初始化操作
        var operation = _defaultPackage.InitializeAsync(parameters);
        await operation.Task;

        if (operation.Status != EOperationStatus.Succeed)
        {
            Debug.LogError($"ResourceManager: {packageName} initialization failed: {operation.Error}");
            return;
        }

        RequestPackageVersionOperation versionOperation = _defaultPackage.RequestPackageVersionAsync();
        await versionOperation.Task;
        if (versionOperation.Status != EOperationStatus.Succeed)
        {
            Debug.LogError($"ResourceManager: {packageName} version request failed: {versionOperation.Error}");
            return;
        }

        UpdatePackageManifestOperation manifestOperation = _defaultPackage.UpdatePackageManifestAsync(versionOperation.PackageVersion);
        await manifestOperation.Task;
        if (manifestOperation.Status != EOperationStatus.Succeed)
        {
            Debug.LogError($"ResourceManager: {packageName} manifest update failed: {manifestOperation.Error}");
            return;
        }

        IsReady = true;
        Debug.Log($"<color=green>ResourceManager: {packageName} initialized successfully!</color>");
    }

    /// <summary>
    /// 获取当前的默认资源包裹
    /// </summary>
    public ResourcePackage GetPackage()
    {
        if (_defaultPackage == null)
            throw new Exception("ResourceManager has not been initialized. Please call InitializeAsync first.");
        return _defaultPackage;
    }

    /// <summary>
    /// 预加载资源列表
    /// </summary>
    public async Task PreloadAssetsAsync(IEnumerable<string> locations)
    {
        List<AssetHandle> handles = new List<AssetHandle>();
        foreach (var location in locations)
        {
            handles.Add(_defaultPackage.LoadAssetAsync<UnityEngine.Object>(location));
        }

        foreach (var handle in handles)
        {
            await handle.Task;
        }
        Debug.Log("ResourceManager: All assets preloaded.");
    }
}
