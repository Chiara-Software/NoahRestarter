using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Windows.Forms;
using static System.Windows.Forms.AxHost;

namespace NoahRestarter
{
    public partial class MainForm : Form
    {
        private const string TaskName = "Restart Noah Services";
        private const string SvcServer = "NoahServer";
        private const string SvcClient = "NoahClient";
        private const string Ps1Path = @"C:\ProgramData\Chiara Software\Noah Restarter\Restart-Noah.ps1";

        private readonly Timer _timer = new Timer();
        private readonly Timer _closeTimer = new Timer();
        private readonly Stopwatch _sw = new Stopwatch();
        private const int TimeoutMs = 120_000; // 2 minutes

        private enum UiState { Launching, Stopping, Starting, Done, Error }
        private UiState _state = UiState.Launching;
        private DateTime _lastRunBefore;
        private const int TASK_STATE_READY = 3;
        private const int TASK_STATE_RUNNING = 4;
        private readonly System.Drawing.Color CInit = System.Drawing.Color.DodgerBlue;
        private readonly System.Drawing.Color CStop = System.Drawing.Color.Red;
        private readonly System.Drawing.Color CStart = System.Drawing.Color.DarkOrange;
        private readonly System.Drawing.Color COk = System.Drawing.Color.SeaGreen;

