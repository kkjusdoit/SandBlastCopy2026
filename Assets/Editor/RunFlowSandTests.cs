using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class RunFlowSandTests
{
    private static TestRunnerApi runner;

    [MenuItem("Flow Sand/Run EditMode Tests")]
    public static void Run()
    {
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new ResultCallback());
        runner.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
        }));
    }

    private sealed class ResultCallback : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log($"[FlowSand Tests] Running {testsToRun.TestCaseCount} tests");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            Debug.Log($"[FlowSand Tests] Passed={result.PassCount} Failed={result.FailCount} Skipped={result.SkipCount}");
            runner = null;
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.TestStatus == TestStatus.Failed)
            {
                Debug.LogError($"[FlowSand Tests] {result.FullName}\n{result.Message}\n{result.StackTrace}");
            }
        }
    }
}
