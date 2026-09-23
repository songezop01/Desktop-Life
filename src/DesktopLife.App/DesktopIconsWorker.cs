using System.Collections.Concurrent;
using DesktopLife.Core;
using DesktopLife.Windows;
namespace DesktopLife.App;
internal sealed class DesktopIconsWorker : IDisposable
{
    private readonly BlockingCollection<Action<DesktopIconSession>> work=new();
    public DesktopIconsWorker(string path)
    {
        var thread=new Thread(()=>{var session=new DesktopIconSession(new ShellDesktopIcons(),path);foreach(var action in work.GetConsumingEnumerable())action(session);}){IsBackground=true,Name="Desktop Life Shell"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
    }
    public Task<T> Run<T>(Func<DesktopIconSession,T> action)
    {
        var completion=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        work.Add(session=>{try{completion.SetResult(action(session));}catch(Exception ex){completion.SetException(ex);}});
        return completion.Task;
    }
    public void Dispose()=>work.CompleteAdding();
}
