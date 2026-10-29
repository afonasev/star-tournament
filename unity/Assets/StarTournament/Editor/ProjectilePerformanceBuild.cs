using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
namespace StarTournament.ProvingGround.Editor
{
    // Minimal isolated Development Player for authorized projectile A/B; derived from
    // ModelPerformanceBuild @6951a008. No baseline assets or release/install step.
    public static class ProjectilePerformanceBuild
    {
        public static void Build()
        {
            string output=Environment.GetEnvironmentVariable("STAR_PROJECTILE_BUILD");
            if(!Path.IsPathFullyQualified(output??""))throw new BuildFailedException("Absolute owned build path required");
            ProvingGroundBuild.Prepare();LabReleaseBuild.Validate();
            string company=PlayerSettings.companyName,product=PlayerSettings.productName,id=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);bool timing=PlayerSettings.enableFrameTimingStats;
            try {
                PlayerSettings.companyName="StarTournament QA";PlayerSettings.productName=Environment.GetEnvironmentVariable("STAR_PROJECTILE_PRODUCT")??"Projectile Performance QA";PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,Environment.GetEnvironmentVariable("STAR_PROJECTILE_APP_ID")??"tech.afonasev.projectile-performance-20261029");PlayerSettings.enableFrameTimingStats=true;
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/StarTournament/Scenes/ProvingGround.unity"},locationPathName=output,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development,extraScriptingDefines=new[]{"STAR_TOURNAMENT_DEVELOPMENT_QA"}});
                if(result.summary.result!=BuildResult.Succeeded)throw new BuildFailedException(result.summary.result.ToString());
                UnityEngine.Debug.Log("PROJECTILE_PERFORMANCE_BUILD_PASS "+result.summary.totalSize);
            } finally {PlayerSettings.companyName=company;PlayerSettings.productName=product;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,id);PlayerSettings.enableFrameTimingStats=timing;AssetDatabase.SaveAssets();}
        }
    }
}
