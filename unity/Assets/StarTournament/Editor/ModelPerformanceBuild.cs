using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace StarTournament.ProvingGround.Editor
{
    // Builds only an explicitly requested local diagnostic. Generated baseline is never a shipping asset.
    public static class ModelPerformanceBuild
    {
        public static void Build()
        {
            string source=Environment.GetEnvironmentVariable("STAR_MODEL_BASELINE"),output=Environment.GetEnvironmentVariable("STAR_MODEL_BUILD");
            if(!Path.IsPathFullyQualified(source??"")||!Path.IsPathFullyQualified(output??""))throw new BuildFailedException("Absolute owned baseline/build paths required");
            const string legacy="Assets/StarTournament/Art/model-performance-baseline.glb";
            File.Copy(source,legacy,true);AssetDatabase.ImportAsset(legacy,ImportAssetOptions.ForceSynchronousImport);
            ProvingGroundBuild.Prepare();
            var ground=UnityEngine.Object.FindFirstObjectByType<ProvingGround>();
            TrooperPreparation.Prepare(ground,AssetDatabase.LoadAssetAtPath<GameObject>(legacy),AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Art/trooper-arms-animated.glb"));
            const string prefab="Assets/Resources/ModelPerformanceBaseline.prefab";
            AssetDatabase.DeleteAsset(prefab);
            if(!AssetDatabase.CopyAsset("Assets/StarTournament/Trooper/TrooperBody.prefab",prefab))throw new BuildFailedException("Baseline prefab copy failed");
            ProvingGroundBuild.Prepare();LabReleaseBuild.Validate();
            string company=PlayerSettings.companyName,product=PlayerSettings.productName,id=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);
            bool timing=PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.companyName="StarTournament QA";PlayerSettings.productName="F23 Model Performance QA 20261029";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,"tech.afonasev.f23-model-performance-20261029");PlayerSettings.enableFrameTimingStats=true;
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/StarTournament/Scenes/ProvingGround.unity"},locationPathName=output,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development,extraScriptingDefines=new[]{"STAR_TOURNAMENT_DEVELOPMENT_QA"}});
                if(result.summary.result!=BuildResult.Succeeded)throw new BuildFailedException(result.summary.result.ToString());
                Debug.Log("MODEL_PERFORMANCE_BUILD_PASS "+result.summary.totalSize);
            }
            finally
            {PlayerSettings.companyName=company;PlayerSettings.productName=product;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,id);PlayerSettings.enableFrameTimingStats=timing;AssetDatabase.SaveAssets();}
        }
    }
}
