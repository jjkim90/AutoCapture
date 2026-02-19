using System.Diagnostics;

namespace AutoCapture.Wpf.Utilities
{
    public static class MemoryHelper
    {
        /// <summary>
        /// 현재 프로세스의 메모리 사용량을 MB 단위로 반환
        /// </summary>
        public static double GetCurrentMemoryUsageMB()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.WorkingSet64 / 1024.0 / 1024.0;
            }
        }

        /// <summary>
        /// 메모리 압박 상태 확인 (임계값: 1GB)
        /// </summary>
        public static bool IsMemoryPressureHigh(double thresholdMB = 1024)
        {
            return GetCurrentMemoryUsageMB() > thresholdMB;
        }

        /// <summary>
        /// 강제 가비지 컬렉션 수행 및 결과 반환
        /// </summary>
        public static (double beforeMB, double afterMB) PerformGarbageCollection()
        {
            var beforeMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var afterMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            
            return (beforeMB, afterMB);
        }
    }
}