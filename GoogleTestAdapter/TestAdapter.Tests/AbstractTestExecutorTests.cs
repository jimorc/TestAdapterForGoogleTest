﻿using System;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using GoogleTestAdapter.Helpers;
using GoogleTestAdapter.Runners;
using GoogleTestAdapter.Model;
using GoogleTestAdapter.Settings;
using GoogleTestAdapter.TestAdapter.Framework;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;
using VsTestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;
using VsTestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;
using static GoogleTestAdapter.TestMetadata.TestCategories;

namespace GoogleTestAdapter.TestAdapter
{
    public abstract class AbstractTestExecutorTests : AbstractTestAdapterTests
    {
        private readonly bool _parallelTestExecution;

        private readonly int _maxNrOfThreads;


        protected AbstractTestExecutorTests(bool parallelTestExecution, int maxNrOfThreads)
        {
            _parallelTestExecution = parallelTestExecution;
            _maxNrOfThreads = maxNrOfThreads;
        }


        protected virtual void CheckMockInvocations(int nrOfPassedTests, int nrOfFailedTests, int nrOfUnexecutedTests, int nrOfNotFoundTests)
        {
            MockFrameworkHandle.Verify(h => h.RecordResult(It.Is<VsTestResult>(tr => tr.Outcome == VsTestOutcome.None)),
                Times.Exactly(nrOfUnexecutedTests));
            MockFrameworkHandle.Verify(h => h.RecordEnd(It.IsAny<VsTestCase>(), It.Is<VsTestOutcome>(to => to == VsTestOutcome.None)),
                Times.Exactly(nrOfUnexecutedTests));
        }

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();

            MockOptions.Setup(o => o.ParallelTestExecution).Returns(_parallelTestExecution);
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(_maxNrOfThreads);
        }

