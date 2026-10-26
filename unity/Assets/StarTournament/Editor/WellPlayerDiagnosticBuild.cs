using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace StarTournament.ProvingGround.Editor
{
    public static class WellPlayerDiagnosticBuild
    {
        // Minimal Development QA build uses the existing diagnostic entry points.
        // Dedicated identity isolates PlayerPrefs and history from the human's game.
        public static void Build() => BuildDiagnostic("Well Player Diagnostic","tech.afonasev.well-player-diagnostic");
        public static void BuildRenderingBaseline() => BuildDiagnostic("Rendering Baseline QA 20261025","tech.afonasev.rendering-baseline-20261025");
        public static void BuildRenderingFinal() => BuildDiagnostic("Rendering Final QA 20261026","tech.afonasev.rendering-final-20261026");
        public static void BuildWellFrameProfile() => BuildDiagnostic("Well Frame QA 20261026","tech.afonasev.well-frame-20261026");
        static void BuildDiagnostic(string productName,string applicationId)
        {
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WELL_PLAYER_OUTPUT");
            if(string.IsNullOrEmpty(output)||!System.IO.Path.IsPathFullyQualified(output))throw new BuildFailedException("Absolute diagnostic output required");
            ProvingGroundBuild.Prepare();LabReleaseBuild.Validate();
            string company=PlayerSettings.companyName,product=PlayerSettings.productName;
            string identifier=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);
            try
            {
                PlayerSettings.companyName="StarTournament QA";PlayerSettings.productName=productName;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,applicationId);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/StarTournament/Scenes/ProvingGround.unity"},locationPathName=output,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development,extraScriptingDefines=new[]{"STAR_TOURNAMENT_DEVELOPMENT_QA"}});
                if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException(report.summary.result.ToString());
                Debug.Log("WELL_PLAYER_DIAGNOSTIC_BUILD_PASS "+report.summary.totalSize);
            }
            finally
            {PlayerSettings.companyName=company;PlayerSettings.productName=product;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,identifier);AssetDatabase.SaveAssets();}
        }
    }
}
