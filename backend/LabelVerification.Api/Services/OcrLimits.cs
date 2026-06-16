using System.Collections.Concurrent;
namespace LabelVerification.Api.Services;

public sealed class OcrConcurrencyGate
{
    private readonly SemaphoreSlim _semaphore;

    public OcrConcurrencyGate(IConfiguration configuration)
    {
        var max = configuration.GetValue("OCR_MAX_PARALLEL", configuration.GetValue<int>("Ocr:MaxParallel", 2));
        _semaphore = new SemaphoreSlim(Math.Max(1, max), Math.Max(1, max));
    }

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new ReleaseToken(_semaphore);
    }

    private sealed class ReleaseToken : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _released;

        public ReleaseToken(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _semaphore.Release();
            }
        }
    }
}

public sealed class OcrPerUserLimiter
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _userGates = new();
    private readonly int _maxPerUser;

    public OcrPerUserLimiter(IConfiguration configuration)
    {
        _maxPerUser = Math.Max(1, configuration.GetValue("OCR_MAX_PER_USER", 1));
    }

    public async Task<IDisposable> AcquireAsync(string userId, CancellationToken cancellationToken)
    {
        var gate = _userGates.GetOrAdd(userId, _ => new SemaphoreSlim(_maxPerUser, _maxPerUser));
        await gate.WaitAsync(cancellationToken);
        return new ReleaseToken(gate);
    }

    private sealed class ReleaseToken : IDisposable
    {
        private readonly SemaphoreSlim _gate;
        private int _released;

        public ReleaseToken(SemaphoreSlim gate) => _gate = gate;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _gate.Release();
            }
        }
    }
}

