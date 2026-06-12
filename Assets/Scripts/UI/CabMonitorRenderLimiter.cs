using UnityEngine;

public class CabMonitorRenderLimiter : MonoBehaviour
{
    [System.Serializable]
    private class MonitorRenderSetting
    {
        public Camera renderCamera;
        [Min(0.001f)] public float interval = 0.05f;
    }

    [SerializeField] private MonitorRenderSetting[] monitors;

    [SerializeField, HideInInspector] private Camera[] renderCameras;
    [SerializeField, HideInInspector] private float interval = 0.05f; // Legacy shared interval.

    private float[] timers;

    private void OnValidate()
    {
        interval = Mathf.Max(0.001f, interval);
        MigrateLegacySettingsIfNeeded();
        NormalizeMonitorSettings();
    }

    private void Awake()
    {
        MigrateLegacySettingsIfNeeded();
        NormalizeMonitorSettings();
        EnsureTimerCount();

        for (int i = 0; i < monitors.Length; i++)
        {
            Camera cam = monitors[i]?.renderCamera;
            if (cam != null)
            {
                cam.enabled = false;
            }
        }
    }

    private void Update()
    {
        if (monitors == null || monitors.Length == 0)
        {
            return;
        }

        EnsureTimerCount();

        for (int i = 0; i < monitors.Length; i++)
        {
            MonitorRenderSetting monitor = monitors[i];
            Camera cam = monitor?.renderCamera;
            if (cam == null)
            {
                continue;
            }

            timers[i] += Time.deltaTime;
            float renderInterval = Mathf.Max(0.001f, monitor.interval);
            if (timers[i] < renderInterval)
            {
                continue;
            }

            timers[i] = 0f;
            cam.Render();
        }
    }

    private void MigrateLegacySettingsIfNeeded()
    {
        if (monitors != null && monitors.Length > 0)
        {
            return;
        }

        if (renderCameras == null || renderCameras.Length == 0)
        {
            return;
        }

        monitors = new MonitorRenderSetting[renderCameras.Length];
        float migratedInterval = Mathf.Max(0.001f, interval);
        for (int i = 0; i < renderCameras.Length; i++)
        {
            monitors[i] = new MonitorRenderSetting
            {
                renderCamera = renderCameras[i],
                interval = migratedInterval
            };
        }
    }

    private void NormalizeMonitorSettings()
    {
        if (monitors == null)
        {
            monitors = System.Array.Empty<MonitorRenderSetting>();
            return;
        }

        for (int i = 0; i < monitors.Length; i++)
        {
            if (monitors[i] == null)
            {
                monitors[i] = new MonitorRenderSetting();
            }

            monitors[i].interval = Mathf.Max(0.001f, monitors[i].interval);
        }
    }

    private void EnsureTimerCount()
    {
        int count = monitors != null ? monitors.Length : 0;
        if (timers != null && timers.Length == count)
        {
            return;
        }

        float[] nextTimers = new float[count];
        if (timers != null)
        {
            int copyCount = Mathf.Min(timers.Length, nextTimers.Length);
            for (int i = 0; i < copyCount; i++)
            {
                nextTimers[i] = timers[i];
            }
        }

        timers = nextTimers;
    }
}
