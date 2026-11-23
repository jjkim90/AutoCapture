using System;
using System.IO;
using AutoCapture.Wpf.Models;
using Xunit;

namespace AutoCapture.Tests.Models
{
    public class ErrorMessagesTests
    {
        [Fact]
        public void TargetProblem_ExplainsWhatToDoInsteadOfUnexpectedError()
        {
            var exception = new CaptureTargetException("입력 대상 창이 닫혀서 멈췄습니다.");

            var message = ErrorMessages.GetUserFriendlyMessage(exception);
            var advice = ErrorMessages.GetRecoveryAdvice(exception);

            Assert.Equal("입력 대상 창이 닫혀서 멈췄습니다.", message);
            Assert.DoesNotContain("예기치 않은", message);
            Assert.DoesNotContain("Exception", message);
            Assert.Contains("다시 선택", advice);
            Assert.DoesNotContain("재부팅", advice);
        }

        [Fact]
        public void GetUserFriendlyMessage_UnauthorizedAccessException_ReturnsCorrectMessage()
        {
            // Arrange
            var exception = new UnauthorizedAccessException("Access denied");

            // Act
            var message = ErrorMessages.GetUserFriendlyMessage(exception);

            // Assert
            Assert.Contains("파일 또는 폴더에 접근할 수 없습니다", message);
            Assert.Contains("관리자 권한", message);
        }

        [Fact]
        public void GetUserFriendlyMessage_DirectoryNotFoundException_ReturnsCorrectMessage()
        {
            // Arrange
            var exception = new DirectoryNotFoundException("Directory not found");

            // Act
            var message = ErrorMessages.GetUserFriendlyMessage(exception);

            // Assert
            Assert.Contains("지정된 저장 폴더를 찾을 수 없습니다", message);
        }

        [Fact]
        public void GetUserFriendlyMessage_IOException_WithDiskMessage_ReturnsCorrectMessage()
        {
            // Arrange
            var exception = new IOException("Not enough disk space");

            // Act
            var message = ErrorMessages.GetUserFriendlyMessage(exception);

            // Assert
            Assert.Contains("디스크 공간이 부족합니다", message);
        }

        [Fact]
        public void GetUserFriendlyMessage_IOException_Generic_ReturnsCorrectMessage()
        {
            // Arrange
            var exception = new IOException("Generic IO error");

            // Act
            var message = ErrorMessages.GetUserFriendlyMessage(exception);

            // Assert
            Assert.Contains("파일 저장 중 오류가 발생했습니다", message);
        }

        [Fact]
        public void GetUserFriendlyMessage_OutOfMemoryException_ReturnsCorrectMessage()
        {
            // Arrange
            var exception = new OutOfMemoryException();

            // Act
            var message = ErrorMessages.GetUserFriendlyMessage(exception);

            // Assert
            Assert.Contains("메모리가 부족합니다", message);
        }

        [Fact]
        public void GetUserFriendlyMessage_InvalidOperationException_WithGDIPlus_ReturnsCorrectMessage()
        {
            // Arrange
            var exception = new InvalidOperationException("GDI+ error occurred");

            // Act
            var message = ErrorMessages.GetUserFriendlyMessage(exception);

            // Assert
            Assert.Contains("화면 캡처에 실패했습니다", message);
        }

        [Fact]
        public void GetUserFriendlyMessage_UnknownException_ReturnsGenericMessage()
        {
            // Arrange
            var exception = new ArgumentException("Unknown error");

            // Act
            var message = ErrorMessages.GetUserFriendlyMessage(exception);

            // Assert
            Assert.Contains("예기치 않은 오류가 발생했습니다", message);
            Assert.Contains("ArgumentException", message);
        }

        [Fact]
        public void GetRecoveryAdvice_UnauthorizedAccessException_ReturnsCorrectAdvice()
        {
            // Arrange
            var exception = new UnauthorizedAccessException();

            // Act
            var advice = ErrorMessages.GetRecoveryAdvice(exception);

            // Assert
            Assert.Contains("관리자 권한으로 실행", advice);
            Assert.Contains("쓰기 권한 확인", advice);
        }

        [Fact]
        public void GetRecoveryAdvice_OutOfMemoryException_ReturnsCorrectAdvice()
        {
            // Arrange
            var exception = new OutOfMemoryException();

            // Act
            var advice = ErrorMessages.GetRecoveryAdvice(exception);

            // Assert
            Assert.Contains("캡처 크기 축소", advice);
            Assert.Contains("캡처 간격 늘리기", advice);
        }

        [Fact]
        public void GetRecoveryAdvice_UnknownException_ReturnsGenericAdvice()
        {
            // Arrange
            var exception = new ArgumentException();

            // Act
            var advice = ErrorMessages.GetRecoveryAdvice(exception);

            // Assert
            Assert.Contains("프로그램 재시작", advice);
            Assert.Contains("컴퓨터 재부팅", advice);
        }
    }
}