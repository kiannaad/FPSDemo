using NugetForUnity;
using NugetForUnity.Models;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class MessagePackDependencyInstaller
{
    // Recompiling after NuGetForUnity finishes its first import triggers this one-time dependency check.
    private const string PackageId = "MessagePack";
    private const string PackageVersion = "3.1.8";

    static MessagePackDependencyInstaller()
    {
        EditorApplication.delayCall += InstallMessagePack;
    }

    private static void InstallMessagePack()
    {
        bool installed = NugetPackageInstaller.InstallIdentifier(
            new NugetPackageIdentifier(PackageId, PackageVersion));
        if (!installed)
        {
            Debug.LogError($"[Network] Unable to install {PackageId} {PackageVersion}.");
        }
    }
}
