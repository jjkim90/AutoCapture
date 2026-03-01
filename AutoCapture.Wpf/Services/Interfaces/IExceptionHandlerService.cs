using System;

namespace AutoCapture.Wpf.Services.Interfaces
{
    public interface IExceptionHandlerService
    {
        void RegisterGlobalHandlers();
        void UnregisterGlobalHandlers();
        void HandleException(Exception exception, bool canContinue = true, bool canRestart = true);
    }
}