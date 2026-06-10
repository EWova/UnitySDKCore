using EWova.Auth;

using System.Text;

using TMPro;

using UnityEngine;

public class PrintText : MonoBehaviour
{
    public TextMeshProUGUI TextMeshProUGUI;

    private void Awake()
    {
        EwovaAuthManager.Logger.LogReceived += Log_LogReceived;
    }
    private void OnDestroy()
    {
        EwovaAuthManager.Logger.LogReceived -= Log_LogReceived;
    }

    StringBuilder stringBuilder = new StringBuilder();
    private void Log_LogReceived(EWova.LogEntry entry)
    {
        stringBuilder.AppendLine(entry.ToString());
        TextMeshProUGUI.text = stringBuilder.ToString();
    }
}
