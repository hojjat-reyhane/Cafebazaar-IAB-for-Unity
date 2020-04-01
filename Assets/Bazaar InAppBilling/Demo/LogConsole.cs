using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A console to display Unity's debug logs runtime. developed by mohammad kaveh firouz "n2o_66@yahoo.com"
/// </summary>
///
[ExecuteInEditMode]
public class LogConsole : MonoBehaviour {

    struct Log {
        public string message;
        public string stackTrace;
        public LogType type;
    }

    public string consoleName = "Console";
    public KeyCode toggleKey = KeyCode.A;
    public bool dontDestroy;
    [Space(10)]
    public bool logAll;
    public bool logStack;
    public LogType dontLog;
    public int logLimit = 1000;
    [Space(10)]
    public bool showConsole = true;
    public bool debugOnScreen = false;
    public bool collapse;

    [Header("Console key")]
    [Range(0, 1)]
    public float consoleKeyposX;
    [Range(0, 1)]
    public float consoleKeyposY;
    [Range(0, 1)]
    public float consoleKeyHeight = 0.146f;
    [Range(0, 1)]
    public float consoleKeyWith = 0.076f;

    [Header("Style")]
    public GUIStyle style;
    public GUIStyle closeBtnStyle;
    [Range(0, 1)]
    public float closeBtnWith = 1;
    [Range(0, 1)]
    public float closeBtnHeight = 0.2f;
    public GUIStyle clearBtnStyle;
    [Range(0, 1)]
    public float clearBtnWith = 0.8f;
    [Range(0, 1)]
    public float clearBtnHeight = 0.2f;
    public GUIContent con;

    [Header("Refrences")]
    public GameObject[] activateList;

    private bool showKeys = true;
    private Vector2 scrollPosition;
    private List<Log> logs = new List<Log>();

    static readonly Dictionary<LogType, Color> logTypeColors = new Dictionary<LogType, Color>()
    {
        { LogType.Assert, Color.white },
        { LogType.Error, Color.red },
        { LogType.Exception, Color.red },
        { LogType.Log, Color.white },
        { LogType.Warning, Color.yellow },
    };

    [Header("Window")]
    [Range(0, 1)]
    public float windowHeight = 1;
    [Range(0, 1)]
    public float windowWith = 1;
    [Range(0, 1)]
    public float windowPosX = 0;
    [Range(0, 1)]
    public float windowPosY = 0;

    public Rect windowRect;//= new Rect(margin, margin, Screen.width - (margin * 2), Screen.height - (margin * 2));
    private Rect rect;

    Rect titleBarRect = new Rect(0, 0, 10000, 20);
    GUIContent clearLabel = new GUIContent("Clear", "Clear the contents of the console.");
    GUIContent closeLable = new GUIContent("Close", "Close the console.");
    GUIContent collapseLabel = new GUIContent("Collapse", "Hide repeated messages.");
    GUIContent text;
    GUIStyle errorStyle = new GUIStyle();
    public bool isDirty;
    private int lastSize;

    private void Awake() {
        if (dontDestroy && Application.isPlaying)
            DontDestroyOnLoad(gameObject);
        Initial();
    }

    void Initial() {
        Application.logMessageReceivedThreaded -= HandleLog;
        Application.logMessageReceivedThreaded += HandleLog;
    }

    void Start()
    {
        text = new GUIContent();
        errorStyle.fontSize = errorStyle.fontSize == 0 ? 20 : errorStyle.fontSize;
        errorStyle.normal.textColor = Color.white;
        errorStyle.wordWrap = true;
        style.normal.textColor = Color.white;
        style.wordWrap = true;
        style.fontSize = style.fontSize == 0 ? 20 : style.fontSize;
    }

    void OnEnable() {
        Application.logMessageReceivedThreaded -= HandleLog;
        Application.logMessageReceivedThreaded += HandleLog;
    }

    void OnDisable() {
        Application.logMessageReceivedThreaded -= HandleLog;
    }

