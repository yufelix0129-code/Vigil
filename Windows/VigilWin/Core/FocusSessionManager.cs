using VigilWin.Models;
using VigilWin.Services;
using System.IO;

namespace VigilWin.Core;

public sealed class FocusSessionManager
{
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly AIService _aiService;
    private readonly IdleDetectorService _idleDetectorService;
    private readonly StorageService _storageService;
    private readonly SettingsService _settingsService;
    private readonly NotificationService _notificationService;
    private readonly OverlayService _overlayService;
    private readonly FrameAnalyzer _frameAnalyzer;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly SemaphoreSlim _tickLock = new(1, 1);
    private readonly LogService? _logService;

    private CancellationTokenSource? _sessionCancellation;
    private Task? _sessionTask;
    private FocusSession? _currentSession;
    private SessionState _state = SessionState.Idle;
    private FocusStatus _currentFocusStatus = FocusStatus.Unknown;
    private string _currentReason = string.Empty;

    public FocusSessionManager(
        ScreenCaptureService screenCaptureService,
        AIService aiService,
        IdleDetectorService idleDetectorService,
        StorageService storageService,
        SettingsService settingsService,
        NotificationService notificationService,
        OverlayService overlayService,
        FrameAnalyzer frameAnalyzer,
        LogService? logService = null)
    {
        _screenCaptureService = screenCaptureService;
        _aiService = aiService;
        _idleDetectorService = idleDetectorService;
        _storageService = storageService;
        _settingsService = settingsService;
        _notificationService = notificationService;
        _overlayService = overlayService;
        _frameAnalyzer = frameAnalyzer;
        _logService = logService;
    }

    public event EventHandler<SessionState>? StateChanged;

    public event EventHandler<FocusSession>? TickUpdated;

    public event EventHandler<FrameRecord>? AnalysisUpdated;

    public event EventHandler<FocusSession>? SessionCompleted;

    public event EventHandler<string>? ErrorOccurred;

    public DateTime? CurrentSessionStartTime => _currentSession?.StartTime;

    public SessionState CurrentState => _state;

    public TimeSpan CurrentElapsed => _currentSession is null ? TimeSpan.Zero : GetElapsed(_currentSession);

    public string CurrentGoal => _currentSession?.Goal ?? string.Empty;

    public int CurrentDistractionCount => _currentSession?.DistractionCount ?? 0;

    public FocusStatus CurrentFocusStatus => _currentFocusStatus;

    public string CurrentReason => _currentReason;

    public async Task StartSessionAsync(string goal, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);

