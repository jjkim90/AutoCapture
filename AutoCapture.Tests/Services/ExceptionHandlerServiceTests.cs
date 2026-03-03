using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AutoCapture.Wpf.Services.Implementations;
using AutoCapture.Wpf.Services.Interfaces;
using Moq;
using Serilog;
using Xunit;

namespace AutoCapture.Tests.Services
{
    public class ExceptionHandlerServiceTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly ExceptionHandlerService _service;

        public ExceptionHandlerServiceTests()
        {
            _mockLogger = new Mock<ILogger>();
            _service = new ExceptionHandlerService(_mockLogger.Object)
            {
                IsTestMode = true  // 테스트 모드 활성화
            };
        }

        [Fact]
        public void RegisterGlobalHandlers_ShouldLogInformation()
        {
            // Act
            _service.RegisterGlobalHandlers();

            // Assert
            _mockLogger.Verify(x => x.Information("전역 예외 처리기 등록 완료"), Times.Once);
        }

        [Fact]
        public void UnregisterGlobalHandlers_ShouldLogInformation()
        {
            // Arrange
            _service.RegisterGlobalHandlers();

            // Act
            _service.UnregisterGlobalHandlers();

            // Assert
            _mockLogger.Verify(x => x.Information("전역 예외 처리기 해제 완료"), Times.Once);
        }

        [Fact]
        public void HandleException_WithNullException_ShouldReturnWithoutAction()
        {
            // Act
            _service.HandleException(null!);

            // Assert
            _mockLogger.VerifyNoOtherCalls();
        }

        [Fact]
        public void WriteCrashLog_ShouldCreateLogFile()
        {
            // Arrange
            var testException = new InvalidOperationException("Test exception");
            var logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            
            // Clean up any existing crash logs
            if (Directory.Exists(logsDirectory))
            {
                var existingCrashLogs = Directory.GetFiles(logsDirectory, "crash_*.log");
                foreach (var log in existingCrashLogs)
                {
                    File.Delete(log);
                }
            }

            // Act - Use reflection to call private method
            var methodInfo = typeof(ExceptionHandlerService).GetMethod("WriteCrashLog", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            methodInfo?.Invoke(_service, new object[] { testException });

            // Assert
            Assert.True(Directory.Exists(logsDirectory));
            var crashLogs = Directory.GetFiles(logsDirectory, "crash_*.log");
            Assert.NotEmpty(crashLogs);

            // Read the crash log and verify content
            var logContent = File.ReadAllText(crashLogs[0]);
            Assert.Contains("AutoCapture 크래시 로그", logContent);
            Assert.Contains("InvalidOperationException", logContent);
            Assert.Contains("Test exception", logContent);

            // Clean up
            foreach (var log in crashLogs)
            {
                File.Delete(log);
            }
        }

        [Fact]
        public void HandleDispatcherUnhandledException_MethodExists()
        {
            // This test verifies the method exists with correct signature
            var methodInfo = typeof(ExceptionHandlerService).GetMethod("HandleDispatcherUnhandledException",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.NotNull(methodInfo);
            
            var parameters = methodInfo.GetParameters();
            Assert.Equal(2, parameters.Length);
            Assert.Equal("sender", parameters[0].Name);
            Assert.Equal("e", parameters[1].Name);
            Assert.Equal(typeof(DispatcherUnhandledExceptionEventArgs), parameters[1].ParameterType);
        }

        [Fact]
        public void HandleAppDomainUnhandledException_WithTerminatingException_ShouldLogFatal()
        {
            // Arrange
            var testException = new OutOfMemoryException("Critical exception");
            var eventArgs = new UnhandledExceptionEventArgs(testException, isTerminating: true);

            // Act - Call private method via reflection
            var methodInfo = typeof(ExceptionHandlerService).GetMethod("HandleAppDomainUnhandledException",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (methodInfo != null)
            {
                try
                {
                    methodInfo.Invoke(_service, new object?[] { null, eventArgs });
                }
                catch (Exception)
                {
                    // Expected - MessageBox.Show will fail in unit test
                }
            }

            // Assert
            _mockLogger.Verify(x => x.Fatal(
                It.IsAny<Exception>(), 
                It.IsAny<string>(), 
                It.IsAny<bool>()), 
                Times.Once);
        }

        [Fact]
        public void HandleTaskSchedulerUnobservedException_ShouldLogErrorAndSetObserved()
        {
            // Arrange
            var innerException = new InvalidOperationException("Async exception");
            var aggregateException = new AggregateException(innerException);
            var eventArgs = new UnobservedTaskExceptionEventArgs(aggregateException);

            // Act - Call private method via reflection
            var methodInfo = typeof(ExceptionHandlerService).GetMethod("HandleTaskSchedulerUnobservedException",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (methodInfo != null)
            {
                try
                {
                    methodInfo.Invoke(_service, new object?[] { null, eventArgs });
                }
                catch (Exception)
                {
                    // Expected - Dialog will fail in unit test
                }
            }

            // Assert
            Assert.True(eventArgs.Observed);
            _mockLogger.Verify(x => x.Error(
                It.IsAny<AggregateException>(), 
                "비동기 작업에서 관찰되지 않은 예외 발생"), 
                Times.Once);
        }
    }
}