namespace FastLazy.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest, MethodOrderPolicy.Declared)]
[RankColumn]
//[EventPipeProfiler(EventPipeProfile.CpuSampling)]
[DisassemblyDiagnoser]
//[InliningDiagnoser(logFailuresOnly: true, allowedNamespaces: new[] { "Mediator" })]
public class FastLazyValueBenchmarks
{
    private Lazy<long> _lazy;
    private FastLazyValue<long, long> _fastLazy;

    [GlobalSetup]
    public void Setup()
    {
        _lazy = new Lazy<long>(() => 1);
        _fastLazy = new FastLazyValue<long, long>(_ => 1, 0);
    }

    [Benchmark]
    public long Lazy_Value() => _lazy.Value;

    [Benchmark]
    public long FastLazy_Value() => _fastLazy.Value;
}
