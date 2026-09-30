using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    /// <summary>Opt-in progress receipt for a bounded batchmode test run.</summary>
    [InitializeOnLoad]
    internal static class ScoreboardTestProgress
    {
        static readonly TestRunnerApi api;
        static readonly ProgressCallbacks callbacks;

        static ScoreboardTestProgress()
        {
            var args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-scoreboardTestProgress");
            if (flag < 0) return;
            if (flag + 1 >= args.Length || !Path.IsPathFullyQualified(args[flag + 1]))
                throw new ArgumentException("Absolute scoreboard test progress path required");
            var path = args[flag + 1];
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            callbacks = new ProgressCallbacks(path);
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(callbacks);
        }

        sealed class ProgressCallbacks : ICallbacks
        {
            readonly string path;
            public ProgressCallbacks(string path) { this.path = path; }
            void Write(string stage, string name)
            {
                File.AppendAllText(path, DateTime.UtcNow.ToString("O") + "\t" + stage + "\t" + name + "\n");
            }
            public void RunStarted(ITestAdaptor tree) => Write("run-start", tree.TestCaseCount.ToString());
            public void RunFinished(ITestResultAdaptor result) => Write("run-end", result.ResultState);
            public void TestStarted(ITestAdaptor test)
            {
                if (!test.IsSuite) Write("start", test.FullName);
            }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite) Write("finish:" + result.ResultState, result.FullName);
            }
        }
    }
}