    public void ShowConsole() {
        showConsole = !showConsole;
        for (int i = 0; i < activateList.Length; i++) {
            activateList[i].SetActive(showConsole);
        }
    }

    void OnGUI() {
        if (showConsole) {
            windowRect = new Rect(Screen.width * windowPosX, Screen.height * windowPosY, Screen.width * windowWith, Screen.height * windowHeight);
            windowRect = GUILayout.Window(123456, windowRect, ConsoleWindow, "Console");
        }

        showKeys = !showConsole;
        if (showKeys) {
            float h = consoleKeyposY * Screen.height;
            float w = consoleKeyposX * Screen.width;
            float hh = consoleKeyHeight * Screen.height;
            float ww = consoleKeyWith * Screen.width;

            if (GUI.Button(new Rect(w, h, hh, ww), consoleName)) {
                ShowConsole();
            }

        }

        if (!showConsole && debugOnScreen) {

            for (int i = 0; i < logs.Count; i++) {
                if (logs[i].type == LogType.Exception || logs[i].type == LogType.Error) {
                    GUI.contentColor = logTypeColors[logs[i].type];
                    GUILayout.Label(logStack ? logs[i].message + logs[i].stackTrace : logs[i].message, errorStyle);

                }
            }
        }
    }

    /// <summary>
    /// A window that displayss the recorded logs.
    /// </summary>
    /// <param name="windowID">Window ID.</param>
    void ConsoleWindow(int windowID) {
        closeBtnStyle.fixedWidth = Screen.width * closeBtnWith;
        closeBtnStyle.fixedHeight = Screen.height * closeBtnHeight;
        clearBtnStyle.fixedWidth = Screen.width * clearBtnWith;
        clearBtnStyle.fixedHeight = Screen.height * clearBtnHeight;
        GUILayout.BeginHorizontal();

        if (closeBtnStyle.normal.background) {
            if (GUILayout.Button(closeLable, closeBtnStyle))
                ShowConsole();
        } else
            if (GUILayout.Button(closeLable))
            ShowConsole();

        GUILayout.EndHorizontal();
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        for (int i = 0; i < logs.Count; i++) {
            var log = logs[i];

            if (collapse) {
                var messageSameAsPrevious = i > 0 && log.message == logs[i - 1].message;

                if (messageSameAsPrevious) {
                    continue;
                }
            }

            GUI.contentColor = logTypeColors[log.type];
            GUILayout.Label(logStack ? log.message + log.stackTrace : log.message, style);

            if (lastSize != logs.Count) {
                isDirty = true;
            }
        }

        GUILayout.EndScrollView();

        GUI.contentColor = Color.white;
        GUILayout.BeginHorizontal();

        if (clearBtnStyle.normal.background) {
            if (GUILayout.Button(clearLabel, clearBtnStyle))
                logs.Clear();
        } else
            if (GUILayout.Button(clearLabel))
            logs.Clear();


        collapse = GUILayout.Toggle(collapse, collapseLabel, GUILayout.ExpandWidth(false));
        GUILayout.EndHorizontal();
        GUI.DragWindow(titleBarRect);
    }

    /// <summary>
    /// Records a log from the log callback.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="stackTrace">Trace of where the message came from.</param>
    /// <param name="type">Type of message (error, exception, warning, assert).</param>
    void HandleLog(string message, string stackTrace, LogType type) {
        if (logs.Count > logLimit)
            logs.Clear();

        if (logAll) {
            logs.Add(new Log() {
                message = message,
                stackTrace = stackTrace,
                type = type,
            });
            return;
        }

        if (type != dontLog) {
            logs.Add(new Log() {
                message = message,
                stackTrace = stackTrace,
                type = type,
            });
        }
    }

    void Update() {
        if (Input.GetKeyDown(toggleKey)) {
            showConsole = !showConsole;
        }

        if (isDirty) {
            scrollPosition.y = Mathf.Infinity;
            isDirty = false;
            lastSize = logs.Count;
        }
    }
}
