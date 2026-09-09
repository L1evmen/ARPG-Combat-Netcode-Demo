using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CombatV2;
using CombatV2.Actions;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

public sealed class PerformanceBenchmark : MonoBehaviour
{
    private const float WarmupSeconds = 10f;
    private const float SampleSeconds = 30f;
    private const int SampleCapacity = 60000;

    private readonly List<double> _frameTimes = new List<double>(SampleCapacity);
    private readonly List<double> _mainThreadTimes = new List<double>(SampleCapacity);
    private readonly List<double> _renderThreadTimes = new List<double>(SampleCapacity);
    private readonly List<double> _gpuTimes = new List<double>(SampleCapacity);
    private readonly List<double> _gcAllocations = new List<double>(SampleCapacity);
    private readonly List<double> _systemMemory = new List<double>(SampleCapacity);
    private readonly List<double> _gcMemory = new List<double>(SampleCapacity);
    private readonly List<double> _batches = new List<double>(SampleCapacity);
    private readonly List<double> _setPassCalls = new List<double>(SampleCapacity);
    private readonly List<double> _drawCalls = new List<double>(SampleCapacity);
    private readonly List<double> _triangles = new List<double>(SampleCapacity);
    private readonly List<double> _vertices = new List<double>(SampleCapacity);

    private ProfilerRecorder _mainThreadRecorder;
    private ProfilerRecorder _renderThreadRecorder;
    private ProfilerRecorder _gpuRecorder;
    private ProfilerRecorder _gcAllocationRecorder;
    private ProfilerRecorder _systemMemoryRecorder;
    private ProfilerRecorder _gcMemoryRecorder;
    private ProfilerRecorder _batchesRecorder;
    private ProfilerRecorder _setPassRecorder;
    private ProfilerRecorder _drawCallsRecorder;
    private ProfilerRecorder _trianglesRecorder;
    private ProfilerRecorder _verticesRecorder;

    private ComboManager _combo;
    private PlayerProperty _playerProperty;
    private float _nextAttackTime;
    private bool _sampling;
    private bool _profiling;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-performance-test") < 0)
            return;

