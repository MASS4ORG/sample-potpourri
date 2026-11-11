namespace Usercode;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Turian.Engine.UI;

/// <summary>
/// Code-behind + data source for <c>Assets/UI/diagnostics.ui</c>, the world-space screen of the
/// in-scene 3D computer. Feeds synthetic telemetry and handles the on-screen buttons.
///
/// Bindings resolve against this controller, so the document uses paths like
/// <c>{Machine.CpuText}</c>. World-space panels and raycast input are still on the roadmap
/// (Epic 1 issues #5 / #6); until then this drives a screen-space panel just as well.
/// </summary>
public class DiagnosticsController : UiController
{
    /// <summary>Data context root for the document's <c>{Machine.*}</c> bindings.</summary>
    public MachineModel Machine { get; } = new();

    private float _accum;
    private readonly Random _rng = new(12345);

    /// <inheritdoc/>
    protected override void OnBind() => Machine.Seed();

    /// <inheritdoc/>
    public override void OnUpdate(float deltaTime)
    {
        _accum += deltaTime;
        Machine.Clock = DateTime.Now.ToString("HH:mm:ss");
        if (_accum < 0.5f) return;
        _accum = 0f;
        Machine.Tick(_rng);
    }

    /// <summary>Handles <c>click="OnScan"</c>.</summary>
    public void OnScan() => Machine.Push("scan: 0 threats found");

    /// <summary>Handles <c>click="OnReboot"</c>.</summary>
    public void OnReboot() => Machine.Push("reboot: scheduled in 3s");
}

#pragma warning disable CS1591 // plain telemetry DTOs bound by diagnostics.ui — members are self-describing

/// <summary>Observable telemetry model bound by <c>diagnostics.ui</c>.</summary>
public sealed class MachineModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public string Name { get; } = "ATOM-01";
    public ObservableCollection<ProcInfo> Processes { get; } = new();
    public ObservableCollection<string> Log { get; } = new();

    private string _clock = "--:--:--";
    public string Clock { get => _clock; set => Set(ref _clock, value, nameof(Clock)); }

    private float _cpu, _ram = 40f;
    public float CpuPercent { get => _cpu; private set => Set(ref _cpu, value, nameof(CpuPercent)); }
    public float RamPercent { get => _ram; private set => Set(ref _ram, value, nameof(RamPercent)); }
    public string CpuText => $"{_cpu:0}%";
    public string RamText => $"{_ram:0}% of 16 GB";

    public string NetDown { get; private set; } = "down  0.0 MB/s";
    public string NetUp { get; private set; } = "up    0.0 MB/s";

    private int _tab;
    public int Tab { get => _tab; set => Set(ref _tab, value, nameof(Tab)); }

    public void Seed()
    {
        string[] names = { "kernel", "compositor", "turian", "vulkan-drv", "audio", "net", "shell" };
        for (var i = 0; i < names.Length; i++)
            Processes.Add(new ProcInfo(1000 + i, names[i], 0.02f * i));
        Push("boot: userspace ready");
    }

    public void Tick(Random rng)
    {
        CpuPercent = Math.Clamp(CpuPercent + (float)(rng.NextDouble() - 0.5) * 30f, 3f, 98f);
        RamPercent = Math.Clamp(RamPercent + (float)(rng.NextDouble() - 0.5) * 6f, 20f, 92f);
        NetDown = $"down  {rng.NextDouble() * 12:0.0} MB/s";
        NetUp = $"up    {rng.NextDouble() * 3:0.0} MB/s";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CpuText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RamText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NetDown)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NetUp)));

        foreach (var p in Processes)
            p.Cpu = Math.Clamp(p.Cpu + (float)(rng.NextDouble() - 0.5) * 0.08f, 0f, 1f);
    }

    public void Push(string line)
    {
        Log.Insert(0, $"{DateTime.Now:HH:mm:ss}  {line}");
        while (Log.Count > 200) Log.RemoveAt(Log.Count - 1);
    }
}

/// <summary>One row of the process list.</summary>
public sealed class ProcInfo : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ProcInfo(int pid, string name, float cpu)
    {
        Pid = pid;
        Name = name;
        _cpu = cpu;
    }

    public int Pid { get; }
    public string Name { get; }

    private float _cpu;
    public float Cpu
    {
        get => _cpu;
        set
        {
            if (Math.Abs(_cpu - value) < 0.0001f) return;
            _cpu = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Cpu)));
        }
    }
}

#pragma warning restore CS1591
