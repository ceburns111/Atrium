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
public sealed class ToastService
{
    private readonly List<Toast> _toasts = [];

    public IReadOnlyList<Toast> Toasts => _toasts;
    public event Action? Changed;

    public void Show(string message, ToastVariant variant = ToastVariant.Neutral)
    {
        var toast = new Toast(Guid.NewGuid(), message, variant);
        _toasts.Add(toast);
        Changed?.Invoke();
        _ = DismissAfterAsync(toast.Id, TimeSpan.FromSeconds(3));
    }

    public void Dismiss(Guid id)
    {
        if (_toasts.RemoveAll(t => t.Id == id) > 0)
        {
            Changed?.Invoke();
        }
    }

    private async Task DismissAfterAsync(Guid id, TimeSpan delay)
    {
        await Task.Delay(delay);
        Dismiss(id);
    }
}
