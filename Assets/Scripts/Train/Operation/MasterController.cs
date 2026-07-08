using UnityEngine;

public class MasterController : MonoBehaviour, ITimsDataSource
{
    [SerializeField, Min(1)] private int maxPowerPosition = 4;
    [SerializeField, Min(1)] private int maxBrakePosition = 7;
    [SerializeField, Min(0.01f)] private float notchStepIntervalSeconds = 0.15f;
    [SerializeField] private bool acceptKeyboardInput = true;
    [SerializeField] private TrainController.ReverserPosition reverserPosition = TrainController.ReverserPosition.Neutral;

    private TimsSystem tims;
    private TimsCarTerminal terminal;
    private CabActivationSwitch activationSwitch;
    private float nextBrakeStepTime;
    private float nextNeutralStepTime;

    public int PowerPosition { get; private set; }
    public int BrakePosition { get; private set; }
    public float TransmissionIntervalSeconds => 0.05f;
    public TrainController.ReverserPosition ReverserPosition => reverserPosition;
    public bool IsNeutral => PowerPosition <= 0 && BrakePosition <= 0;

    private void Awake()
    {
        ResolveReferences();
        ClampPositions();
    }

    private void Update()
    {
        ResolveReferences();
        HandleKeyboardInput();
    }

    public void ConfigureLimits(int maxPower, int maxBrake)
    {
        maxPowerPosition = Mathf.Max(1, maxPower);
        maxBrakePosition = Mathf.Max(1, maxBrake);
        ClampPositions();
    }

    public void SetPowerPosition(int position)
    {
        PowerPosition = Mathf.Clamp(position, 0, maxPowerPosition);
        if (PowerPosition > 0)
        {
            BrakePosition = 0;
        }
    }

    public void SetBrakePosition(int position)
    {
        BrakePosition = Mathf.Clamp(position, 0, maxBrakePosition);
        if (BrakePosition > 0)
        {
            PowerPosition = 0;
        }
    }

    public void SetNeutral()
    {
        PowerPosition = 0;
        BrakePosition = 0;
    }

    public void SetReverserPosition(TrainController.ReverserPosition position)
    {
        reverserPosition = position;
    }

    private void ResolveReferences()
    {
        if (tims == null)
        {
            tims = GetComponentInParent<TimsSystem>();
        }

        if (terminal == null)
        {
            terminal = GetComponentInParent<TimsCarTerminal>();
        }

        if (activationSwitch == null)
        {
            Transform parent = transform.parent;
            if (parent != null)
            {
                activationSwitch = parent.GetComponentInChildren<CabActivationSwitch>(true);
            }
        }
    }

    private void HandleKeyboardInput()
    {
        if (!CanAcceptKeyboardInput())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (PowerPosition > 0)
            {
                SetPowerPosition(PowerPosition - 1);
            }
            else
            {
                SetBrakePosition(BrakePosition + 1);
            }
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (BrakePosition > 0)
            {
                SetBrakePosition(BrakePosition - 1);
            }
            else
            {
                SetPowerPosition(PowerPosition + 1);
            }
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            nextNeutralStepTime = 0f;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            nextBrakeStepTime = 0f;
        }

        if (Input.GetKey(KeyCode.LeftArrow) && CanStepNotch(ref nextNeutralStepTime))
        {
            StepTowardNeutral();
        }

        if (Input.GetKey(KeyCode.RightArrow) && CanStepNotch(ref nextBrakeStepTime))
        {
            StepTowardServiceMaxBrake();
        }

        if (PowerPosition > 0)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            SetReverserPosition(TrainController.ReverserPosition.Forward);
        }
        else if (Input.GetKeyDown(KeyCode.N))
        {
            SetReverserPosition(TrainController.ReverserPosition.Neutral);
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            SetReverserPosition(TrainController.ReverserPosition.Reverse);
        }
    }

    private bool CanStepNotch(ref float nextStepTime)
    {
        if (Time.time < nextStepTime)
        {
            return false;
        }

        nextStepTime = Time.time + notchStepIntervalSeconds;
        return true;
    }

    private void StepTowardServiceMaxBrake()
    {
        int serviceMaxBrakePosition = Mathf.Max(1, maxBrakePosition - 1);
        if (BrakePosition < serviceMaxBrakePosition)
        {
            SetBrakePosition(BrakePosition + 1);
        }
    }

    private void StepTowardNeutral()
    {
        if (BrakePosition > 0)
        {
            SetBrakePosition(BrakePosition - 1);
            return;
        }

        if (PowerPosition > 0)
        {
            SetPowerPosition(PowerPosition - 1);
        }
    }

    private bool CanAcceptKeyboardInput()
    {
        if (!acceptKeyboardInput)
        {
            return false;
        }

        if (tims == null || terminal == null || activationSwitch == null || tims.Terminals == null || tims.Terminals.Count == 0)
        {
            return true;
        }

        int rearIndex = tims.Terminals.Count - 1;
        return
            terminal.CarIndex == 0 && activationSwitch.Selection == CabDirectionSelection.Forward ||
            terminal.CarIndex == rearIndex && activationSwitch.Selection == CabDirectionSelection.Reverse;
    }

    public void WriteTimsData(TimsCarTerminal terminal)
    {
        if (terminal == null)
        {
            return;
        }

        TimsDataBus localBus = terminal.LocalBus;
        localBus.SetInt(new TimsTagKey("MasterController", "PowerPosition"), PowerPosition);
        localBus.SetInt(new TimsTagKey("MasterController", "BrakePosition"), BrakePosition);
        localBus.SetInt(new TimsTagKey("MasterController", "ReverserPosition"), (int)ReverserPosition);
        localBus.SetBool(new TimsTagKey("MasterController", "IsNeutral"), IsNeutral);
    }

    private void ClampPositions()
    {
        PowerPosition = Mathf.Clamp(PowerPosition, 0, maxPowerPosition);
        BrakePosition = Mathf.Clamp(BrakePosition, 0, maxBrakePosition);

        if (PowerPosition > 0 && BrakePosition > 0)
        {
            BrakePosition = 0;
        }
    }

    private void OnValidate()
    {
        maxPowerPosition = Mathf.Max(1, maxPowerPosition);
        maxBrakePosition = Mathf.Max(1, maxBrakePosition);
        notchStepIntervalSeconds = Mathf.Max(0.01f, notchStepIntervalSeconds);
        ClampPositions();
    }
}