        var benchmark = new GameObject(nameof(PerformanceBenchmark));
        DontDestroyOnLoad(benchmark);
        benchmark.AddComponent<PerformanceBenchmark>();
    }

    private IEnumerator Start()
    {
        QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;
        Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);

        yield return new WaitForSecondsRealtime(2f);

        var player = GameObject.FindGameObjectWithTag("Player");
        var boss = FindFirstObjectByType<BossController>();
        _combo = player.GetComponent<ComboManager>();
        _playerProperty = player.GetComponent<PlayerProperty>();

        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = boss.transform.position + Vector3.back * 3f + Vector3.up * 1.65f;
        player.transform.LookAt(new Vector3(boss.transform.position.x, player.transform.position.y, boss.transform.position.z));
        controller.enabled = true;
        _combo.SetLockedEnemy(boss.transform);

        float warmupEnd = Time.realtimeSinceStartup + WarmupSeconds;
        while (Time.realtimeSinceStartup < warmupEnd)
        {
            DriveCombat();
            yield return null;
        }

        StartRecorders();
        StartProfilerCapture();
        _sampling = true;

        float sampleEnd = Time.realtimeSinceStartup + SampleSeconds;
        while (Time.realtimeSinceStartup < sampleEnd)
        {
            DriveCombat();
            yield return null;
        }

        _sampling = false;
        StopProfilerCapture();
        DisposeRecorders();

        string outputPath = GetArgumentValue("-performance-output");
        string screenshotPath = Path.ChangeExtension(outputPath, ".png");
        File.WriteAllText(outputPath, JsonUtility.ToJson(CreateResult(), true));
        ScreenCapture.CaptureScreenshot(screenshotPath);
        Debug.Log($"[PerformanceBenchmark] COMPLETE output={outputPath}");

        yield return new WaitForSecondsRealtime(2f);
        Application.Quit();
    }

    private void LateUpdate()
    {
        if (!_sampling)
            return;

        _frameTimes.Add(Time.unscaledDeltaTime * 1000.0);
        AddNanoseconds(_mainThreadRecorder, _mainThreadTimes);
        AddNanoseconds(_renderThreadRecorder, _renderThreadTimes);
        AddNanoseconds(_gpuRecorder, _gpuTimes);
        AddValue(_gcAllocationRecorder, _gcAllocations);
        AddBytesAsMegabytes(_systemMemoryRecorder, _systemMemory);
        AddBytesAsMegabytes(_gcMemoryRecorder, _gcMemory);
        AddValue(_batchesRecorder, _batches);
        AddValue(_setPassRecorder, _setPassCalls);
        AddValue(_drawCallsRecorder, _drawCalls);
        AddValue(_trianglesRecorder, _triangles);
        AddValue(_verticesRecorder, _vertices);
    }

    private void DriveCombat()
    {
        if (_combo.IsInAction || Time.unscaledTime < _nextAttackTime)
            return;

        _playerProperty.mentalValue = _playerProperty.mentalMax;
        _combo.ForceTransition<LightAttack1>();
        _nextAttackTime = Time.unscaledTime + 0.5f;
    }

    private void StartRecorders()
    {
        _mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        _renderThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
        _gpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 1);
        _gcAllocationRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        _systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory", 1);
        _gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory", 1);
        _batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
        _setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
        _drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
        _trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
        _verticesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count", 1);
    }

    private void DisposeRecorders()
    {
        _mainThreadRecorder.Dispose();
        _renderThreadRecorder.Dispose();
        _gpuRecorder.Dispose();
        _gcAllocationRecorder.Dispose();
        _systemMemoryRecorder.Dispose();
        _gcMemoryRecorder.Dispose();
        _batchesRecorder.Dispose();
        _setPassRecorder.Dispose();
        _drawCallsRecorder.Dispose();
        _trianglesRecorder.Dispose();
        _verticesRecorder.Dispose();
    }

    private void StartProfilerCapture()
    {
        string outputPath = GetOptionalArgumentValue("-performance-profiler-output");
        if (outputPath == null)
            return;

        StartCoroutine(CaptureProfiler(outputPath));
    }

    private IEnumerator CaptureProfiler(string outputPath)
    {
        var endOfFrame = new WaitForEndOfFrame();
        yield return endOfFrame;

        Profiler.logFile = outputPath;
        Profiler.enableBinaryLog = true;
        Profiler.enableAllocationCallstacks = true;
        Profiler.maxUsedMemory = 256 * 1024 * 1024;
        Profiler.enabled = true;
        _profiling = true;

        for (int i = 0; i < 120; i++)
            yield return endOfFrame;

        StopProfilerCapture();
    }

    private void StopProfilerCapture()
    {
        if (!_profiling)
            return;

        Profiler.enabled = false;
        Profiler.logFile = string.Empty;
        Profiler.enableAllocationCallstacks = false;
        _profiling = false;
    }

    private static void AddValue(ProfilerRecorder recorder, List<double> values)
    {
        if (recorder.Valid)
            values.Add(recorder.LastValue);
    }

    private static void AddNanoseconds(ProfilerRecorder recorder, List<double> values)
    {
        if (recorder.Valid && recorder.LastValue > 0)
            values.Add(recorder.LastValue / 1000000.0);
    }

    private static void AddBytesAsMegabytes(ProfilerRecorder recorder, List<double> values)
    {
        if (recorder.Valid)
            values.Add(recorder.LastValue / 1048576.0);
    }

    private static string GetArgumentValue(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, name);
        return arguments[index + 1];
    }

    private static string GetOptionalArgumentValue(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, name);
        return index < 0 ? null : arguments[index + 1];
    }

    private BenchmarkResult CreateResult()
    {
        return new BenchmarkResult
        {
            timestampUtc = DateTime.UtcNow.ToString("O"),
            scenario = "Single-player Boss combat",
            unityVersion = Application.unityVersion,
            developmentBuild = Debug.isDebugBuild,
            operatingSystem = SystemInfo.operatingSystem,
            processor = SystemInfo.processorType,
            processorCount = SystemInfo.processorCount,
            systemMemoryMb = SystemInfo.systemMemorySize,
            graphicsDevice = SystemInfo.graphicsDeviceName,
            graphicsMemoryMb = SystemInfo.graphicsMemorySize,
            resolution = $"{Screen.width}x{Screen.height}",
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
            warmupSeconds = WarmupSeconds,
            sampleSeconds = SampleSeconds,
            sampleCount = _frameTimes.Count,
            frameTimeMs = Summarize(_frameTimes),
            fps = SummarizeFps(_frameTimes),
            mainThreadTimeMs = Summarize(_mainThreadTimes),
            renderThreadTimeMs = Summarize(_renderThreadTimes),
            gpuFrameTimeMs = Summarize(_gpuTimes),
            gcAllocatedBytesPerFrame = Summarize(_gcAllocations),
            zeroGcFramesPercent = CalculateZeroPercent(_gcAllocations),
            systemUsedMemoryMb = Summarize(_systemMemory),
            gcUsedMemoryMb = Summarize(_gcMemory),
            batches = Summarize(_batches),
            setPassCalls = Summarize(_setPassCalls),
            drawCalls = Summarize(_drawCalls),
            triangles = Summarize(_triangles),
            vertices = Summarize(_vertices)
        };
    }

    private static MetricSummary Summarize(List<double> source)
    {
        if (source.Count == 0)
            return new MetricSummary();

        double[] values = source.ToArray();
        Array.Sort(values);
        double total = 0;
        for (int i = 0; i < values.Length; i++)
            total += values[i];

        return new MetricSummary
        {
            average = total / values.Length,
            median = Percentile(values, 0.5),
            p95 = Percentile(values, 0.95),
            p99 = Percentile(values, 0.99),
            maximum = values[values.Length - 1]
        };
    }

    private static MetricSummary SummarizeFps(List<double> frameTimes)
    {
        var fps = new List<double>(frameTimes.Count);
        for (int i = 0; i < frameTimes.Count; i++)
            fps.Add(1000.0 / frameTimes[i]);
        return Summarize(fps);
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        int index = Mathf.Clamp(Mathf.CeilToInt((float)(sortedValues.Length * percentile)) - 1, 0, sortedValues.Length - 1);
        return sortedValues[index];
    }

    private static double CalculateZeroPercent(List<double> values)
    {
        if (values.Count == 0)
            return 0;

        int zeroCount = 0;
        for (int i = 0; i < values.Count; i++)
            if (values[i] == 0)
                zeroCount++;
        return zeroCount * 100.0 / values.Count;
    }

    [Serializable]
    private sealed class BenchmarkResult
    {
        public string timestampUtc;
        public string scenario;
        public string unityVersion;
        public bool developmentBuild;
        public string operatingSystem;
        public string processor;
        public int processorCount;
        public int systemMemoryMb;
        public string graphicsDevice;
        public int graphicsMemoryMb;
        public string resolution;
        public string quality;
        public float warmupSeconds;
        public float sampleSeconds;
        public int sampleCount;
        public MetricSummary frameTimeMs;
        public MetricSummary fps;
        public MetricSummary mainThreadTimeMs;
        public MetricSummary renderThreadTimeMs;
        public MetricSummary gpuFrameTimeMs;
        public MetricSummary gcAllocatedBytesPerFrame;
        public double zeroGcFramesPercent;
        public MetricSummary systemUsedMemoryMb;
        public MetricSummary gcUsedMemoryMb;
        public MetricSummary batches;
        public MetricSummary setPassCalls;
        public MetricSummary drawCalls;
        public MetricSummary triangles;
        public MetricSummary vertices;
    }

    [Serializable]
    private sealed class MetricSummary
    {
        public double average;
        public double median;
        public double p95;
        public double p99;
        public double maximum;
    }
}
