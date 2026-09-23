using System.Runtime.InteropServices;
using System.Diagnostics;
using DesktopLife.Core;
namespace DesktopLife.Windows;

// Companion runtime observes only presence and cursor movement, never hardware workload.
public sealed class CompanionPresenceSensor : IEnvironmentSensor
{
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private double previousTime;
    private (double X,double Y)? previous;
    public EnvironmentState Sample()
    {
        var input=new LastInput{Size=(uint)Marshal.SizeOf<LastInput>()};
        var idle=GetLastInputInfo(ref input)?SensorValue.Available(SensorMath.IdleSeconds(unchecked((uint)Environment.TickCount),input.Time)):SensorValue.Missing("閒置時間無法取得");
        var now=clock.Elapsed.TotalSeconds;var cursor=DesktopInteraction.Cursor();
        var speed=cursor is {} p && previous is {} old && now>previousTime?SensorValue.Available(Math.Sqrt(Math.Pow(p.X-old.X,2)+Math.Pow(p.Y-old.Y,2))/(now-previousTime)):SensorValue.Missing("等待下一次游標取樣");
        previous=cursor;previousTime=now;
        var off=SensorValue.Missing("陪伴版不使用硬體負載");
        return new(DateTimeOffset.UtcNow,off,off,off,off,off,off,off,idle,speed,0);
    }
    public void Dispose() { }
    [StructLayout(LayoutKind.Sequential)]private struct LastInput{public uint Size,Time;}
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetLastInputInfo(ref LastInput input);
}
