using System;
using System.ComponentModel;
using System.Threading;
using System.Collections.Generic;

[DisplayName("InvertAxisPlus")]
public class InvertAxisPlugin : PluginBase
{
    private Thread _thread;
    private bool _isRunning;
    private string _text;
    private double _l0Value;
    private double _axeValue;
    private Random _random;
    private double _previousAxeValue;
    private int _updateInterval;

    // Propriétés pour définir les valeurs de seuil de randomisation
    private double _randomThresholdMin;
    private double _randomThresholdMax;

    // Propriétés pour la sélection des axes
    private bool _isL1Selected;
    private bool _isL2Selected;
    private bool _isR0Selected;
    private bool _isR1Selected;
    private bool _isR2Selected;

    // Propriété pour le niveau de lissage
    private double _smoothingLevel;

    public ConnectionStatus Status
    {
        get => _status;
        set => SetAndNotify(ref _status, value);
    }
    private ConnectionStatus _status;

    public string Text
    {
        get => _text;
        set => SetAndNotify(ref _text, value);
    }

    public double L0Value
    {
        get => _l0Value;
        set => SetAndNotify(ref _l0Value, value);
    }

    public double AxeValue
    {
        get => _axeValue;
        set => SetAndNotify(ref _axeValue, value);
    }

    public double RandomThresholdMin
    {
        get => _randomThresholdMin;
        set
        {
            var newValue = Math.Max(0.0, Math.Min(value, 1.0));
            if (newValue > _randomThresholdMax - 0.05)
            {
                _randomThresholdMax = Math.Min(1.0, newValue + 0.05);
                OnPropertyChanged(nameof(RandomThresholdMax));
            }
            SetAndNotify(ref _randomThresholdMin, newValue);
        }
    }

    public double RandomThresholdMax
    {
        get => _randomThresholdMax;
        set
        {
            var newValue = Math.Max(0.0, Math.Min(value, 1.0));
            if (newValue < _randomThresholdMin + 0.05)
            {
                _randomThresholdMin = Math.Max(0.0, newValue - 0.05);
                OnPropertyChanged(nameof(RandomThresholdMin));
            }
            SetAndNotify(ref _randomThresholdMax, newValue);
        }
    }

    public bool IsL1Selected
    {
        get => _isL1Selected;
        set => SetAndNotify(ref _isL1Selected, value);
    }

    public bool IsL2Selected
    {
        get => _isL2Selected;
        set => SetAndNotify(ref _isL2Selected, value);
    }

    public bool IsR0Selected
    {
        get => _isR0Selected;
        set => SetAndNotify(ref _isR0Selected, value);
    }

    public bool IsR1Selected
    {
        get => _isR1Selected;
        set => SetAndNotify(ref _isR1Selected, value);
    }

    public bool IsR2Selected
    {
        get => _isR2Selected;
        set => SetAndNotify(ref _isR2Selected, value);
    }

    public double SmoothingLevel
    {
        get => _smoothingLevel;
        set => SetAndNotify(ref _smoothingLevel, value);
    }

    public int UpdateInterval
    {
        get => _updateInterval;
        set
        {
            if (value < 125) value = 125;
            if (value > 200) value = 200;
            SetAndNotify(ref _updateInterval, value);
        }
    }

    public enum SmoothingTypeEnum
    {
        None,
        Linear,
        Exponential,
        MovingAverage,
        Gaussian,
        Spline
    }

    private SmoothingTypeEnum _smoothingType;
    public SmoothingTypeEnum SmoothingType
    {
        get => _smoothingType;
        set => SetAndNotify(ref _smoothingType, value);
    }

    public IEnumerable<SmoothingTypeEnum> SmoothingTypes => Enum.GetValues(typeof(SmoothingTypeEnum)) as IEnumerable<SmoothingTypeEnum>;

    private Queue<double> _movingAverageQueue;
    private const int MovingAverageWindowSize = 5;

