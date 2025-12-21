using Serilog;

namespace ClipVault.App.Extensions;

/// <summary>
/// Extensions for asynchronous operations.
/// </summary>
public static class AsyncExtensions
{
    private static readonly ILogger Logger = Log.ForContext(typeof(AsyncExtensions));
    
    /// <summary>
    /// Safely fires and forgets a Task, logging any exceptions.
    /// Use this instead of discarding tasks with _ = or ignoring the warning.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="errorHandler">Optional error handler. If not provided, errors are logged.</param>
    public static async void SafeFireAndForget(
        this Task task, 
        Action<Exception>? errorHandler = null)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (errorHandler != null)
            {
                errorHandler(ex);
            }
            else
            {
                Logger.Error(ex, "Unhandled exception in fire-and-forget task");
            }
        }
    }
    
    /// <summary>
    /// Safely fires and forgets a Task{T}, logging any exceptions.
    /// Use this instead of discarding tasks with _ = or ignoring the warning.
    /// </summary>
    /// <typeparam name="T">The result type of the task.</typeparam>
    /// <param name="task">The task to execute.</param>
    /// <param name="errorHandler">Optional error handler. If not provided, errors are logged.</param>
    public static async void SafeFireAndForget<T>(
        this Task<T> task, 
        Action<Exception>? errorHandler = null)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (errorHandler != null)
            {
                errorHandler(ex);
            }
            else
            {
                Logger.Error(ex, "Unhandled exception in fire-and-forget task");
            }
        }
    }
    
    /// <summary>
    /// Safely fires and forgets a ValueTask, logging any exceptions.
    /// </summary>
    /// <param name="valueTask">The value task to execute.</param>
    /// <param name="errorHandler">Optional error handler. If not provided, errors are logged.</param>
    public static async void SafeFireAndForget(
        this ValueTask valueTask, 
        Action<Exception>? errorHandler = null)
    {
        try
        {
            await valueTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (errorHandler != null)
            {
                errorHandler(ex);
            }
            else
            {
                Logger.Error(ex, "Unhandled exception in fire-and-forget value task");
            }
        }
    }
}
