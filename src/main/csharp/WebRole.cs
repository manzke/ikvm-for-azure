namespace TMGWebRole
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Xml.Linq;
    using System.Xml.XPath;
    using Ionic.Zip;
    using Microsoft.Web.Administration;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Diagnostics;
    using Microsoft.WindowsAzure.ServiceRuntime;
    using Microsoft.WindowsAzure.StorageClient;
    using WindowsAzure.DevelopmentFabric.IISConfigurator.Syncronizer;


    public class WebRole : RoleEntryPoint
    {
        const string DiagnosticsConnectionString = "Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString";
        const string tomcatFolder = "tomcat_5.0.28";
        const string jdkFolder = "jdk1.6.0_26";

        private readonly Action<string> info = s => Trace.TraceInformation(s);
        private readonly Action<string> warning = s => Trace.TraceWarning(s);
        private readonly Action<string> error = s => Trace.TraceError(s);
        private RestartingProcessHost restartingProcessHost;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        internal enum CatalinaAction { start, stop };

        private string relToTomcat (params string[] parts)
        {
            var paths = new List<string>(1 + parts.Length) { SettingsNames.TomcatFolder };
            paths.AddRange(parts);
            return Path.Combine(paths.ToArray());
        }

        private Func<Process> CreateTomcatProcessFactory(CatalinaAction catalinaAction)
        {
            // How could I stop catalina 'correctly'?
            //CreateTomcatProcessFactory(CatalinaAction.stop)().Start();



            return () =>
            {
                #region Create the process

                #region Calculate all the pathes Java needs

                // Func<string, string> relToTomcat = relativePath => Path.Combine(SettingsNames.TomcatFolder, relativePath);

                var classpath = new StringBuilder();
                classpath.Append(string.Format("{0}", relToTomcat(jdkFolder ,@"lib\tools.jar")));
                classpath.Append(";");
                classpath.Append(string.Format("{0}", relToTomcat(tomcatFolder, @"bin\bootstrap.jar")));

                var arguments = new StringBuilder();
                arguments.Append(string.Format(" -Djava.endorsed.dirs=\"{0}\"", relToTomcat(tomcatFolder, @"common\endorsed")));
                arguments.Append(string.Format(" -classpath \"{0}\"", classpath));
                arguments.Append(string.Format(" -Dcatalina.base=\"{0}\"", relToTomcat(tomcatFolder)));
                arguments.Append(string.Format(" -Dcatalina.home=\"{0}\"", relToTomcat(tomcatFolder)));
                arguments.Append(string.Format(" -Djava.io.tmpdir=\"{0}\"", relToTomcat(tomcatFolder, @"temp")));
                arguments.Append(string.Format(" org.apache.catalina.startup.Bootstrap"));
                arguments.Append(catalinaAction == CatalinaAction.start ? " start" : " stop");

                #endregion

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WorkingDirectory = SettingsNames.TomcatFolder,
                        FileName = relToTomcat(jdkFolder, @"bin\java.exe"),
                        Arguments = arguments.ToString(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };

                #region Setup environment

                Func<string, string> getVariable = variable => process.StartInfo.EnvironmentVariables[variable];
                Action<string, string> setVariable = (variable, value) => process.StartInfo.EnvironmentVariables[variable] = value;

                setVariable("JAVA_HOME", relToTomcat(jdkFolder));
                setVariable("JRE_HOME", relToTomcat(jdkFolder, @"jre"));
                setVariable("CATALINA_HOME", relToTomcat(tomcatFolder));
                setVariable("CATALINA_BASE", relToTomcat(tomcatFolder));
                setVariable("TOMCAT_HOME", relToTomcat(tomcatFolder));

                var searchPath = getVariable("PATH").Split(';').ToList();
                searchPath.Add(relToTomcat(jdkFolder, @"bin"));
                searchPath.Add(relToTomcat(jdkFolder, @"jre\bin"));
                searchPath.Add(relToTomcat(tomcatFolder, @"bin"));
                setVariable("PATH", string.Join(";", searchPath));

                #endregion

                Action<DataReceivedEventArgs> outputDataReceived = args => Trace.WriteLine(args.Data, "stdout");
                Action<DataReceivedEventArgs> errorDataReceived = args => Trace.WriteLine(args.Data, "stderr");

                process.OutputDataReceived += (s, a) => outputDataReceived(a);
                process.ErrorDataReceived += (s, a) => errorDataReceived(a);

                #endregion

                return process;
            };
        }

        public override bool OnStart()
        {
            info("Onstart() called");

            #region Download and install apps

            #region some helpers

            Func<string, string> setting = RoleEnvironment.GetConfigurationSettingValue;

            Action<string, string> extract = (localfilename, folder) =>
            {
                #region extract

                localfilename = new FileInfo(localfilename).FullName;

                info(string.Format("INFO: Requested to extract file \"{0}\" to folder \"{1}\"", localfilename, folder));

                if (!File.Exists(localfilename))
                {
                    error(string.Format("FATAL: Cannot find file {0}", localfilename));
                    throw new FileNotFoundException(string.Format("Missing file {0}", localfilename));
                }

                using (var zip = Ionic.Zip.ZipFile.Read(localfilename))
                {
                    zip.ExtractAll(folder, ExtractExistingFileAction.OverwriteSilently);
                }

                info(string.Format("INFO: Extracted file \"{0}\" to folder \"{1}\"", localfilename, folder));

                #endregion
            };

            #region downloadPublic

            //Action<string,string> downloadPublic = (filename, localfilename) =>
            //{
            //    // if (File.Exists(filename)) return;
            //    var address = string.Format("https://{0}.blob.core.windows.net/{1}/{2}",
            //                    setting(SettingsNames.TMG_Blob_Account),
            //                    setting(SettingsNames.TMG_Blob_Container), filename);
            //    Trace.TraceInformation(string.Format("Downloading {0} to {1}", address, filename));
            //    new WebClient().DownloadFile(address, localfilename);
            //};

            #endregion

            Action<string, string> downloadPrivate = (filename, localfilename) =>
            {
                #region downloadPrivate

                if (File.Exists(localfilename))
                {
                    warning(string.Format("INFO: File \"{0}\" alrady exists, skipping download", localfilename));
                    return;
                }

                var connectionString = setting(SettingsNames.TMG_Storage_ConnectionString);
                var containerName = setting(SettingsNames.TMG_Storage_PrivateBlobContainer);

                var account = CloudStorageAccount.Parse(connectionString);
                var blobClient = account.CreateCloudBlobClient();
                var privateContainer = blobClient.GetContainerReference(containerName);
                var blob = privateContainer.GetBlobReference(filename);

                info(string.Format("Try to download blob {0}", blob.Uri.AbsoluteUri));

                try
                {
                    blob.FetchAttributes();
                    blob.DownloadToFile(localfilename);
                    info(string.Format("Successfully downloaded blob {0}", blob.Uri.AbsoluteUri));
                }
                catch
                {
                    error(string.Format("Failed to download blob {0}", blob.Uri.AbsoluteUri));
                }

                #endregion
            };

            Action<string, string, string, Action<string, string>> downloadAndExtract = (filename, localfilename, folder, download) =>
            {
                download(filename, localfilename);
                extract(localfilename, folder);

                info(string.Format("Installed {0}", filename));
            };

            #endregion

            bool installSuccessful = false;
            while (!installSuccessful)
            {
                try
                {
                    SettingsNames.Files
                        .AsParallel()
                        .ForAll(filename => downloadAndExtract(
                            filename, Path.Combine(SettingsNames.TomcatFolder, filename), 
                            SettingsNames.TomcatFolder, downloadPrivate));
                    installSuccessful = true;
                }
                catch (Exception ex)
                {
                    error("Problem during installation, trying again: " + ex.Message);
                }
            }

            #endregion

            #region patch app configuration

            #region patch app.ini

            File.Delete(relToTomcat(tomcatFolder, @"webapps\tmg\WEB-INF\app.ini"));
            var content = File.ReadAllText(@"tomcat_5.0.28\webapps\tmg\WEB-INF\app.ini");
            content = content.Replace("THE_BASE_FOLDER", SettingsNames.TomcatFolder);
            File.WriteAllText(relToTomcat(tomcatFolder, @"webapps\tmg\WEB-INF\app.ini"), content);

            info(string.Format("Patched {0}", relToTomcat(tomcatFolder, @"webapps\tmg\WEB-INF\app.ini")));

            #endregion

            #region patch IP address and port

            Action<XDocument, string,string,string> setAttr = (doc, xpath, attributeName, attributeValue) =>
            {
                XElement o = doc.XPathSelectElement(xpath);
                var attr = o.Attribute(attributeName);
                if (attr == null)
                {
                    attr = new XAttribute(attributeName, attributeValue);
                    o.Add(attr);
                }
                else
                {
                    attr.Value = attributeValue;
                }
            };

            var server_xml_filename = relToTomcat(tomcatFolder, @"conf\server.xml");
            var server_xml = XDocument.Parse(File.ReadAllText(server_xml_filename));

            #region shutdown port. This unfortunately binds globally, so we cannot specify an IPListenAddress. That's why each Tomcat instance in the development fabric needs it's own port..

            var s = RoleEnvironment.CurrentRoleInstance.Id;
            s = s.Substring(s.LastIndexOf("_") + 1);
            int shutdownPortOffset = int.Parse(s);
            int shutdownPort = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["tomcatShutdownPort"].IPEndpoint.Port + shutdownPortOffset;
            setAttr(server_xml, "/Server", "port", shutdownPort.ToString());

            #endregion

            setAttr(server_xml, "/Server/Service[@name='Catalina']/Connector[1]", "port", 
                RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["tomcat"].IPEndpoint.Port.ToString());
            setAttr(server_xml, "/Server/Service[@name='Catalina']/Connector[1]", "address",
                    RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["tomcat"].IPEndpoint.Address.ToString());
            setAttr(server_xml, "/Server/Service[@name='Catalina']/Connector[2]", "address",
                    RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["tomcat"].IPEndpoint.Address.ToString());

            File.WriteAllText(server_xml_filename, server_xml.ToString());

            #endregion

            #endregion

            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            #region Log files

            ServicePointManager.DefaultConnectionLimit = 12;

            var scheduledTransferPeriod = TimeSpan.FromMinutes(1);
            var diagConfig = DiagnosticMonitor.GetDefaultInitialConfiguration();
            diagConfig.DiagnosticInfrastructureLogs.ScheduledTransferLogLevelFilter = LogLevel.Information;
            diagConfig.DiagnosticInfrastructureLogs.ScheduledTransferPeriod = scheduledTransferPeriod;
            diagConfig.WindowsEventLog.ScheduledTransferLogLevelFilter = LogLevel.Warning;
            diagConfig.WindowsEventLog.ScheduledTransferPeriod = scheduledTransferPeriod;
            diagConfig.Logs.ScheduledTransferLogLevelFilter = LogLevel.Verbose;
            diagConfig.Logs.ScheduledTransferPeriod = scheduledTransferPeriod;

            #region Add custom folders to log

            new string[][]
            {
                new string[] {@"sis\reports", "sisreports"},
                new string[] {@"sis\docs", "sisdocs"},
                new string[] {@"sis\oppdocs", "sisoppdocs"}
            }
            .Select(entry => new DirectoryConfiguration
            {
                Path = new DirectoryInfo(Path.Combine(SettingsNames.TomcatFolder, entry[0])).FullName,
                Container = entry[1],
                DirectoryQuotaInMB = 100
            }).ToList()
            .ForEach(diagConfig.Directories.DataSources.Add);

            diagConfig.Directories.ScheduledTransferPeriod = scheduledTransferPeriod;

            #endregion

            diagConfig.ConfigurationChangePollInterval = TimeSpan.FromMinutes(1);
            DiagnosticMonitor.Start(DiagnosticsConnectionString, diagConfig);

            #endregion

            RoleEnvironment.Stopping += (sender, args) => cancellationTokenSource.Cancel();

            restartingProcessHost = new RestartingProcessHost(
                CreateTomcatProcessFactory(CatalinaAction.start), 
                cancellationTokenSource)
            {
                error = error,
                info = info,
                warn = warning
            };

            #region Setup ARR

            Action<ServerManager> configureApplicationRequestRouting = sm => { info("Here I would call server manager..."); Thread.Sleep(TimeSpan.FromSeconds(1)); };
            ServerManagerBarrier.ApplyServerManagerActions(configureApplicationRequestRouting);

            #endregion

            info("OnStart() finished");
            return base.OnStart();
        }

        public override void Run()
        {
            info("Run() called");

            var runTask = restartingProcessHost.StartRunTask();
            runTask.Wait(cancellationTokenSource.Token);
        }

        public override void OnStop()
        {
            warning("OnStop() called, cancelling Tomcat");

            cancellationTokenSource.Cancel();
        }
        
        public static class SettingsNames
        {
            internal static string TomcatFolder 
            { 
                get 
                {
                    if (!RoleEnvironment.IsAvailable)
                    {
                        throw new NotSupportedException("Must run in Azure");
                    }

                    return RoleEnvironment.GetLocalResource(TMG_LocalResourceName).RootPath; 
                } 
            }

            internal const string TMG_LocalResourceName = "TC";
            internal const string TMG_Storage_ConnectionString = "TMG.Storage";
            internal const string TMG_Storage_PrivateBlobContainer = "TMG.Storage.PrivateBlobContainer";

            private const string TMG_Blob_Files = "TMG.Blob.Files";
            public static IEnumerable<string> Files
            {
                get
                {
                    Func<string, string> s = RoleEnvironment.GetConfigurationSettingValue;

                    return s(SettingsNames.TMG_Blob_Files)
                        .Split(',')
                        .Select(str => str.Trim());
                }
            }
        }
    }
}
