using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

namespace BuildLogger
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [Guid(LoggerPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class LoggerPackage : AsyncPackage, IVsUpdateSolutionEvents
    {
        /// <summary>
        /// VSPackage1 GUID string.
        /// </summary>
        public const string PackageGuidString = "80c3c801-0cc7-4120-acef-d8256ee43886";
        private IVsSolutionBuildManager2 _solutionService;
        private uint _cookieHandle;
        private Stopwatch stopwatch;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggerPackage"/> class.
        /// </summary>
        public LoggerPackage()
        {
            stopwatch = new Stopwatch();
        }

        #region Package Members

       protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            if (!Directory.Exists(GetLogFileRoot()))
            {
                Directory.CreateDirectory(GetLogFileRoot());
            }

            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _solutionService = await GetServiceAsync(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager2;
            if (_solutionService != null)
            {
                _solutionService.AdviseUpdateSolutionEvents(this, out _cookieHandle);
            }
        }



        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            base.Dispose(disposing);

            if (_solutionService != null && _cookieHandle != 0)
            {
                _solutionService.UnadviseUpdateSolutionEvents(_cookieHandle);
            }
        }

        public int UpdateSolution_Begin(ref int pfCancelUpdate)
        {
            stopwatch.Start();
            return VSConstants.S_OK;
        }

        public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand)
        {
            BuildEnd();
            return VSConstants.S_OK;
        }

        private void BuildEnd()
        {
            stopwatch.Stop();
            LogTime(stopwatch.Elapsed);
            stopwatch.Reset();
        }

        private void LogTime(TimeSpan elapsed)
        {
            try
            {
                int existingTime = ReadTime();
                WriteTime(existingTime + (int)Math.Floor(elapsed.TotalSeconds));
            } catch (Exception e)
            {
                int thing;
            }
        }

        private int ReadTime()
        {
            string s = File.ReadAllText(GetLogFilePath());

            return string.IsNullOrEmpty(s) ? 0 : int.Parse(s);
        }

        private void WriteTime(int v)
        {
            using (var file = new FileStream(GetLogFilePath(), FileMode.Truncate, FileAccess.Write))
            {
                using (var writer = new StreamWriter(file))
                {
                    writer.Write(v);
                }
            }
        }

        private string GetLogFilePath()
        {
            return $@"C:\build-log\build-log-{DateTime.Now.ToString(@"yyyy-MM-dd")}.txt";
        }

        private string GetLogFileRoot()
        {
            return $@"C:\build-log";
        }

        public int UpdateSolution_StartUpdate(ref int pfCancelUpdate)
        {
            return VSConstants.S_OK;
        }

        public int UpdateSolution_Cancel()
        {
            BuildEnd();
            return VSConstants.S_OK;
        }

        public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
        {
            return VSConstants.S_OK;
        }

        #endregion
    }
}
