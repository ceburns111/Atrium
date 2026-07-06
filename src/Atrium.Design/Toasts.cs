namespace Atrium.Design;

public enum ToastVariant
{
    Neutral,
    Success,
    Danger,
}

public sealed record Toast(Guid Id, string Message, ToastVariant Variant);

/// <summary>
/// Lightweight transient notifications, scoped per circuit. Any module injects this to surface
/// feedback (e.g. "Added to cart"); <c>ToastHost</c> renders them and they auto-dismiss.
/// </summary>
/// <remarks>
/// <see cref="Show"/> is expected to be called on the circuit's dispatcher (a UI event handler or
/// lifecycle method) — that's every current caller. The auto-dismiss timer, however, resumes on a
/// thread-pool thread, so <see cref="Dismiss"/> can mutate the list while <c>ToastHost</c> is
/// rendering; <see cref="Toasts"/> therefore hands out a snapshot and mutations are locked.
/// </remarks>
public sealed class ToastService
{
    private readonly object _gate = new();
    private readonly List<Toast> _toasts = [];

    /// <summary>A point-in-time snapshot, safe to enumerate while a timer dismissal races a render.</summary>
    public IReadOnlyList<Toast> Toasts
    {
        get
        {
            lock (_gate)
            {
                return [.. _toasts];
            }
        }
    }

    public event Action? Changed;

    public void Show(string message, ToastVariant variant = ToastVariant.Neutral)
    {
        var toast = new Toast(Guid.NewGuid(), message, variant);
        lock (_gate)
        {
            _toasts.Add(toast);
        }
        Changed?.Invoke();
        _ = DismissAfterAsync(toast.Id, TimeSpan.FromSeconds(3));
    }

    public void Dismiss(Guid id)
    {
        bool removed;
        lock (_gate)
        {
            removed = _toasts.RemoveAll(t => t.Id == id) > 0;
        }
        if (removed)
        {
            // Subscribers marshal to the dispatcher themselves (ToastHost wraps this in InvokeAsync),
            // so raising from the timer's thread-pool continuation is fine.
            Changed?.Invoke();
        }
    }

    private async Task DismissAfterAsync(Guid id, TimeSpan delay)
    {
        await Task.Delay(delay);
        Dismiss(id);
    }
}