        await _lifecycleLock.WaitAsync();
        try
        {
            if (_currentSession is not null && _currentSession.EndTime is null)
            {
                _logService?.Warn("StartSessionAsync ignored because a session is already running.");
                throw new InvalidOperationException("已有专注会话正在运行。");
            }

            await _storageService.InitializeAsync();
            SetState(SessionState.Preparing);

            var session = new FocusSession
            {
                Goal = goal.Trim(),
                StartTime = DateTime.Now,
                PlannedDurationSeconds = Math.Max(1, (int)duration.TotalSeconds)
            };

            await _storageService.CreateSessionAsync(session);
            _currentSession = session;
            _currentFocusStatus = FocusStatus.Unknown;
            _currentReason = string.Empty;
            _sessionCancellation = new CancellationTokenSource();
            SetState(SessionState.Running);
            TickUpdated?.Invoke(this, session);
            _logService?.Info($"Session started. id={session.Id} durationSeconds={session.PlannedDurationSeconds}");

            _sessionTask = Task.Run(() => RunSessionLoopAsync(session, _sessionCancellation.Token));
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopSessionAsync()
    {
        FocusSession? sessionToEnd;
        CancellationTokenSource? cancellation;
        Task? sessionTask;

        await _lifecycleLock.WaitAsync();
        try
        {
            sessionToEnd = _currentSession;
            cancellation = _sessionCancellation;
            sessionTask = _sessionTask;
            cancellation?.Cancel();
            _logService?.Info(sessionToEnd is null ? "StopSessionAsync called without active session." : $"Stop requested. sessionId={sessionToEnd.Id}");
        }
        finally
        {
            _lifecycleLock.Release();
        }

        if (sessionTask is not null)
        {
            await Task.WhenAny(sessionTask, Task.Delay(TimeSpan.FromSeconds(2)));
        }

        if (sessionToEnd is not null && sessionToEnd.EndTime is null)
        {
            await EndSessionAsync(sessionToEnd, SessionState.Cancelled);
        }
    }

    private async Task RunSessionLoopAsync(FocusSession session, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && GetElapsed(session) < TimeSpan.FromSeconds(session.PlannedDurationSeconds))
            {
                var settings = _settingsService.Load();
                var intervalSeconds = Math.Max(1, settings.CaptureIntervalSeconds);
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

                if (!await timer.WaitForNextTickAsync(cancellationToken))
                {
                    break;
                }

                if (GetElapsed(session) >= TimeSpan.FromSeconds(session.PlannedDurationSeconds))
                {
                    break;
                }

                await HandleTickAsync(session, intervalSeconds, cancellationToken);
            }

            if (!cancellationToken.IsCancellationRequested && session.EndTime is null)
            {
                await EndSessionAsync(session, SessionState.Completed);
            }
        }
        catch (OperationCanceledException)
        {
            // StopSessionAsync owns the cancellation path and persists the final session state.
        }
        catch (Exception ex)
        {
            OnError($"会话循环出错：{ex.Message}");
            if (session.EndTime is null)
            {
                await EndSessionAsync(session, SessionState.Error);
            }
        }
    }

    private async Task HandleTickAsync(FocusSession session, int intervalSeconds, CancellationToken cancellationToken)
    {
        if (!await _tickLock.WaitAsync(0, cancellationToken))
        {
            _logService?.Warn("Skipped tick because previous tick is still running.");
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var settings = _settingsService.Load();
            var threshold = TimeSpan.FromSeconds(Math.Max(1, settings.IdleThresholdSeconds));

            if (_idleDetectorService.IsIdle(threshold))
            {
                var idleRecord = new FrameRecord
                {
                    SessionId = session.Id,
                    Timestamp = DateTime.Now,
                    Status = FocusStatus.Idle,
                    Confidence = 1,
                    Reason = $"用户空闲超过 {threshold.TotalSeconds:0} 秒"
                };

                _logService?.Info($"Tick recorded idle. sessionId={session.Id}");
                await SaveRecordAndUpdateSessionAsync(session, idleRecord, intervalSeconds);
                return;
            }

            byte[] screenshot;
            string? screenshotPath = null;

            try
            {
                SetState(SessionState.Analyzing);
                screenshot = await _screenCaptureService.CapturePrimaryScreenJpegAsync();
                cancellationToken.ThrowIfCancellationRequested();

                if (settings.SaveScreenshots)
                {
                    screenshotPath = await SaveScreenshotAsync(session.Id, screenshot, cancellationToken);
                    _logService?.Info($"Screenshot saved for session. sessionId={session.Id}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex.Message);
                var captureFailureRecord = new FrameRecord
                {
                    SessionId = session.Id,
                    Timestamp = DateTime.Now,
                    Status = FocusStatus.Unknown,
                    Confidence = 0,
                    Reason = ex.Message
                };
                await SaveRecordAndUpdateSessionAsync(session, captureFailureRecord, intervalSeconds);
                SetState(SessionState.Running);
                return;
            }

            AIAnalysisResult result;
            if (!_frameAnalyzer.ShouldAnalyze(screenshot))
            {
                result = new AIAnalysisResult
                {
                    Status = FocusStatus.Unknown,
                    Confidence = 0,
                    Reason = "截图未发生明显变化，跳过 AI 分析"
                };
            }
            else
            {
                result = await _aiService.AnalyzeScreenshotAsync(
                    session.Goal,
                    screenshot,
                    settings.AIProvider,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var analysisRecord = new FrameRecord
            {
                SessionId = session.Id,
                Timestamp = DateTime.Now,
                Status = result.Status,
                Confidence = result.Confidence,
                Reason = result.Reason,
                ScreenshotPath = screenshotPath
            };

            await SaveRecordAndUpdateSessionAsync(session, analysisRecord, intervalSeconds);

            if (analysisRecord.Status == FocusStatus.Distracted)
            {
                _notificationService.ShowDistractedNotification(session.Goal, analysisRecord.Reason);
                if (settings.EnableOverlay)
                {
                    _overlayService.ShowDistractedOverlay(session.Goal, analysisRecord.Reason);
                }
            }

            SetState(SessionState.Running);
        }
        finally
        {
            _tickLock.Release();
        }
    }

    private async Task SaveRecordAndUpdateSessionAsync(FocusSession session, FrameRecord record, int intervalSeconds)
    {
        var remainingSeconds = Math.Max(0, session.PlannedDurationSeconds - (int)GetElapsed(session).TotalSeconds);
        var accountedSeconds = Math.Max(1, Math.Min(intervalSeconds, remainingSeconds == 0 ? intervalSeconds : remainingSeconds));

        AddStatusSeconds(session, record.Status, accountedSeconds);
        _currentFocusStatus = record.Status;
        _currentReason = record.Reason;
        await _storageService.AddFrameRecordAsync(record);
        await _storageService.UpdateSessionAsync(session);

        AnalysisUpdated?.Invoke(this, record);
        TickUpdated?.Invoke(this, session);
    }

    private async Task EndSessionAsync(FocusSession session, SessionState finalState)
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (session.EndTime is not null)
            {
                return;
            }

            session.EndTime = DateTime.Now;
            SetState(finalState);
        }
        finally
        {
            _lifecycleLock.Release();
        }

        try
        {
            var records = await _storageService.GetFrameRecordsAsync(session.Id);
            var settings = _settingsService.Load();
            if (finalState == SessionState.Cancelled)
            {
                session.Summary = SummaryService.BuildLocalSummary(session, records);
            }
            else
            {
                using var summaryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                session.Summary = await _aiService.GenerateSummaryAsync(session, records, settings.AIProvider, summaryTimeout.Token);
            }
        }
        catch (Exception ex)
        {
            OnError($"生成总结失败：{ex.Message}");
            session.Summary ??= "总结生成失败，但会话记录已保存。";
        }

        await _storageService.UpdateSessionAsync(session);
        _logService?.Info($"Session ended. id={session.Id} state={finalState} distractions={session.DistractionCount}");
        SessionCompleted?.Invoke(this, session);
        TickUpdated?.Invoke(this, session);

        await _lifecycleLock.WaitAsync();
        try
        {
            _currentSession = null;
            _sessionCancellation?.Dispose();
            _sessionCancellation = null;
            _sessionTask = null;

            if (finalState is SessionState.Completed or SessionState.Cancelled)
            {
                SetState(finalState);
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private static async Task<string> SaveScreenshotAsync(Guid sessionId, byte[] screenshot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(SettingsService.ScreenshotDirectory);
        var fileName = $"{sessionId:N}-{DateTime.Now:yyyyMMddHHmmssfff}.jpg";
        var path = Path.Combine(SettingsService.ScreenshotDirectory, fileName);
        await File.WriteAllBytesAsync(path, screenshot, cancellationToken);
        return path;
    }

    private static void AddStatusSeconds(FocusSession session, FocusStatus status, int seconds)
    {
        switch (status)
        {
            case FocusStatus.Focused:
                session.FocusedSeconds += seconds;
                break;
            case FocusStatus.Wandering:
                session.WanderingSeconds += seconds;
                break;
            case FocusStatus.Distracted:
                session.DistractedSeconds += seconds;
                session.DistractionCount += 1;
                break;
            case FocusStatus.Idle:
                session.IdleSeconds += seconds;
                break;
        }
    }

    private void SetState(SessionState state)
    {
        _state = state;
        StateChanged?.Invoke(this, _state);
    }

    private void OnError(string message)
    {
        _logService?.Warn(message);
        ErrorOccurred?.Invoke(this, message);
    }

    private static TimeSpan GetElapsed(FocusSession session)
    {
        return (session.EndTime ?? DateTime.Now) - session.StartTime;
    }
}