        private void RunAndVerifySingleTest(TestCase testCase, VsTestOutcome expectedOutcome)
        {
            TestExecutor executor = new TestExecutor(TestEnvironment);
            executor.RunTests(testCase.ToVsTestCase().Yield(), MockRunContext.Object, MockFrameworkHandle.Object);

            foreach (VsTestOutcome outcome in Enum.GetValues(typeof(VsTestOutcome)))
            {
                MockFrameworkHandle.Verify(h => h.RecordEnd(It.IsAny<VsTestCase>(), It.Is<VsTestOutcome>(to => to == outcome)),
                    Times.Exactly(outcome == expectedOutcome ? 1 : 0));
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_TestDirectoryViaUserParams_IsPassedViaCommandLineArg()
        {
            TestCase testCase = TestDataCreator.GetTestCasesOfSampleTests("CommandArgs.TestDirectoryIsSet").First();

            RunAndVerifySingleTest(testCase, VsTestOutcome.Failed);

            MockFrameworkHandle.Reset();
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("-testdirectory=\"" + SettingsWrapper.TestDirPlaceholder + "\"");

            RunAndVerifySingleTest(testCase, VsTestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WorkingDir_IsSetCorrectly()
        {
            TestCase testCase = TestDataCreator.GetTestCasesOfSampleTests("WorkingDir.IsSolutionDirectory").First();

            MockOptions.Setup(o => o.WorkingDir).Returns(SettingsWrapper.ExecutableDirPlaceholder);
            RunAndVerifySingleTest(testCase, VsTestOutcome.Failed);

            MockFrameworkHandle.Reset();
            MockOptions.Setup(o => o.WorkingDir).Returns(SettingsWrapper.SolutionDirPlaceholder);
            RunAndVerifySingleTest(testCase, VsTestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_ExternallyLinkedX86Tests_CorrectTestResults()
        {
            RunAndVerifyTests(TestResources.X86ExternallyLinkedTests, 2, 0, 0);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_ExternallyLinkedX86TestsInDebugMode_CorrectTestResults()
        {
            // for at least having the debug messaging code executed once
            MockOptions.Setup(o => o.DebugMode).Returns(true);

            RunAndVerifyTests(TestResources.X86ExternallyLinkedTests, 2, 0, 0);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_StaticallyLinkedX86Tests_CorrectTestResults()
        {
            // let's print the test output
            MockOptions.Setup(o => o.PrintTestOutput).Returns(true);

            RunAndVerifyTests(TestResources.X86StaticallyLinkedTests, 1, 1, 0);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_ExternallyLinkedX64_CorrectTestResults()
        {
            RunAndVerifyTests(TestResources.X64ExternallyLinkedTests, 2, 0, 0);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_StaticallyLinkedX64Tests_CorrectTestResults()
        {
            RunAndVerifyTests(TestResources.X64StaticallyLinkedTests, 1, 1, 0);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_CrashingX64Tests_CorrectTestResults()
        {
            RunAndVerifyTests(TestResources.X64CrashingTests, 0, 2, 0);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_CrashingX86Tests_CorrectTestResults()
        {
            RunAndVerifyTests(TestResources.X86CrashingTests, 0, 2, 0);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_HardCrashingX86Tests_CorrectTestResults()
        {
            TestExecutor executor = new TestExecutor(TestEnvironment);
            executor.RunTests(TestResources.HardCrashingSampleTests.Yield(), MockRunContext.Object, MockFrameworkHandle.Object);

            CheckMockInvocations(1, 2, 0, 3);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WithSetupAndTeardownBatchesWhereTeardownFails_LogsWarning()
        {
            MockOptions.Setup(o => o.BatchForTestSetup).Returns(TestResources.Results0Batch);
            MockOptions.Setup(o => o.BatchForTestTeardown).Returns(TestResources.Results1Batch);

            RunAndVerifyTests(TestResources.X86ExternallyLinkedTests, 2, 0, 0);

            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup))),
                Times.Never);
            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestTeardown))),
                Times.AtLeastOnce());
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WithSetupAndTeardownBatchesWhereSetupFails_LogsWarning()
        {
            MockOptions.Setup(o => o.BatchForTestSetup).Returns(TestResources.Results1Batch);
            MockOptions.Setup(o => o.BatchForTestTeardown).Returns(TestResources.Results0Batch);

            RunAndVerifyTests(TestResources.X64ExternallyLinkedTests, 2, 0, 0);

            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup))),
                Times.AtLeastOnce());
            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestTeardown))),
                Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WithoutBatches_NoLogging()
        {
            RunAndVerifyTests(TestResources.X64ExternallyLinkedTests, 2, 0, 0);

            MockLogger.Verify(l => l.LogInfo(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup))),
                Times.Never);
            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup))),
                Times.Never);
            MockLogger.Verify(l => l.LogError(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup))),
                Times.Never);
            MockLogger.Verify(l => l.LogInfo(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestTeardown))),
                Times.Never);
            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestTeardown))),
                Times.Never);
            MockLogger.Verify(l => l.LogError(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestTeardown))),
                Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WithNonexistingSetupBatch_LogsError()
        {
            MockOptions.Setup(o => o.BatchForTestSetup).Returns("some_nonexisting_file");

            RunAndVerifyTests(TestResources.X64ExternallyLinkedTests, 2, 0, 0);

            MockLogger.Verify(l => l.LogError(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup.ToLower()))),
                Times.AtLeastOnce());
        }

        private void RunAndVerifyTests(string executable, int nrOfPassedTests, int nrOfFailedTests, int nrOfUnexecutedTests, int nrOfNotFoundTests = 0)
        {
            TestExecutor executor = new TestExecutor(TestEnvironment);
            executor.RunTests(executable.Yield(), MockRunContext.Object, MockFrameworkHandle.Object);

            CheckMockInvocations(nrOfPassedTests, nrOfFailedTests, nrOfUnexecutedTests, nrOfNotFoundTests);
        }

    }

}