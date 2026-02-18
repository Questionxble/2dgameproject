using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Tests.Editor
{
    public static class CodeCoverageTestRunner
    {
        [MenuItem("Tools/Run All Tests with Coverage")]
        public static void RunAllTestsWithCoverage()
        {
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            
            var editModeFilter = new Filter()
            {
                testMode = TestMode.EditMode
            };
            
            var playModeFilter = new Filter()
            {
                testMode = TestMode.PlayMode
            };
            
            Debug.Log("Starting Code Coverage Test Run...");
            
            // Run EditMode tests first
            testRunnerApi.Execute(new ExecutionSettings(editModeFilter));
            
            // Run PlayMode tests
            testRunnerApi.Execute(new ExecutionSettings(playModeFilter));
        }
        
        [MenuItem("Tools/Generate Coverage Report")]
        public static void GenerateCoverageReport()
        {
            Debug.Log("Generating Code Coverage Report...");
            // The coverage report will be generated automatically when tests run with code coverage enabled
        }
    }
}