        public MainForm()
        {
            InitializeComponent();

            _timer.Interval = 500;
            _timer.Tick += (s, e) => TickCheck();

            _closeTimer.Interval = 1200;
            _closeTimer.Tick += (s, e) =>
            {
                _closeTimer.Stop();
                Close();
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (!CheckPrereqs(out var err))
            {
                ShowError(err);
                return;
            }

            _state = UiState.Launching;
            if (!TryGetTaskInfo(out _lastRunBefore, out _, out _))
            {
                ShowError("Impossible de lire l’état de la tâche planifiée.");
                return;
            }

            SetStatus("Initialisation…", CInit);
            progressBar.Style = ProgressBarStyle.Marquee;

            if (!RunTask(out var runErr))
            {
                ShowError("Impossible de lancer la tâche planifiée.\n" + runErr);
                return;
            }

            _sw.Start();
            _timer.Start();
        }

        private bool CheckPrereqs(out string error)
        {
            // 1) Script présent
            if (!System.IO.File.Exists(Ps1Path))
            {
                error = "Script manquant :\n" + Ps1Path + "\n\nRéinstalle l’outil ou contacte le support.";
                return false;
            }

            // 2) Tâche présente (sans droits admin)
            if (!ScheduledTaskExists(TaskName))
            {
                error = "Tâche planifiée manquante :\n" + TaskName + "\n\nRéinstalle l’outil ou contacte le support.";
                return false;
            }

            error = null;
            return true;
        }

        private bool ScheduledTaskExists(string taskName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/query /tn \"{taskName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(3000);
                    return p.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool RunTask(out string err)
        {
            err = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/run /tn \"{TaskName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(5000);
                    if (p.ExitCode == 0) 
                        return true;
                    err = (p.StandardError.ReadToEnd() + "\n" + p.StandardOutput.ReadToEnd()).Trim();
                    if (string.IsNullOrWhiteSpace(err)) 
                        err = $"ExitCode={p.ExitCode}";
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool TryGetTaskInfo(out DateTime lastRun, out int lastResult, out int state)
        {
            lastRun = DateTime.MinValue; lastResult = -1; state = -1;
            try
            {
                dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
                svc.Connect();
                dynamic task = svc.GetFolder("\\").GetTask(TaskName);
                lastRun = (DateTime)task.LastRunTime;
                lastResult = (int)task.LastTaskResult;
                state = (int)task.State;
                return true;
            }
            catch 
            { 
                return false; 
            }
        }

        private void TickCheck()
        {
            if (_sw.ElapsedMilliseconds > TimeoutMs)
            {
                _timer.Stop();
                ShowError("Timeout : le redémarrage a pris trop de temps.\nContactez le support.");
                return;
            }

            var server = GetStatusSafe(SvcServer);
            var client = GetStatusSafe(SvcClient);

            if (server == null || client == null)
            {
                _timer.Stop();
                ShowError("Services Noah introuvables (NoahServer/NoahClient).\nVérifiez que Noah est installé.");
                _state = UiState.Error;
                return;
            }

            if (!TryGetTaskInfo(out var lastRun, out var lastResult, out var taskState))
            {
                _timer.Stop();
                ShowError("Impossible de lire l’état de la tâche planifiée.");
                _state = UiState.Error;
                return;
            }

            bool taskHasStarted = lastRun > _lastRunBefore;
            bool taskRunning = taskState == TASK_STATE_RUNNING;

            switch (_state)
            {
                case UiState.Launching:
                    SetStatus("Initialisation…", CInit);
                    if (taskHasStarted && taskRunning)
                        _state = UiState.Stopping;
                    else if (taskHasStarted && !taskRunning)
                        _state = UiState.Starting; // exécution ultra rapide
                    break;

                case UiState.Stopping:
                    SetStatus("Arrêt des services Noah…", CStop);
                    if (server != ServiceControllerStatus.Running || client != ServiceControllerStatus.Running)
                        _state = UiState.Starting;
                    break;

                case UiState.Starting:
                    SetStatus("Redémarrage des services Noah…", CStart);

                    // Tant que la tâche tourne, on attend
                    if (taskRunning) break;

                    // Tâche terminée => LastTaskResult devient fiable
                    if (taskHasStarted && lastResult != 0)
                    {
                        _timer.Stop();
                        ShowError($"Erreur tâche planifiée (code {lastResult}).");
                        _state = UiState.Error;
                        return;
                    }

                    // Succès quand la tâche a tourné + finie, et services revenus Running
                    if (taskHasStarted && server == ServiceControllerStatus.Running && client == ServiceControllerStatus.Running)
                        _state = UiState.Done;
                    break;

                case UiState.Done:
                    SetStatus("OK ✅", COk);
                    statusLabel.ForeColor = System.Drawing.Color.Green;
                    _timer.Stop();
                    StartNoahGUI(); // relance Noah côté user
                    _closeTimer.Start();
                    break;
            }
        }

        private static ServiceControllerStatus? GetStatusSafe(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                    return sc.Status;
            }
            catch
            {
                return null;
            }
        }

        private void SetStatus(string text, System.Drawing.Color color)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = color;
        }

        private void ShowError(string msg)
        {
            progressBar.Style = ProgressBarStyle.Blocks;
            statusLabel.Text = msg;
            // Option : laisser la fenêtre ouverte, ou fermer après quelques secondes.
            // Ici on laisse ouvert.
        }

        private bool IsNoahInstalled
        {
            get
            {
                bool ret = false;
                // Assuming we are a 64 bit, use SOFTWARE\WOW6432Node\HIMSA\Installationinfo\Noah\v4
                // Assuming we are a 32 bit, use SOFTWARE\HIMSA\Installationinfo\Noah\v4

                var reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\HIMSA\Installationinfo\Noah\v4");
                if (reg != null)
                {
                    object o = reg.GetValue("Installed");
                    if (o != null)
                    {
                        if (o is int)
                        {
                            ret = (int)o != 0;
                        }
                    }
                }
                return ret;
            }
        }

        private string NoahInstallPath
        {
            get
            {
                // Assuming we are a 64 bit, use SOFTWARE\WOW6432Node\HIMSA\Installationinfo\Noah\v4
                // Assuming we are a 32 bit, use SOFTWARE\HIMSA\Installationinfo\Noah\v4
                string reg = Registry.GetValue($@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\HIMSA\Installationinfo\Noah\v4", "InstallPath", RegistryValueKind.String).ToString();
                return $@"{reg}Noah4.exe";
            }
        }

        private void StartNoahGUI()
        {
            if (!IsNoahInstalled)
                return;

            try
            {
                using (Process myProcess = new Process())
                {
                    myProcess.StartInfo.FileName = NoahInstallPath;
                    myProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                    myProcess.Start();
                }
            }
            catch (Exception e)
            {
                ShowError($"{e.Message}");
            }
        }
    }
}
