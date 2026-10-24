namespace MacKeys;

public readonly record struct ProfileApplyResult(string Profile, bool Changed, bool Suspended);

public sealed class ProfileSession
{
    private readonly KeyboardManagerStore _store;
    private readonly Action _signal;
    private readonly object _gate = new();
    private IReadOnlyList<GameEntry> _games;
    private bool _suspended;
    private string? _active;
    private bool _stopped;

    public ProfileSession(
        KeyboardManagerStore store,
        Action signal,
        bool suspended,
        IReadOnlyList<GameEntry> games)
    {
        _store = store;
        _signal = signal;
        _suspended = suspended;
        _games = games;
    }

    public bool Suspended
    {
        get
        {
            lock (_gate)
                return _suspended;
        }
    }

    public string? ActiveProfile
    {
        get
        {
            lock (_gate)
                return _active;
        }
    }

    public ProfileApplyResult ApplyNow(ForegroundObservation foreground)
    {
        lock (_gate)
        {
            _store.WriteProfiles();
            return Activate(foreground, force: true);
        }
    }

    public ProfileApplyResult OnForeground(ForegroundObservation foreground)
    {
        lock (_gate)
        {
            if (_stopped || _suspended)
                return new ProfileApplyResult(_active ?? Profiles.Off, false, _suspended);

            return Activate(foreground, force: false);
        }
    }

    public ProfileApplyResult SetSuspended(bool suspended, ForegroundObservation foreground)
    {
        lock (_gate)
        {
            _suspended = suspended;
            return Activate(foreground, force: true);
        }
    }

    public void UpdateGames(IReadOnlyList<GameEntry> games)
    {
        lock (_gate)
            _games = games;
    }

    public void Exit()
    {
        lock (_gate)
        {
            if (_stopped)
                return;

            _stopped = true;
            var target = ProfileSelection.OnExit(_suspended);
            if (_suspended && string.Equals(_active, Profiles.Off, StringComparison.Ordinal))
                return;

            _store.SetActiveConfiguration(target);
            _signal();
            _active = target;
        }
    }

    private ProfileApplyResult Activate(ForegroundObservation foreground, bool force)
    {
        var desired = ProfileSelection.ForForeground(_suspended, foreground, _games);
        if (!force && string.Equals(_active, desired, StringComparison.Ordinal))
            return new ProfileApplyResult(desired, false, _suspended);

        _store.SetActiveConfiguration(desired);
        _signal();
        _active = desired;
        return new ProfileApplyResult(desired, true, _suspended);
    }
}
