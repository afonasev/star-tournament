using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    // Opt-in progress evidence for identifying a stalled batch test without altering it.
    [InitializeOnLoad] public static class NativeTestProgress
    {
        static readonly TestRunnerApi api;
        static NativeTestProgress()
        {
            var path=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_TEST_PROGRESS");
            if(string.IsNullOrEmpty(path))return;
            api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Progress(path));
        }
        sealed class Progress:ICallbacks
        {
            readonly string path;public Progress(string path){this.path=path;}
            void Write(string stage,string name)=>File.AppendAllText(path,DateTime.UtcNow.ToString("O")+" "+stage+" "+name+"\n");
            public void RunStarted(ITestAdaptor test)=>Write("RUN",test.FullName);
            public void RunFinished(ITestResultAdaptor result)=>Write("DONE "+result.ResultState,result.FullName);
            public void TestStarted(ITestAdaptor test){if(!test.IsSuite)Write("START",test.FullName);}
            public void TestFinished(ITestResultAdaptor result){if(!result.Test.IsSuite)Write("END "+result.ResultState,result.FullName);}
        }
    }
}