    protected override void OnInitialize()
    {
        _random = new Random();
        _randomThresholdMax = 0.55; // Initialiser d'abord Max
        _randomThresholdMin = 0.45; // Puis Min
        OnPropertyChanged(nameof(RandomThresholdMax));
        OnPropertyChanged(nameof(RandomThresholdMin));
        
        SmoothingLevel = 1.0;
        SmoothingType = SmoothingTypeEnum.Exponential;
        UpdateInterval = 150;
        _previousAxeValue = 0.0;
        _movingAverageQueue = new Queue<double>(MovingAverageWindowSize);
        Status = ConnectionStatus.Disconnected;
    }

    protected override void OnDispose()
    {
        Stop();
    }

    public void OnConnectClick()
    {
        if (_isRunning)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }

    private void Start()
    {
        if (_thread == null || !_thread.IsAlive)
        {
            _isRunning = true;
            _thread = new Thread(Run);
            _thread.Start();
            Status = ConnectionStatus.Connecting;
        }
    }

    private void Stop()
    {
        _isRunning = false;
        if (_thread != null && _thread.IsAlive)
        {
            _thread.Join();
        }
        Status = ConnectionStatus.Disconnected;
    }

    private void Run()
    {
        try
        {
            Status = ConnectionStatus.Connected;
            while (_isRunning)
            {
                L0Value = ReadProperty<DeviceAxis, double>("Axis::Value", DeviceAxis.Parse("L0"));
                double previousAxeValue = AxeValue;

                // Update Axe based on L0
                AxeValue = 0.5 - (L0Value - 0.5);

                // Inverse Axe if its value crosses a random threshold between RandomThresholdMin and RandomThresholdMax
                double randomThreshold = _random.NextDouble() * (RandomThresholdMax - RandomThresholdMin) + RandomThresholdMin;
                if ((previousAxeValue <= randomThreshold && AxeValue > randomThreshold) || (previousAxeValue > randomThreshold && AxeValue <= randomThreshold))
                {
                    AxeValue = 1.0 - AxeValue;
                }

                // Appliquer le lissage en fonction du type sélectionné
                switch (SmoothingType)
                {
                    case SmoothingTypeEnum.Linear:
                        AxeValue = _previousAxeValue * (1.0 - SmoothingLevel) + AxeValue * SmoothingLevel;
                        break;
                    case SmoothingTypeEnum.Exponential:
                        AxeValue = _previousAxeValue + (AxeValue - _previousAxeValue) * SmoothingLevel;
                        break;
                    case SmoothingTypeEnum.MovingAverage:
                        _movingAverageQueue.Enqueue(AxeValue);
                        if (_movingAverageQueue.Count > MovingAverageWindowSize)
                        {
                            _movingAverageQueue.Dequeue();
                        }
                        AxeValue = _movingAverageQueue.Average();
                        break;
                    case SmoothingTypeEnum.Gaussian:
                        AxeValue = _previousAxeValue * 0.6 + AxeValue * 0.4;
                        break;
                    case SmoothingTypeEnum.Spline:
                        AxeValue = (_previousAxeValue + AxeValue) / 2.0;
                        break;
                    case SmoothingTypeEnum.None:
                    default:
                        break;
                }
                _previousAxeValue = AxeValue;

                // Set values for selected axes if at least one axis is selected
                if (IsL1Selected || IsL2Selected || IsR0Selected || IsR1Selected || IsR2Selected)
                {
                    SetAxisValue("L1", IsL1Selected, AxeValue);
                    SetAxisValue("L2", IsL2Selected, AxeValue);
                    SetAxisValue("R0", IsR0Selected, AxeValue);
                    SetAxisValue("R1", IsR1Selected, AxeValue);
                    SetAxisValue("R2", IsR2Selected, AxeValue);

                    Text = $"L0: {L0Value:F5}, Axe: {AxeValue:F5}, Threshold: {randomThreshold:F5}";
                }
                else
                {
                    Text = $"L0: {L0Value:F5}, Aucun axe sélectionné";
                }

                Thread.Sleep(UpdateInterval);
            }
        }
        finally
        {
            Status = ConnectionStatus.Disconnecting;
            Thread.Sleep(500);
            Status = ConnectionStatus.Disconnected;
            Text = null;
        }
    }

    private void SetAxisValue(string axisName, bool isSelected, double value)
    {
        if (isSelected)
        {
            InvokeAction("Axis::Value::Set", DeviceAxis.Parse(axisName), value, 0.1);
        }
    }
}