#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildWeatherDrivenSolarPanelShaders
{
    private const string BundleName = "weatherdrivensolarpanel";
    private const string ShaderAsset = "Assets/Shaders/DustOverlay.shader";

    [MenuItem("Weather Driven Solar Panel/Build Shader Bundle")]
    public static void Build()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        string outputDirectory = Path.Combine(
            repoRoot,
            "GameData",
            "WeatherDrivenSolarPanel",
            "Shaders");
        Directory.CreateDirectory(outputDirectory);

        AssetImporter importer = AssetImporter.GetAtPath(ShaderAsset);
        if (importer == null)
        {
            throw new FileNotFoundException("Missing shader asset", ShaderAsset);
        }

        importer.assetBundleName = BundleName;
        importer.SaveAndReimport();

        AssetBundleBuild[] builds =
        {
            new AssetBundleBuild
            {
                assetBundleName = BundleName + ".bundle",
                assetNames = new[] { ShaderAsset }
            }
        };

        BuildPipeline.BuildAssetBundles(
            outputDirectory,
            builds,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64);

        Debug.Log("Built WDSP shader bundle -> " + outputDirectory);
    }
}
#endif
